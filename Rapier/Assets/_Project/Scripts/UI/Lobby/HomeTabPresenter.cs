using Game.Data.Save;
using UnityEngine;
using Game.Core;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 3 — 메인(홈) 탭 Presenter.
    ///
    /// [역할]
    ///   - 스테이지 번호 표시 (SaveManager.Current.highestStage + 1 기준)
    ///   - "스테이지 진입" 버튼 클릭 → SceneController.LoadStageDemo() 호출
    ///
    /// [Phase 13-B 변경]
    ///   - PlayerPrefs("Progress_CurrentStage") 레거시 제거.
    ///   - 스테이지 번호 원천: SaveManager.Current.highestStage (SaveMigrator v0→v1 이 PlayerPrefs 흡수).
    ///
    /// [이벤트 구독/해제]
    ///   OnTabShown (LobbyPresenter가 탭 활성화 시 호출): 버튼 리스너 등록
    ///   OnTabHidden (LobbyPresenter가 탭 비활성화 시 호출): 버튼 리스너 해제
    ///
    /// ── 구독/이벤트 매핑 ────────────────────────────────────��───────────
    /// | 이벤트                         | 구독 위치    | 해제 위치     | 핸들러                   |
    /// |--------------------------------|-------------|--------------|--------------------------|
    /// | _view.EnterStageButton.onClick | OnTabShown  | OnTabHidden  | HandleEnterStageClicked  |
    /// ─────────────────────────────────────────────────────────────────────
    /// </summary>
    public class HomeTabPresenter : MonoBehaviour
    {
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
        }

        /// <summary>탭이 숨겨질 때 LobbyPresenter가 호출한다. 리스너를 해제한다.</summary>
        public void OnTabHidden()
        {
            if (_view == null) return;
            _view.EnterStageButton.onClick.RemoveListener(HandleEnterStageClicked);
        }

        // ── Private Methods ───────────────────────────────────────
        private void RefreshStageNumber()
        {
            // Phase 13-B: SaveManager 기반 스테이지 번호 표시.
            // PlayerPrefs 레거시는 SaveMigrator v0→v1 에서 흡수되므로 여기서 접근하지 않는다.
            int highestStage = _saveManager != null ? _saveManager.Current.highestStage : 0;
            // 표시 번호 = 도달 최고 스테이지 + 1 (다음 도전 스테이지)
            int displayStage = highestStage + 1;
            _view.SetStageNumber(displayStage);
        }

        // ── Event Handlers ────────────────────────────────────────
        private void HandleEnterStageClicked()
        {
            SceneController.LoadStageDemo();
        }
    }
}
