using System;
using System.Collections;
using UnityEngine;
using Game.Characters;
using Game.Enemies;
using Game.UI;
using Game.UI.Intermission;
using Game.Core;
using Game.Data.Stage;

namespace Game.Core.Stage
{
    /// <summary>
    /// 스테이지 방 전환 오케스트레이터.
    ///
    /// [역할]
    ///   - StageManager.OnRoomEntered 구독 → 방 종류에 따라 보스 스폰 또는 인터미션 처리
    ///   - 보스 스폰 시 씬의 BossHudView에 SetupBoss/UpdatePhase/ShowResult를 호출해 HUD를 구동
    ///   - 보스 사망 → 포탈 스폰 (마지막 방이면 HUD ShowResult(true) 추가 호출)
    ///   - 포탈 진입 → StageManager.NotifyPortalEntered()
    ///   - 인터미션 진입 → 플레이어 위치 리셋 + UI + 포탈 스폰
    ///   - 플레이어 사망 → 이어하기 팝업 표시
    /// </summary>
    public class ProgressionManager : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private StageManager        _stageManager;
        [SerializeField] private IntermissionManager _intermissionManager;

        [Header("Boss HUD")]
        [SerializeField] private BossHudView _bossHud; // 씬에서 연결. 없으면 null 허용.

        [Header("보스 스폰")]
        [SerializeField] private Vector2 _bossSpawnPosition = Vector2.zero;

        [Header("포탈 / 플레이어 스폰")]
        [SerializeField] private Portal  _portalPrefab;           // null이면 런타임 생성
        [SerializeField] private Vector2 _playerSpawnPosition = new Vector2(0f, -3f);
        [SerializeField] private Vector2 _portalSpawnPosition  = new Vector2(0f,  3f);

        // ── 런타임 ───────────────────────────────────────────────────
        private EnemyPresenterBase _currentBoss;
        private BossPresenterBase  _currentBossPresenter; // BossPresenterBase 캐스팅 캐시
        private bool               _bossAlive;
        private bool               _playerDeathHandled;
        private Portal             _activePortal;
        private int                _currentBossRoomIndex; // 보스 방 진입 순번 (1-based)

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

            // 모든 방 진입 시 플레이어를 고정 시작 위치로 리셋
            // (보스방은 이전 방의 포탈 위치 잔재 제거, 인터미션은 스탯 선택 대기 위치 확보)
            ResetPlayerPosition(_playerSpawnPosition);

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
            {
                // 스테이지 스케일링: EnemyModel 레벨에서 multiplier 적용.
                // BossStatData SO는 불변이므로, Spawn 후 EnemyPresenterBase.ApplyStageMultipliers 호출.
                _currentBoss.Spawn(room.bossStatData, _bossSpawnPosition);

                // StageData multiplier 적용 (있는 경우)
                var stageData = _stageManager != null ? _stageManager.CurrentStageData : null;
                if (stageData != null)
                {
                    float hp  = stageData.HpMultiplier;
                    float atk = stageData.AtkMultiplier;
                    if (hp != 1f || atk != 1f)
                    {
                        _currentBoss.ApplyStageMultipliers(hp, atk);
                        Debug.Log($"[ProgressionManager] 스테이지 배율 적용: HP×{hp}, ATK×{atk}");
                    }
                }
            }

            _bossAlive = true;
            _currentBoss.OnDeath += HandleBossDeath;

            // BossPresenterBase 캐스팅 → HUD + 페이즈 이벤트 연결
            _currentBossPresenter = _currentBoss as BossPresenterBase;
            if (_currentBossPresenter != null)
            {
                _currentBossPresenter.OnPhaseChanged += HandleBossPhaseChanged;

                // StageManager.BossesDefeated(처치 수) 기반으로 현재 보스 방 번호를 계산한다.
                // 사망 후 이어하기로 같은 보스 방에 재진입해도 누적 증가하지 않는다.
                _currentBossRoomIndex = _stageManager != null ? _stageManager.BossesDefeated + 1 : _currentBossRoomIndex + 1;
                int totalBossRooms    = _stageManager != null ? _stageManager.TotalBossRooms       : 0;
                string bossDisplayName = room.bossStatData?.enemyName
                    ?? room.displayName
                    ?? room.bossPrefab.name;

                _bossHud?.SetupBoss(bossDisplayName, _currentBossPresenter, _currentBossRoomIndex, totalBossRooms);
                Debug.Log($"[ProgressionManager] HUD SetupBoss: {bossDisplayName} ({_currentBossRoomIndex}/{totalBossRooms})");
            }
            else
            {
                Debug.LogWarning($"[ProgressionManager] {room.bossPrefab.name}이 BossPresenterBase가 아님. HUD 연결 생략.");
            }

            // 플레이어 사망 구독 (방 진입마다 갱신)
            SubscribePlayer();

            Debug.Log($"[ProgressionManager] 보스 스폰: {room.displayName} @ {_bossSpawnPosition}");
        }

        private void HandleBossPhaseChanged(int phaseIndex)
        {
            _bossHud?.UpdatePhase(phaseIndex);
        }

        private void HandleBossDeath()
        {
            if (!_bossAlive) return;
            _bossAlive = false;
            UnsubscribeBoss();

            // 마지막 보스 방 처치 여부 판정
            bool isFinalBoss = _stageManager != null
                && _currentBossRoomIndex >= _stageManager.TotalBossRooms;

            Debug.Log($"[ProgressionManager] 보스 처치. 마지막: {isFinalBoss}. → 포탈 스폰");
            SpawnPortal(_portalSpawnPosition);

            if (isFinalBoss)
                _bossHud?.ShowResult(true);
        }

        // ── 인터미션 방 ──────────────────────────────────────────────
        private void HandleIntermissionEntered()
        {
            // (플레이어 위치 리셋은 HandleRoomEntered 공통 처리)

            // 이어하기 진입이면 부활 처리, 아니면 HP 회복
            if (_stageManager != null && _stageManager.IsContinueMode)
            {
                RevivePlayer();
                // 이어하기 진입: 결과/승리 패널 초기화
                HideHudPanels();
            }
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
                model.Heal(model.MaxHp);
                Debug.Log($"[ProgressionManager] HP 100% 회복 완료. 현재: {model.CurrentHp}/{model.MaxHp}");
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

        // ── 이어하기 HUD 리셋 ────────────────────────────────────────
        /// <summary>이어하기 진입 시 결과/승리 패널을 숨긴다.</summary>
        private void HideHudPanels()
        {
            _bossHud?.HideVictoryPanel();
            _bossHud?.HideResultPanel();
        }

        // ── 정리 유틸 ────────────────────────────────────────────────
        private void UnsubscribeBoss()
        {
            if (_currentBoss != null)
                _currentBoss.OnDeath -= HandleBossDeath;
            if (_currentBossPresenter != null)
            {
                _currentBossPresenter.OnPhaseChanged -= HandleBossPhaseChanged;
                _currentBossPresenter = null;
            }
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

        // ── 공개 세터 (에디터 Setup 툴 등 외부에서 HUD 주입용) ──────
        /// <summary>BossHudSetup 에디터 툴이 호출하는 HUD 주입 세터.</summary>
        public void SetBossHud(BossHudView hud)
        {
            _bossHud = hud;
        }
    }
}
