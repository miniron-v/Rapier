using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.UI;
using Game.Characters;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 러시 진행 관리자.
    ///
    /// [흐름]
    ///   Start → 첫 보스 스폰 + 플레이어 사망 구독
    ///   보스(군) 전원 사망 → BossHudView에 승리 패널 표시
    ///   "다음 스테이지" 버튼 → SpawnNextBoss() (BossHudView.OnNextStageRequested 구독)
    ///   모든 보스 처치 → ShowResult(true) [ALL CLEAR]
    ///   플레이어 사망  → ShowResult(false) [GAME OVER]
    ///
    /// [다중 스폰]
    ///   BossStatData.SpawnCount > 1 인 보스는 spawnOffsets 만큼 위치를 달리해
    ///   여러 인스턴스를 동시 스폰한다. 스테이지 클리어 판정은 "전원 사망".
    ///   IMultiBossSibling 구현체에는 스폰 직후 SetSiblings() 를 호출한다.
    ///
    /// [설정]
    ///   bossPrefabs  : 보스 프리팹 배열 (순서대로 등장)
    ///   bossStatDatas: 각 보스에 대응하는 BossStatData 배열
    ///   spawnPosition: 보스 스폰 기준 위치
    ///
    /// [이벤트 구독/해제 짝]
    ///   플레이어 OnPlayerDeath          : SubscribePlayerDeath (Start) / UnsubscribePlayerDeath (OnDestroy)
    ///   보스    OnDeath                 : SpawnBossRoutine (스폰 시) / ClearActiveBossInstances (다음 스폰 직전/OnDestroy)
    ///   보스    OnPhaseChanged          : SpawnBossRoutine (스폰 시) / ClearActiveBossInstances (다음 스폰 직전/OnDestroy)
    ///   HUD    OnNextStageRequested    : InitHudView (+= SpawnNextBoss) / InitHudView 재호출 or OnDestroy (-= SpawnNextBoss)
    /// </summary>
    public class BossRushManager : MonoBehaviour
    {
        [Header("보스 설정")]
        [SerializeField] private GameObject[]   _bossPrefabs;
        [SerializeField] private BossStatData[] _bossStatDatas;
        [SerializeField] private Vector2        _spawnPosition = Vector2.zero;

        [Header("참조")]
        [SerializeField] private BossHudView _hudView;

        // ── 내부 상태 ─────────────────────────────────────────────
        private int                          _currentStageIndex  = -1;
        private readonly List<BossPresenterBase> _activeBossInstances = new List<BossPresenterBase>();
        private int                          _aliveCount;
        private bool                         _isGameOver;

        /// <summary>편의 프로퍼티 — 단일 보스 접근 (HUD 등 레거시 연결용).</summary>
        private BossPresenterBase CurrentBoss =>
            _activeBossInstances.Count > 0 ? _activeBossInstances[0] : null;

        public int CurrentStage => _currentStageIndex + 1;
        public int TotalStages  => _bossPrefabs != null ? _bossPrefabs.Length : 0;

        // ── 플레이어 사망 구독 해제용 캐시 ───────────────────────
        private CharacterPresenterBase _subscribedPlayer;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void Start()
        {
            SubscribePlayerDeath();
            SpawnNextBoss();
        }

        private void OnDestroy()
        {
            ClearActiveBossInstances();
            UnsubscribePlayerDeath();
            if (_hudView != null)
                _hudView.OnNextStageRequested -= SpawnNextBoss;
            ServiceLocator.Unregister<BossRushManager>();
        }

        // ── 외부 API ──────────────────────────────────────────────
        /// <summary>현재 살아있는 보스 첫 번째 인스턴스 반환. 없으면 null.</summary>
        public BossPresenterBase GetCurrentBoss()
        {
            foreach (var b in _activeBossInstances)
            {
                if (b != null && b.IsAlive)
                    return b;
            }
            return null;
        }

        /// <summary>BossHudSetup이 호출하는 HudView 주입 메서드.</summary>
        public void InitHudView(BossHudView hudView)
        {
            // 기존 구독 해제 (중복 방지)
            if (_hudView != null)
                _hudView.OnNextStageRequested -= SpawnNextBoss;

            _hudView = hudView;

            if (_hudView != null)
                _hudView.OnNextStageRequested += SpawnNextBoss;
        }

        /// <summary>"다음 스테이지" 버튼에서 호출.</summary>
        public void SpawnNextBoss()
        {
            _currentStageIndex++;

            if (_bossPrefabs == null || _currentStageIndex >= _bossPrefabs.Length)
            {
                _hudView?.ShowResult(true);
                Debug.Log("[BossRushManager] 모든 보스 처치! ALL CLEAR!");
                return;
            }

            // 이전 스테이지 인스턴스 정리
            ClearActiveBossInstances();

            StartCoroutine(SpawnBossRoutine(_currentStageIndex));
        }

        // ── 플레이어 사망 감지 ────────────────────────────────────
        private void SubscribePlayerDeath()
        {
            _subscribedPlayer = FindObjectOfType<CharacterPresenterBase>();
            if (_subscribedPlayer == null)
            {
                Debug.LogWarning("[BossRushManager] CharacterPresenterBase를 찾을 수 없음. 사망 이벤트 구독 불가.");
                return;
            }
            _subscribedPlayer.OnPlayerDeath += HandlePlayerDeath;
            Debug.Log("[BossRushManager] 플레이어 사망 이벤트 구독 완료.");
        }

        private void UnsubscribePlayerDeath()
        {
            if (_subscribedPlayer != null)
            {
                _subscribedPlayer.OnPlayerDeath -= HandlePlayerDeath;
                _subscribedPlayer = null;
            }
        }

        private void HandlePlayerDeath()
        {
            if (_isGameOver) return;
            _isGameOver = true;
            Debug.Log("[BossRushManager] 플레이어 사망 → GAME OVER");
            _hudView?.ShowResult(false);
        }

        // ── 스폰 ──────────────────────────────────────────────────
        private IEnumerator SpawnBossRoutine(int index)
        {
            _hudView?.HideVictoryPanel();

            yield return new WaitForSeconds(0.5f);

            var prefab   = _bossPrefabs[index];
            var statData = _bossStatDatas != null && index < _bossStatDatas.Length
                ? _bossStatDatas[index]
                : null;

            // ── 다중 스폰 파라미터 결정 ─────────────────────────
            int     spawnCount   = statData != null ? statData.SpawnCount : 1;
            IReadOnlyList<Vector2> spawnOffsets = statData?.SpawnOffsets;

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 offset   = (spawnOffsets != null && i < spawnOffsets.Count)
                    ? spawnOffsets[i]
                    : Vector2.zero;
                Vector2 spawnPos = _spawnPosition + offset;

                var go   = Instantiate(prefab, spawnPos, Quaternion.identity);
                var boss = go.GetComponent<BossPresenterBase>();

                if (boss == null)
                {
                    Debug.LogError($"[BossRushManager] {prefab.name}에 BossPresenterBase가 없음! (인스턴스 {i})");
                    Destroy(go);
                    continue;
                }

                if (statData != null)
                    boss.Spawn(statData, spawnPos);

                boss.OnDeath        += HandleBossDeath;
                boss.OnPhaseChanged += HandleBossPhaseChanged;

                _activeBossInstances.Add(boss);
            }

            if (_activeBossInstances.Count == 0)
            {
                Debug.LogError($"[BossRushManager] Stage {CurrentStage} — 유효한 보스 인스턴스 없음!");
                yield break;
            }

            _aliveCount = _activeBossInstances.Count;

            // ── IMultiBossSibling 주입 ─────────────────────────
            bool hasSibling = false;
            foreach (var b in _activeBossInstances)
            {
                if (b is IMultiBossSibling) { hasSibling = true; break; }
            }
            if (hasSibling)
            {
                var readonlyList = _activeBossInstances.AsReadOnly();
                foreach (var b in _activeBossInstances)
                {
                    if (b is IMultiBossSibling sibling)
                        sibling.SetSiblings(readonlyList);
                }
            }

            // ── HUD 연결 (첫 번째 인스턴스 기준) ─────────────────
            _hudView?.SetupBoss(
                statData?.enemyName ?? prefab.name,
                CurrentBoss,
                CurrentStage,
                TotalStages
            );

            Debug.Log($"[BossRushManager] Stage {CurrentStage}/{TotalStages} — " +
                      $"{statData?.enemyName ?? prefab.name} ×{_activeBossInstances.Count} 등장!");
        }

        // ── 페이즈 변경 핸들러 ────────────────────────────────────
        private void HandleBossPhaseChanged(BossPresenterBase.BossPhase phase)
        {
            _hudView?.UpdatePhase(phase);
        }

        // ── 보스 사망 처리 ────────────────────────────────────────
        private void HandleBossDeath()
        {
            _aliveCount--;
            Debug.Log($"[BossRushManager] 보스 1체 처치. 남은 생존 카운트: {_aliveCount}");

            if (_aliveCount > 0) return; // 아직 살아있는 인스턴스 있음

            // 전원 사망
            bool isFinalBoss = (_currentStageIndex >= TotalStages - 1);
            Debug.Log($"[BossRushManager] 스테이지 보스 전원 처치! 최종: {isFinalBoss}");

            if (isFinalBoss)
                _hudView?.ShowResult(true);
            else
                _hudView?.ShowVictoryPanel(CurrentStage, TotalStages);
        }

        // ── 활성 인스턴스 정리 (이벤트 해제 포함) ─────────────────
        private void ClearActiveBossInstances()
        {
            foreach (var boss in _activeBossInstances)
            {
                if (boss == null) continue;
                // 이벤트 구독 해제
                boss.OnDeath        -= HandleBossDeath;
                boss.OnPhaseChanged -= HandleBossPhaseChanged;
                Destroy(boss.gameObject);
            }
            _activeBossInstances.Clear();
            _aliveCount = 0;
        }
    }
}
