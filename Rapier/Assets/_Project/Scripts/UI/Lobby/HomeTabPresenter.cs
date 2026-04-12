using Game.Data.Save;
using UnityEngine;
using Game.Core;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 3 — 메인(홈) 탭 Presenter.
    ///
    /// [역할]
    ///   - 스테이지 번호 표시 (SaveManager.Current.highestClearedStage + 1 기준)
    ///   - "출격" 버튼 클릭 → StageSelectView 표시
    ///   - 스테이지 선택 → SceneController.LoadGame(stageIndex) 호출
    ///
    /// [Phase 17 변경]
    ///   - 출격 버튼 → 스테이지 선택 패널 삽입.
    ///   - StageSelectView 참조 선택 사항: null이면 스테이지 1로 직접 진입 (폴백).
    ///
    /// ── 구독/이벤트 매핑 ─────────────────────────────────────────────────
    /// | 이벤트                              | 구독 위치   | 해제 위치    | 핸들러                       |
    /// |-------------------------------------|------------|-------------|------------------------------|
    /// | _view.EnterStageButton.onClick      | OnTabShown | OnTabHidden | HandleEnterStageClicked      |
    /// | _stageSelectView.OnStageSelected    | OnTabShown | OnTabHidden | HandleStageSelected          |
    /// | _stageSelectView.OnCloseClicked     | OnTabShown | OnTabHidden | HandleStageSelectClosed      |
    /// ─────────────────────────────────────────────────────────────────────
    /// </summary>
    public class HomeTabPresenter : MonoBehaviour
    {
        [Header("스테이지 선택 패널 (없으면 스테이지 1 직접 진입)")]
        [SerializeField] private StageSelectView _stageSelectView;

        private HomeTabView  _view;
        private SaveManager  _saveManager;

        /// <summary>LobbyPresenter가 초기화 시 호출한다.</summary>
        public void Init(HomeTabView view, SaveManager saveManager)
        {
            _view        = view;
            _saveManager = saveManager;

            if (_saveManager == null)
                Debug.LogWarning("[HomeTabPresenter] SaveManager 미주입 — " +
                                 "GameBootstrap 가 Lobby 진입 전에 실행되었는지 확인할 것. " +
                                 "폴백으로 기본 표시.");
        }

        // ── 탭 전환 진입점 (LobbyPresenter가 호출) ───────────────
        /// <summary>탭이 표시될 때 LobbyPresenter가 호출한다. 리스너를 등록한다.</summary>
        public void OnTabShown()
        {
            if (_view == null) return;
            _view.EnterStageButton.onClick.AddListener(HandleEnterStageClicked);
            RefreshStageNumber();

            if (_stageSelectView != null)
            {
                _stageSelectView.OnStageSelected += HandleStageSelected;
                _stageSelectView.OnCloseClicked  += HandleStageSelectClosed;
            }
        }

        /// <summary>탭이 숨겨질 때 LobbyPresenter가 호출한다. 리스너를 해제한다.</summary>
        public void OnTabHidden()
        {
            if (_view == null) return;
            _view.EnterStageButton.onClick.RemoveListener(HandleEnterStageClicked);

            if (_stageSelectView != null)
            {
                _stageSelectView.OnStageSelected -= HandleStageSelected;
                _stageSelectView.OnCloseClicked  -= HandleStageSelectClosed;
                _stageSelectView.Hide();
            }
        }

        // ── Private Methods ───────────────────────────────────────
        private void RefreshStageNumber()
        {
            // Phase 17: highestClearedStage 기반 스테이지 번호 표시.
            int highestCleared = _saveManager != null ? _saveManager.Current.highestClearedStage : 0;
            // 표시 번호 = 도달 최고 스테이지 + 1 (다음 도전 스테이지)
            int displayStage = highestCleared + 1;
            _view.SetStageNumber(displayStage);
        }

        private int GetUnlockedStageCount()
        {
            int highestCleared = _saveManager != null ? _saveManager.Current.highestClearedStage : 0;
            // 최고 클리어 + 1 = 도전 가능한 최대 스테이지 (최소 1)
            return Mathf.Max(1, highestCleared + 1);
        }

        // ── Event Handlers ────────────────────────────────────────
        private void HandleEnterStageClicked()
        {
            if (_stageSelectView != null)
            {
                int unlocked = GetUnlockedStageCount();
                _stageSelectView.Show(unlocked);
            }
            else
            {
                // StageSelectView 없으면 스테이지 1로 직접 진입 (폴백)
                SceneController.LoadGame(1);
            }
        }

        private void HandleStageSelected(int stageIndex)
        {
            _stageSelectView?.Hide();
            SceneController.LoadGame(stageIndex);
        }

        private void HandleStageSelectClosed()
        {
            _stageSelectView?.Hide();
        }
    }
}
