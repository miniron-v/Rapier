using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 분해 완료 결과 모달 View.
    /// sortingOrder 400, 전체화면 불투명 배경.
    /// 타이틀 "분해 완료" + 본문 "획득: 강화의 가루 ×N" + [닫기] 버튼.
    /// 로직 없음 — 표시만 담당.
    /// </summary>
    public class DismantleResultModalView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private TextMeshProUGUI _bodyText;
        [SerializeField] private Button          _closeButton;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>[닫기] 버튼 클릭 이벤트.</summary>
        public event Action OnCloseClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>런타임 생성 시 SerializeField 참조를 주입한다 (LobbyHudSetup 호출).</summary>
        public void InitReferences(TextMeshProUGUI bodyText, Button closeButton)
        {
            _bodyText    = bodyText;
            _closeButton = closeButton;
            if (_closeButton != null)
                _closeButton.onClick.AddListener(HandleCloseClicked);
        }

        // ── Public 메서드 (Presenter → View) ────────────────────────────────

        /// <summary>본문 텍스트 설정.</summary>
        public void SetBody(int dustAmount)
        {
            if (_bodyText != null)
                _bodyText.text = $"획득: 강화의 가루 ×{dustAmount}";
        }

        /// <summary>모달 표시/숨김.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleCloseClicked() => OnCloseClicked?.Invoke();
    }
}
