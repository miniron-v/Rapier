using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 3 — 메인(홈) 패널 View.
    ///
    /// [표시 요소]
    ///   - 현재 스테이지 번호 (TMP 텍스트)
    ///   - "스테이지 진입" 버튼
    ///   - 우편함 아이콘 플레이스홀더
    ///
    /// View는 UI 표시만 담당한다.
    /// 스테이지 진입 버튼 클릭 시 HomeTabPresenter를 통해 SceneController를 호출한다.
    /// </summary>
    public class HomeTabView : LobbyTabViewBase
    {
        [Header("Home UI")]
        [SerializeField] private TMP_Text    _stageNumberText;
        [SerializeField] private Button      _enterStageButton;
        [SerializeField] private GameObject  _mailboxIconPlaceholder;

        /// <summary>스테이지 진입 버튼. HomeTabPresenter가 리스너를 등록한다.</summary>
        public Button EnterStageButton => _enterStageButton;

        /// <summary>
        /// HomeTabPresenter가 초기화 시 호출한다.
        /// </summary>
        public void Init(TMP_Text stageNumberText, Button enterStageButton, GameObject mailboxIcon)
        {
            _stageNumberText        = stageNumberText;
            _enterStageButton       = enterStageButton;
            _mailboxIconPlaceholder = mailboxIcon;
        }

        /// <summary>현재 스테이지 번호를 화면에 표시한다.</summary>
        public void SetStageNumber(int stageNumber)
        {
            if (_stageNumberText != null)
                _stageNumberText.text = $"Stage {stageNumber}";
        }
    }
}
