using UnityEngine;
using Game.Core;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 3 — 메인(홈) 탭 Presenter.
    ///
    /// [역할]
    ///   - 스테이지 번호 표시 (현재는 PlayerPrefs에서 읽음)
    ///   - "스테이지 진입" 버튼 클릭 → SceneController.LoadGame() 호출
    ///
    /// [이벤트 구독/해제]
    ///   OnTabShown (LobbyPresenter가 탭 활성화 시 호출): 버튼 리스너 등록
    ///   OnTabHidden (LobbyPresenter가 탭 비활성화 시 호출): 버튼 리스너 해제
    ///
    /// ── 구독/이벤트 매핑 ────────────────────────────────────────────────
    /// | 이벤트                      | 구독 위치    | 해제 위치     | 핸들러                      |
    /// |-----------------------------|-------------|--------------|----------------------------|
    /// | _view.EnterStageButton.onClick | OnTabShown  | OnTabHidden  | HandleEnterStageClicked    |
    /// ─────────────────────────────────────────────────────────────────────
    /// </summary>
    public class HomeTabPresenter : MonoBehaviour
    {
        private HomeTabView _view;
        private const string KEY_STAGE = "Progress_CurrentStage";

        /// <summary>LobbyPresenter가 초기화 시 호출한다.</summary>
        public void Init(HomeTabView view)
        {
            _view = view;
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
            int stage = PlayerPrefs.GetInt(KEY_STAGE, 1);
            _view.SetStageNumber(stage);
        }

        // ── Event Handlers ────────────────────────────────────────
        private void HandleEnterStageClicked()
        {
            SceneController.LoadStageDemo();
        }
    }
}
