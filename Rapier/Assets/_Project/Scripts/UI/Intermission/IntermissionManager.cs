using UnityEngine;
using Game.Core.Stage;
using Game.Data.RunStats;
using Game.Core;

namespace Game.UI.Intermission
{
    /// <summary>
    /// 인터미션 흐름 관리자. INTERMISSION.md §3 본연 역할만 담당.
    ///
    /// [역할]
    ///   - ProgressionManager 로부터 Open() / ShowDeathPopup() 호출을 받는다.
    ///   - IntermissionView / DeathPopupView 수명 관리.
    ///   - 스탯 선택 완료 → 스탯 적용 + UI 닫기 (다음 방 전환은 포탈이 담당).
    ///   - 이어하기/로비 복귀 → StageManager.ContinueFromDeath() / ReturnToLobby() 연결.
    ///
    /// [역할에서 제거됨]
    ///   - 스테이지 클리어 처리는 StageClearManager 가 전담한다 (SRP 준수).
    ///   - OnStageCleared 구독 제거.
    ///   - StageClearView / Crystal 지급 / RecordStageClear 전부 StageClearManager 로 이전.
    ///
    /// [이벤트 구독 쌍]
    ///   OnEnable  : 모든 View 이벤트 구독
    ///   OnDisable : 구독 해제 (짝 보장)
    /// </summary>
    public class IntermissionManager : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private IntermissionView  _intermissionView;
        [SerializeField] private DeathPopupView    _deathPopupView;
        [SerializeField] private StageManager      _stageManagerRef;

        // ── 내부 상태 ────────────────────────────────────────────────
        private RunStatContainer _runStat;
        private StageManager     _stageManager;

        // ── 구독 관리 ────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_intermissionView != null)
                _intermissionView.OnStatSelected += HandleStatSelected;

            if (_deathPopupView != null)
            {
                _deathPopupView.OnContinueClicked      += HandleContinue;
                _deathPopupView.OnReturnToLobbyClicked += HandleReturnToLobby;
            }
        }

        private void OnDisable()
        {
            if (_intermissionView != null)
                _intermissionView.OnStatSelected -= HandleStatSelected;

            if (_deathPopupView != null)
            {
                _deathPopupView.OnContinueClicked      -= HandleContinue;
                _deathPopupView.OnReturnToLobbyClicked -= HandleReturnToLobby;
            }
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>
        /// 인터미션 방 진입 시 호출.
        /// IsContinueMode면 UI를 띄우지 않고 즉시 리턴 — 플레이어는 이미 부활했고 포탈만 세상에 있는 상태.
        /// 일반 모드는 카드 2장 표시.
        /// </summary>
        public void Open(RunStatContainer runStat, StageManager stageManager)
        {
            _runStat      = runStat;
            _stageManager = stageManager;

            // 이어하기 모드: UI 없이 포탈만 대기
            if (stageManager != null && stageManager.IsContinueMode)
            {
                Debug.Log("[IntermissionManager] 이어하기 인터미션 — UI 생략, 포탈 대기.");
                return;
            }

            if (_intermissionView != null)
            {
                var (first, second) = StatPickPool.PickTwo();
                _intermissionView.Show(first, second);
            }
            else
            {
                Debug.LogWarning("[IntermissionManager] IntermissionView 없음 — 카드 UI 생략.");
            }
        }

        /// <summary>
        /// 플레이어 사망 시 사망 팝업을 표시한다.
        /// </summary>
        public void ShowDeathPopup(StageManager stageManager)
        {
            _stageManager = stageManager;

            if (_deathPopupView != null)
                _deathPopupView.Show();
            else
            {
                Debug.LogWarning("[IntermissionManager] DeathPopupView 없음 — 자동 이어하기.");
                HandleContinue();
            }
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────────
        private void HandleStatSelected(RunStatEntry entry)
        {
            _runStat?.Apply(entry.Type, entry.Value);
            Debug.Log($"[IntermissionManager] 스탯 선택: {entry.DisplayName}\n" +
                      $"[RunStat 현황] {_runStat?.GetSummaryLog()}");

            // 스탯 적용 + UI 닫기까지만. 다음 방 전환은 플레이어가 포탈을 밟을 때 일어난다.
            _intermissionView?.Hide();
        }

        private void HandleContinue()
        {
            Debug.Log("[IntermissionManager] 이어하기 선택 — RunStat 유지.");
            _deathPopupView?.Hide();
            _stageManager?.ContinueFromDeath();
        }

        private void HandleReturnToLobby()
        {
            Debug.Log("[IntermissionManager] 로비 복귀 선택 — RunStat 초기화.");
            _deathPopupView?.Hide();
            _stageManager?.ReturnToLobby();
        }
    }
}
