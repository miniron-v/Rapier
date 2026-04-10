using System;
using System.Collections;
using UnityEngine;
using Game.Characters;
using Game.Enemies;
using Game.UI.Intermission;
using Game.Core;

namespace Game.Core.Stage
{
    /// <summary>
    /// 스테이지 방 전환 오케스트레이터.
    ///
    /// [역할]
    ///   - StageManager.OnRoomEntered 구독 → 방 종류에 따라 보스 스폰 또는 인터미션 처리
    ///   - 보스 사망 → 포탈 스폰
    ///   - 포탈 진입 → StageManager.NotifyPortalEntered()
    ///   - 인터미션 진입 → 플레이어 위치 리셋 + UI + 포탈 스폰
    ///   - 플레이어 사망 → 이어하기 팝업 표시
    /// </summary>
    public class ProgressionManager : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private StageManager        _stageManager;
        [SerializeField] private IntermissionManager _intermissionManager;

        [Header("보스 스폰")]
        [SerializeField] private Vector2 _bossSpawnPosition = Vector2.zero;

        [Header("포탈 / 플레이어 스폰")]
        [SerializeField] private Portal  _portalPrefab;           // null이면 런타임 생성
        [SerializeField] private Vector2 _playerSpawnPosition = new Vector2(0f, -3f);
        [SerializeField] private Vector2 _portalSpawnPosition  = new Vector2(0f,  3f);

        // ── 런타임 ───────────────────────────────────────────────────
        private EnemyPresenterBase _currentBoss;
        private bool               _bossAlive;
        private bool               _playerDeathHandled;
        private Portal             _activePortal;

        // ── 이벤트 구독/해제 ─────────────────────────────────────────
        private void OnEnable()
        {
            if (_stageManager != null)
                _stageManager.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            if (_stageManager != null)
                _stageManager.OnRoomEntered -= HandleRoomEntered;

            UnsubscribeBoss();
            UnsubscribePlayer();
            CleanupPortal();
        }

        // ── 방 진입 처리 ─────────────────────────────────────────────
        private void HandleRoomEntered(RoomNode room)
        {
            CleanupPortal();

            // 이전 방 리소스 정리
            UnsubscribeBoss();
            CleanupCurrentBoss();

            _playerDeathHandled = false;

            switch (room.roomType)
            {
                case RoomType.BossRoom:
                    StartCoroutine(SpawnBossRoutine(room));
                    break;

                case RoomType.IntermissionRoom:
                    HandleIntermissionEntered();
                    break;
            }
        }

        // ── 보스 방 ─────────────────────────────────────────────────
        private IEnumerator SpawnBossRoutine(RoomNode room)
        {
            yield return new WaitForSeconds(0.3f);

            if (room.bossPrefab == null)
            {
                Debug.LogError($"[ProgressionManager] 보스 프리팹이 없습니다: {room.displayName}");
                yield break;
            }

            var go = Instantiate(room.bossPrefab, _bossSpawnPosition, Quaternion.identity);
            _currentBoss = go.GetComponent<EnemyPresenterBase>();

            if (_currentBoss == null)
            {
                Debug.LogError($"[ProgressionManager] {room.bossPrefab.name}에 EnemyPresenterBase가 없음!");
                yield break;
            }

            if (room.bossStatData != null)
                _currentBoss.Spawn(room.bossStatData, _bossSpawnPosition);

            _bossAlive = true;
            _currentBoss.OnDeath += HandleBossDeath;

            // 플레이어 사망 구독 (방 진입마다 갱신)
            SubscribePlayer();

            Debug.Log($"[ProgressionManager] 보스 스폰: {room.displayName} @ {_bossSpawnPosition}");
        }

        private void HandleBossDeath()
        {
            if (!_bossAlive) return;
            _bossAlive = false;
            UnsubscribeBoss();

            Debug.Log("[ProgressionManager] 보스 처치 → 포탈 스폰");
            SpawnPortal(_portalSpawnPosition);
        }

        // ── 인터미션 방 ──────────────────────────────────────────────
        private void HandleIntermissionEntered()
        {
            // 플레이어 위치 고정 스폰
            ResetPlayerPosition(_playerSpawnPosition);

            // 이어하기 진입이면 부활 처리, 아니면 HP 회복
            if (_stageManager != null && _stageManager.IsContinueMode)
                RevivePlayer();
            else
                HealPlayerToFull();

            // 인터미션 UI 열기 (continue 여부는 IntermissionManager 내부에서 판단 — IsContinueMode면 UI 생략)
            if (_intermissionManager != null)
                _intermissionManager.Open(_stageManager.RunStat, _stageManager);
            else
                Debug.LogWarning("[ProgressionManager] IntermissionManager가 없음. 스탯 선택 UI 생략.");

            // 포탈 즉시 스폰
            SpawnPortal(_portalSpawnPosition);
        }

