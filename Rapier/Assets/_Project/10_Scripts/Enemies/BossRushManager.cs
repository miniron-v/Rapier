using System.Collections;
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
    ///   보스 사망 → BossRushHudView에 승리 패널 표시
    ///   "다음 스테이지" 버튼 → SpawnNextBoss()
    ///   모든 보스 처치 → ShowResult(true) [ALL CLEAR]
    ///   플레이어 사망  → ShowResult(false) [GAME OVER]
    ///
    /// [설정]
    ///   bossPrefabs  : 보스 프리팹 배열 (순서대로 등장)
    ///   bossStatDatas: 각 보스에 대응하는 BossStatData 배열
    ///   spawnPosition: 보스 스폰 위치
    /// </summary>
    public class BossRushManager : MonoBehaviour
    {
        [Header("보스 설정")]
        [SerializeField] private GameObject[]   _bossPrefabs;
        [SerializeField] private BossStatData[] _bossStatDatas;
        [SerializeField] private Vector2        _spawnPosition = Vector2.zero;

        [Header("참조")]
        [SerializeField] private BossRushHudView _hudView;

        // ── 내부 상태 ─────────────────────────────────────────────
        private int               _currentStageIndex = -1;
        private BossPresenterBase _currentBoss;
        private bool              _isBossAlive;
        private bool              _isGameOver;

        public int CurrentStage => _currentStageIndex + 1;
        public int TotalStages  => _bossPrefabs != null ? _bossPrefabs.Length : 0;

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
            ServiceLocator.Unregister<BossRushManager>();
        }

        // ── 외부 API ──────────────────────────────────────────────
        /// <summary>현재 살아있는 보스 반환. 없으면 null.</summary>
        public BossPresenterBase GetCurrentBoss()
        {
            if (_currentBoss != null && _currentBoss.IsAlive)
                return _currentBoss;
            return null;
        }

/// <summary>BossRushHudSetup이 호출하는 HudView 주입 메서드.</summary>
        public void InitHudView(BossRushHudView hudView)
        {
            _hudView = hudView;
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

            if (_currentBoss != null)
            {
                Destroy(_currentBoss.gameObject);
                _currentBoss = null;
            }

            StartCoroutine(SpawnBossRoutine(_currentStageIndex));
        }

        // ── 플레이어 사망 감지 ────────────────────────────────────
        private void SubscribePlayerDeath()
        {
            // CharacterPresenterBase는 ServiceLocator 비등록 타입 → FindObjectOfType 사용
            var player = FindObjectOfType<CharacterPresenterBase>();
            if (player == null)
            {
                Debug.LogWarning("[BossRushManager] CharacterPresenterBase를 찾을 수 없음. 사망 이벤트 구독 불가.");
                return;
            }
            player.OnPlayerDeath += HandlePlayerDeath;
            Debug.Log("[BossRushManager] 플레이어 사망 이벤트 구독 완료.");
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

            var go = Instantiate(prefab, _spawnPosition, Quaternion.identity);
            _currentBoss = go.GetComponent<BossPresenterBase>();

            if (_currentBoss == null)
            {
                Debug.LogError($"[BossRushManager] {prefab.name}에 BossPresenterBase가 없음!");
                yield break;
            }

            if (statData != null)
                _currentBoss.Spawn(statData, _spawnPosition);

            _currentBoss.OnDeath += HandleBossDeath;

            if (_currentBoss is BossPresenterBase bossBase)
                bossBase.OnPhaseChanged += phase => _hudView?.UpdatePhase(phase);

            _isBossAlive = true;

            _hudView?.SetupBoss(
                statData?.enemyName ?? prefab.name,
                _currentBoss,
                CurrentStage,
                TotalStages
            );

            Debug.Log($"[BossRushManager] Stage {CurrentStage}/{TotalStages} — {statData?.enemyName ?? prefab.name} 등장!");
        }

        // ── 보스 사망 처리 ────────────────────────────────────────
        private void HandleBossDeath()
        {
            if (!_isBossAlive) return;
            _isBossAlive = false;

            bool isFinalBoss = (_currentStageIndex >= TotalStages - 1);
            Debug.Log($"[BossRushManager] 보스 처치! 최종: {isFinalBoss}");

            if (isFinalBoss)
                _hudView?.ShowResult(true);
            else
                _hudView?.ShowVictoryPanel(CurrentStage, TotalStages);
        }
    }
}
