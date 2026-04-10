using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 로비 하단 탭 바 View.
    ///
    /// [역할]
    ///   - 5개의 탭 전환 버튼 소유
    ///   - 활성 탭 강조 표시 (activeColor / normalColor)
    ///   - 탭 패널 루트(5개) 소유
    ///
    /// [계약]
    ///   LobbyPresenter가 이 View의 버튼 리스너를 OnEnable/OnDisable에서 등록/해제한다.
    ///   View 자체에서는 탭 전환 로직을 처리하지 않는다.
    /// </summary>
    public class LobbyTabView : MonoBehaviour
    {
        [Header("Tab Buttons (1=상점 2=캐릭터 3=메인 4=미션 5=설정)")]
        [SerializeField] private Button[] _tabButtons;   // 길이 5, 인덱스 0=탭1

        [Header("Tab Panels")]
        [SerializeField] private GameObject[] _tabPanels; // 길이 5, 인덱스 0=탭1

        [Header("Tab Highlight Colors")]
        [SerializeField] private Color _activeColor  = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color _normalColor  = new Color(0.7f, 0.7f, 0.7f);

        // ── 프로퍼티 ──────────────────────────────────────────────
        /// <summary>탭 버튼 배열 (인덱스 0 = 탭 1). LobbyPresenter가 리스너를 등록한다.</summary>
        public Button[] TabButtons  => _tabButtons;

        /// <summary>탭 패널 배열 (인덱스 0 = 탭 1). LobbyPresenter가 Show/Hide를 제어한다.</summary>
        public GameObject[] TabPanels => _tabPanels;

        /// <summary>
        /// LobbyHudSetup(에디터 툴)이 호출하는 초기화 메서드.
        /// </summary>
        public void Init(Button[] tabButtons, GameObject[] tabPanels)
        {
            _tabButtons  = tabButtons;
            _tabPanels   = tabPanels;
        }

        /// <summary>
        /// 활성 탭 버튼을 강조하고 나머지는 일반 색으로 되돌린다.
        /// activeIndex: 0-based (0 = 탭1).
        /// View는 색상 변경만 담당한다.
        /// </summary>
        public void HighlightTab(int activeIndex)
        {
            if (_tabButtons == null) return;

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                var image = _tabButtons[i].GetComponent<Image>();
                if (image != null)
                    image.color = (i == activeIndex) ? _activeColor : _normalColor;
            }
        }
    }
}
