using System;
using System.Collections;
using UnityEngine;
using Game.Characters;
using Game.Enemies;
using Game.UI.Intermission;

namespace Game.Core.Stage
{
    /// <summary>
    /// 스테이지 방 전환 오케스트레이터.
    ///
    /// [역할]
    ///   - StageManager.OnRoomEntered 구독 → 방 종류에 따라 보스 스폰 또는 인터미션 UI 표시
    ///   - 보스 사망 → StageManager.NotifyBossDefeated()
    ///   - 플레이어 사망 → 이어하기 팝업 표시
    ///   - 인터미션 선택 완료 → StageManager.NotifyIntermissionComplete()
    ///
    /// [보스 전투 테스트]
    ///   신규 보스(12-C) 참조 금지. TitanBossPresenter(또는 지정 프리팹) 재사용.
    ///
    /// [수정 금지 영역]
    ///   BossRushManager 등록/참조 금지 (12-E 영역).
    /// </summary>
    public class ProgressionManager : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private StageManager        _stageManager;
        [SerializeField] private IntermissionManager _intermissionManager;

        [Header("보스 스폰")]
        [SerializeField] private Vector2 _bossSpawnPosition = Vector2.zero;

        // ── 런타임 ───────────────────────────────────────────────────
        private EnemyPresenterBase _currentBoss;
        private bool               _bossAlive;
        private bool               _playerDeathHandled;

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
        }

        // ── 방 진입 처리 ─────────────────────────────────────────────
        private void HandleRoomEntered(RoomNode room)
        {
            // 이전 방 리소스 정리 (코루틴/구독 잔재 방지)
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

            Debug.Log("[ProgressionManager] 보스 처치 → StageManager 알림");
            _stageManager?.NotifyBossDefeated();
        }

        // ── 인터미션 방 ──────────────────────────────────────────────
        private void HandleIntermissionEntered()
        {
            // HP 100% 회복
            HealPlayerToFull();

            // 인터미션 UI 열기
            if (_intermissionManager != null)
                _intermissionManager.Open(_stageManager.RunStat, _stageManager);
            else
                Debug.LogWarning("[ProgressionManager] IntermissionManager가 없음. 스탯 선택 UI 생략.");
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

            // CharacterModel의 Heal()을 최대 HP만큼 호출하여 100% 회복
            var rapier = player as Game.Characters.RapierPresenter;
            if (rapier != null && rapier.PublicModel != null)
            {
                var model = rapier.PublicModel;
                model.Heal(model.StatData.maxHp); // maxHp 이상 heal해도 Clamp 처리됨
                Debug.Log($"[ProgressionManager] HP 100% 회복 완료. 현재: {model.CurrentHp}/{model.StatData.maxHp}");
            }
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

            // 인터미션 매니저에 사망 팝업 요청
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
