using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.UI.Lobby;

namespace Game.UI
{
    /// <summary>
    /// 로비 씬 진입점.
    ///
    /// [역할]
    ///   - LobbyPresenter를 보유하며 5탭 로비를 초기화한다.
    ///   - LobbyHudSetup(에디터 툴)이 Init()을 통해 모든 의존성을 주입한다.
    ///
    /// [Phase 12-B1]
    ///   5탭 UI 뼈대 구현.
    ///   장비 실구현(B2), 미션·저장(B3)은 후속 에이전트가 채운다.
    ///
    /// [초기화 흐름]
    ///   LobbyHudSetup → Init() → LobbyPresenter.Init() → 각 탭 Presenter.Init()
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] private LobbyPresenter _lobbyPresenter;

        // ── 에디터 Setup 진입점 ───────────────────────────────────
        /// <summary>
        /// LobbyHudSetup이 호출하는 공개 초기화 메서드.
        /// LobbyPresenter와 모든 탭 View/Presenter 의존성을 주입한다.
        /// </summary>
        public void Init(
            LobbyPresenter   lobbyPresenter,
            LobbyTabView     tabView,
            HomeTabView      homeTabView,
            CharacterTabView characterTabView,
            ShopTabView      shopTabView,
            MissionTabView   missionTabView,
            SettingsTabView  settingsTabView,
            HomeTabPresenter      homePresenter,
            CharacterTabPresenter characterPresenter,
            SettingsTabPresenter  settingsPresenter)
        {
            _lobbyPresenter = lobbyPresenter;
            _lobbyPresenter.Init(
                tabView,
                homeTabView,
                characterTabView,
                shopTabView,
                missionTabView,
                settingsTabView,
                homePresenter,
                characterPresenter,
                settingsPresenter);
        }
    }
}
