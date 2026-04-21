using System.Collections.Generic;
using UnityEngine;
using Game.Core.Services;
using Game.Data.Save;
using Game.Data.Stage;
using Game.UI.Intermission;

namespace Game.Core.Stage
{
    /// <summary>
    /// 메인 스테이지 보스 처치 = 스테이지 클리어 시점에 호출되어
    /// (1) StageClearView 표시, (2) Save 에 클리어 기록, (3) Crystal 보상 지급,
    /// (4) 다음 스테이지 존재 여부 판단을 담당한다.
    ///
    /// IntermissionManager 에 혼재되어 있던 "스테이지 클리어" 책임을 분리.
    /// 인터미션은 INTERMISSION.md 의 별도 컨텐츠이므로 IntermissionManager 는 본연 역할(HP 회복 + 스탯 카드)만 유지.
    ///
    /// [이벤트 구독 쌍]
    ///   Start      : StageManager.OnStageCleared 구독 + StageClearView 이벤트 구독
    ///   OnDestroy  : 구독 해제 (짝 보장)
    ///
    /// [서비스 조회 타이밍]
    ///   Awake : ServiceLocator.Register 등록만.
    ///   Start : ServiceLocator.Get/TryGet 은 Start 이상에서 (Awake 순서 비결정성 방지).
    /// </summary>
    public class StageClearManager : MonoBehaviour
    {
        [SerializeField] private StageClearView _stageClearView;

        private StageManager    _stageManager;
        private SaveManager     _saveManager;
        private CurrencyService _currencyService;
        private StageDatabase   _stageDatabase;

        // ── Awake ─────────────────────────────────────────────────────
        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        // ── Start ─────────────────────────────────────────────────────
        private void Start()
        {
            // ServiceLocator 조회는 Start 이상에서 (Awake 순서 비결정성 방지 — feedback_servicelocator_awake_timing)
            _stageManager    = ServiceLocator.TryGet<StageManager>();
            _saveManager     = ServiceLocator.TryGet<SaveManager>();
            _currencyService = ServiceLocator.TryGet<CurrencyService>();
            _stageDatabase   = Resources.Load<StageDatabase>("StageDatabase");

            if (_stageManager != null)
                _stageManager.OnStageCleared += HandleStageCleared;

            // StageClearView 버튼 이벤트 구독 (씬 참조가 있을 때)
            if (_stageClearView != null)
            {
                _stageClearView.OnReturnToLobbyClicked += HandleClearReturnToLobby;
                _stageClearView.OnNextStageClicked     += HandleNextStage;
            }
        }

        // ── OnDestroy ─────────────────────────────────────────────────
        private void OnDestroy()
        {
            if (_stageManager != null)
                _stageManager.OnStageCleared -= HandleStageCleared;

            if (_stageClearView != null)
            {
                _stageClearView.OnReturnToLobbyClicked -= HandleClearReturnToLobby;
                _stageClearView.OnNextStageClicked     -= HandleNextStage;
            }

            ServiceLocator.Unregister<StageClearManager>();
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>다음 스테이지 존재 여부 (UI 버튼 활성화용).</summary>
        public bool HasNextStage()
        {
            if (_stageManager == null || _stageDatabase == null) return false;
            int next = _stageManager.CurrentStageIndex + 1;
            return _stageDatabase.HasStage(next);
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────────
        private void HandleStageCleared()
        {
            int clearedIndex = _stageManager != null ? _stageManager.CurrentStageIndex : 0;

            Debug.Log($"[StageClearManager] 스테이지 {clearedIndex} 클리어 처리 시작.");

            // (1) StageClearView 표시 (ProgressionManager 의 RunDrops 전달)
            var pm = ServiceLocator.TryGet<ProgressionManager>();
            _stageClearView?.Show(pm?.RunDrops);

            if (clearedIndex > 0)
            {
                // (2) Save 에 클리어 기록
                _saveManager?.RecordStageClear(clearedIndex);

                // (3) Crystal 보상 지급 (BALANCE §7-2)
                int reward = StageClearRewards.GetCrystal(clearedIndex);
                _currencyService?.AddCrystal(reward);
                Debug.Log($"[StageClearManager] 스테이지 {clearedIndex} 클리어 → Crystal +{reward}");
            }
        }

        private void HandleClearReturnToLobby()
        {
            Debug.Log("[StageClearManager] 클리어 후 로비 복귀.");
            _stageClearView?.Hide();
            SceneController.LoadLobby();
        }

        private void HandleNextStage()
        {
            Debug.Log("[StageClearManager] 다음 스테이지 진입.");
            _stageClearView?.Hide();

            int currentIndex = _stageManager != null ? _stageManager.CurrentStageIndex : 0;
            int nextIndex    = currentIndex + 1;

            if (_stageDatabase == null || !_stageDatabase.HasStage(nextIndex))
            {
                Debug.Log($"[StageClearManager] 스테이지 {nextIndex} 없음 — 로비 복귀.");
                SceneController.LoadLobby();
                return;
            }

            Debug.Log($"[StageClearManager] 스테이지 {nextIndex} 로드.");
            SceneController.LoadGame(nextIndex);
        }
    }
}
