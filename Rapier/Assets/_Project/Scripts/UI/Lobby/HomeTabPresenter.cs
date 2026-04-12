using Game.Data.Save;
using UnityEngine;
using Game.Core;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 3 — 메인(홈) 탭 Presenter.
    ///
    /// [역할]
    ///   - 스테이지 번호 표시 (_selectedStage 기준)
    ///   - 화살표 버튼으로 선택 스테이지 변경
    ///   - "출격" 버튼 클릭 → SceneController.LoadGame(_selectedStage) 직접 호출
    ///
    /// ── 구독/이벤트 매핑 ─────────────────────────────────────────────────
    /// | 이벤트                              | 구독 위치   | 해제 위치    | 핸들러                       |
    /// |-------------------------------------|------------|-------------|------------------------------|
    /// | _view.EnterStageButton.onClick      | OnTabShown | OnTabHidden | HandleEnterStageClicked      |
    /// | _view.LeftArrowButton.onClick       | OnTabShown | OnTabHidden | HandleLeftArrowClicked       |
    /// | _view.RightArrowButton.onClick      | OnTabShown | OnTabHidden | HandleRightArrowClicked      |
    /// ─────────────────────────────────────────────────────────────────────
    /// </summary>
    public class HomeTabPresenter : MonoBehaviour
    {
        private HomeTabView  _view;
        private SaveManager  _saveManager;
        private int          _selectedStage;

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

            // 초기 선택 스테이지 = 가장 높은 도전 가능 스테이지
            _selectedStage = GetUnlockedStageCount();
            RefreshStageNumber();

            _view.EnterStageButton.onClick.AddListener(HandleEnterStageClicked);
            _view.LeftArrowButton.onClick.AddListener(HandleLeftArrowClicked);
            _view.RightArrowButton.onClick.AddListener(HandleRightArrowClicked);
        }

        /// <summary>탭이 숨겨질 때 LobbyPresenter가 호출한다. 리스너를 해제한다.</summary>
        public void OnTabHidden()
        {
            if (_view == null) return;
            _view.EnterStageButton.onClick.RemoveListener(HandleEnterStageClicked);
            _view.LeftArrowButton.onClick.RemoveListener(HandleLeftArrowClicked);
            _view.RightArrowButton.onClick.RemoveListener(HandleRightArrowClicked);
        }

        // ── Private Methods ───────────────────────────────────────
        private void RefreshStageNumber()
        {
            _view.SetStageNumber(_selectedStage);
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
            SceneController.LoadGame(_selectedStage);
        }

        private void HandleLeftArrowClicked()
        {
            _selectedStage = Mathf.Max(1, _selectedStage - 1);
            RefreshStageNumber();
        }

        private void HandleRightArrowClicked()
        {
            _selectedStage = Mathf.Min(GetUnlockedStageCount(), _selectedStage + 1);
            RefreshStageNumber();
        }
    }
}
