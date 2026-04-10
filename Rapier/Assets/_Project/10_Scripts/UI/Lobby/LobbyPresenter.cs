using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 로비 5탭 메인 Presenter.
    ///
    /// [역할]
    ///   - 5개 탭 전환 버튼 리스너 등록/해제 (OnEnable/OnDisable)
    ///   - 탭 전환 시 해당 패널 Show/Hide, 탭 Presenter OnTabShown/OnTabHidden 중재
    ///   - 마지막 선택 탭 상태 유지 (PlayerPrefs)
    ///   - 각 탭 Presenter 초기화
    ///
    /// [초기 진입 탭]
    ///   진입 시 기본 표시 탭은 메인(홈, 탭 3, 인덱스 2). PlayerPrefs로 마지막 탭 복원.
    ///
    /// ── 구독/이벤트 매핑 ─────────────────────────────────────────────────
    /// | 이벤트                        | 구독 위치   | 해제 위치   | 핸들러                    |
    /// |-------------------------------|------------|------------|--------------------------|
    /// | _tabView.TabButtons[0].onClick| OnEnable   | OnDisable  | HandleTabButtonClicked(0) |
    /// | _tabView.TabButtons[1].onClick| OnEnable   | OnDisable  | HandleTabButtonClicked(1) |
    /// | _tabView.TabButtons[2].onClick| OnEnable   | OnDisable  | HandleTabButtonClicked(2) |
    /// | _tabView.TabButtons[3].onClick| OnEnable   | OnDisable  | HandleTabButtonClicked(3) |
    /// | _tabView.TabButtons[4].onClick| OnEnable   | OnDisable  | HandleTabButtonClicked(4) |
    /// ─────────────────────────────────────────────────────────────────────
    /// 탭 Presenter 이벤트 위임:
    ///   ShowTab(index) 호출 시 → 이전 탭 Presenter.OnTabHidden(), 새 탭 Presenter.OnTabShown()
    /// </summary>
    public class LobbyPresenter : MonoBehaviour
    {
        [Header("Main Tab View")]
        [SerializeField] private LobbyTabView _tabView;

        [Header("Tab Presenters")]
        [SerializeField] private HomeTabPresenter      _homePresenter;
        [SerializeField] private CharacterTabPresenter _characterPresenter;
        [SerializeField] private SettingsTabPresenter  _settingsPresenter;

        [Header("Tab Views (assigned via Init or Inspector)")]
        [SerializeField] private HomeTabView      _homeTabView;
        [SerializeField] private CharacterTabView _characterTabView;
        [SerializeField] private ShopTabView      _shopTabView;
        [SerializeField] private MissionTabView   _missionTabView;
        [SerializeField] private SettingsTabView  _settingsTabView;

        // 마지막 선택 탭 저장 키
        private const string KEY_LAST_TAB = "Lobby_LastTab";

        // 현재 활성 탭 인덱스 (0-based), -1 = 초기값(탭 없음)
        private int _currentTabIndex = -1;

        // 탭 클릭 핸들러 캐싱 (람다 재생성 방지)
        private readonly UnityEngine.Events.UnityAction[] _tabHandlers = new UnityEngine.Events.UnityAction[5];

        // ── 공개 초기화 메서드 ────────────────────────────────────
        /// <summary>
        /// LobbyHudSetup(에디터 툴) 또는 LobbyManager가 호출하는 초기화 메서드.
        /// 모든 View와 Presenter 의존성을 주입한다.
        /// </summary>
        public void Init(
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
            _tabView            = tabView;
            _homeTabView        = homeTabView;
            _characterTabView   = characterTabView;
            _shopTabView        = shopTabView;
            _missionTabView     = missionTabView;
            _settingsTabView    = settingsTabView;
            _homePresenter      = homePresenter;
            _characterPresenter = characterPresenter;
            _settingsPresenter  = settingsPresenter;

            // 각 탭 Presenter 초기화
            _homePresenter?.Init(_homeTabView);
            _characterPresenter?.Init(_characterTabView);
            _settingsPresenter?.Init(_settingsTabView);
        }

        // ── Unity Lifecycle ───────────────────────────────────────
        private void Awake()
        {
            // 핸들러 캐싱 — 인덱스 캡처를 위해 로컬 변수 사용
            for (int i = 0; i < 5; i++)
            {
                int captured = i;
                _tabHandlers[i] = () => HandleTabButtonClicked(captured);
            }
        }

        private void OnEnable()
        {
            if (_tabView == null || _tabView.TabButtons == null) return;

            // 탭 버튼 리스너 등록
            for (int i = 0; i < _tabView.TabButtons.Length && i < 5; i++)
            {
                if (_tabView.TabButtons[i] != null)
                    _tabView.TabButtons[i].onClick.AddListener(_tabHandlers[i]);
            }

            // 마지막 탭 복원 (기본: 탭 3 메인, 인덱스 2)
            int savedTab = PlayerPrefs.GetInt(KEY_LAST_TAB, (int)LobbyTabIndex.Home - 1);
            ShowTab(savedTab);
        }

        private void OnDisable()
        {
            if (_tabView == null || _tabView.TabButtons == null) return;

            // 탭 버튼 리스너 해제
            for (int i = 0; i < _tabView.TabButtons.Length && i < 5; i++)
            {
                if (_tabView.TabButtons[i] != null)
                    _tabView.TabButtons[i].onClick.RemoveListener(_tabHandlers[i]);
            }

            // 현재 탭 숨김 처리
            NotifyTabHidden(_currentTabIndex);
            _currentTabIndex = -1;
        }

        // ── Private Methods ───────────────────────────────────────
        /// <summary>
        /// 지정한 탭(0-based 인덱스)을 활성화하고 나머지를 비활성화한다.
        /// </summary>
        private void ShowTab(int tabIndex)
        {
            // 이전 탭 숨김 처리
            if (_currentTabIndex >= 0 && _currentTabIndex != tabIndex)
                NotifyTabHidden(_currentTabIndex);

            _currentTabIndex = tabIndex;

            if (_tabView?.TabPanels != null)
            {
                for (int i = 0; i < _tabView.TabPanels.Length; i++)
                {
                    if (_tabView.TabPanels[i] != null)
                        _tabView.TabPanels[i].SetActive(i == tabIndex);
                }
            }

            _tabView?.HighlightTab(tabIndex);

            // 새 탭 Presenter 알림
            NotifyTabShown(tabIndex);

            // 마지막 탭 저장
            PlayerPrefs.SetInt(KEY_LAST_TAB, tabIndex);
        }

        /// <summary>해당 탭 Presenter에 OnTabShown을 알린다.</summary>
        private void NotifyTabShown(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: /* 상점 — B1 Presenter 없음 */ break;
                case 1: _characterPresenter?.OnTabShown(); break;
                case 2: _homePresenter?.OnTabShown();      break;
                case 3: /* 미션 — B1 Presenter 없음 */    break;
                case 4: _settingsPresenter?.OnTabShown();  break;
            }
        }

        /// <summary>해당 탭 Presenter에 OnTabHidden을 알린다.</summary>
        private void NotifyTabHidden(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: /* 상점 — B1 Presenter 없음 */ break;
                case 1: _characterPresenter?.OnTabHidden(); break;
                case 2: _homePresenter?.OnTabHidden();      break;
                case 3: /* 미션 — B1 Presenter 없음 */     break;
                case 4: _settingsPresenter?.OnTabHidden();  break;
            }
        }

        // ── Event Handlers ────────────────────────────────────────
        private void HandleTabButtonClicked(int tabIndex)
        {
            ShowTab(tabIndex);
        }
    }
}