        // ── 플레이어 위치 리셋 ────────────────────────────────────────
        private void ResetPlayerPosition(Vector2 pos)
        {
            var player = FindObjectOfType<CharacterPresenterBase>(true);
            if (player == null)
            {
                Debug.LogWarning("[ProgressionManager] 플레이어를 찾을 수 없어 위치 리셋 불가.");
                return;
            }
            player.Warp(pos);
            Debug.Log($"[ProgressionManager] 플레이어 위치 리셋 → {pos}");
        }

        // ── 플레이어 부활 ─────────────────────────────────────────────
        private void RevivePlayer()
        {
            var player = FindObjectOfType<CharacterPresenterBase>(true);
            if (player == null)
            {
                Debug.LogWarning("[ProgressionManager] 부활할 플레이어를 찾을 수 없음.");
                return;
            }
            player.Revive();
            Debug.Log("[ProgressionManager] 플레이어 부활 완료.");
        }

        // ── 플레이어 HP 회복 ─────────────────────────────────────────
        private void HealPlayerToFull()
        {
            var player = FindObjectOfType<CharacterPresenterBase>();
            if (player == null)
            {
                Debug.LogWarning("[ProgressionManager] 플레이어를 찾을 수 없어 HP 회복 불가.");
                return;
            }

            var rapier = player as RapierPresenter;
            if (rapier != null && rapier.PublicModel != null)
            {
                var model = rapier.PublicModel;
                model.Heal(model.StatData.maxHp);
                Debug.Log($"[ProgressionManager] HP 100% 회복 완료. 현재: {model.CurrentHp}/{model.StatData.maxHp}");
            }
        }

        // ── 포탈 수명 관리 ────────────────────────────────────────────
        private void SpawnPortal(Vector2 pos)
        {
            CleanupPortal();

            if (_portalPrefab != null)
                _activePortal = Instantiate(_portalPrefab, pos, Quaternion.identity);
            else
            {
                var go = new GameObject("Portal");
                go.transform.position = pos;
                _activePortal = go.AddComponent<Portal>();
            }

            _activePortal.OnPlayerEntered += HandlePortalEntered;
            Debug.Log($"[ProgressionManager] 포탈 스폰 @ {pos}");
        }

        private void CleanupPortal()
        {
            if (_activePortal == null) return;
            _activePortal.OnPlayerEntered -= HandlePortalEntered;
            Destroy(_activePortal.gameObject);
            _activePortal = null;
            Debug.Log("[ProgressionManager] 포탈 정리 완료.");
        }

        private void HandlePortalEntered()
        {
            Debug.Log("[ProgressionManager] 포탈 진입 → StageManager 알림");
            CleanupPortal();
            _stageManager?.NotifyPortalEntered();
        }

        // ── 플레이어 사망 처리 ───────────────────────────────────────
        private CharacterPresenterBase _subscribedPlayer;

        private void SubscribePlayer()
        {
            UnsubscribePlayer();
            _subscribedPlayer = FindObjectOfType<CharacterPresenterBase>();
            if (_subscribedPlayer != null)
                _subscribedPlayer.OnPlayerDeath += HandlePlayerDeath;
        }

        private void UnsubscribePlayer()
        {
            if (_subscribedPlayer != null)
            {
                _subscribedPlayer.OnPlayerDeath -= HandlePlayerDeath;
                _subscribedPlayer = null;
            }
        }

        private void HandlePlayerDeath()
        {
            if (_playerDeathHandled) return;
            _playerDeathHandled = true;

            Debug.Log("[ProgressionManager] 플레이어 사망 → 사망 팝업 표시");

            if (_intermissionManager != null)
                _intermissionManager.ShowDeathPopup(_stageManager);
            else
                Debug.LogWarning("[ProgressionManager] IntermissionManager 없음 — 사망 팝업 생략.");
        }

        // ── 정리 유틸 ────────────────────────────────────────────────
        private void UnsubscribeBoss()
        {
            if (_currentBoss != null)
                _currentBoss.OnDeath -= HandleBossDeath;
        }

        private void CleanupCurrentBoss()
        {
            if (_currentBoss != null)
            {
                Destroy(_currentBoss.gameObject);
                _currentBoss = null;
            }
            _bossAlive = false;
        }
    }
}
