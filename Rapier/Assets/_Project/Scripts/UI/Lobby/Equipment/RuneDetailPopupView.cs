using System;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 룬 상세 팝업 View.
    /// 룬 이름, 아이콘, 효과 설명, 장착/닫기 버튼을 표시한다.
    /// 로직 없음 — 표시 및 이벤트 발행만 담당. (MVP View 규칙)
    /// </summary>
    public class RuneDetailPopupView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private TextMeshProUGUI _runeNameText;
        [SerializeField] private Image           _runeIcon;
        [SerializeField] private TextMeshProUGUI _effectText;
        [SerializeField] private Button          _equipButton;
        [SerializeField] private TextMeshProUGUI _equipButtonText;
        [SerializeField] private Button          _closeButton;

        // ── 이벤트 (View → Presenter) ────────────────────────────────────────

        /// <summary>장착 버튼 클릭</summary>
        public event Action OnEquipClicked;

        /// <summary>닫기 버튼 클릭</summary>
        public event Action OnCloseClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _equipButton?.onClick.AddListener(() => OnEquipClicked?.Invoke());
            _closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _equipButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.RemoveAllListeners();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 참조를 주입한다.</summary>
        public void InitReferences(
            TextMeshProUGUI runeNameText,
            Image           runeIcon,
            TextMeshProUGUI effectText,
            Button          equipButton,
            TextMeshProUGUI equipButtonText,
            Button          closeButton)
        {
            _runeNameText    = runeNameText;
            _runeIcon        = runeIcon;
            _effectText      = effectText;
            _equipButton     = equipButton;
            _equipButtonText = equipButtonText;
            _closeButton     = closeButton;

            _equipButton?.onClick.RemoveAllListeners();
            _equipButton?.onClick.AddListener(() => OnEquipClicked?.Invoke());
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        // ── Public 메서드 (Presenter → View) ─────────────────────────────────

        /// <summary>팝업을 표시하거나 숨긴다.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        /// <summary>룬 상세 정보를 표시한다.</summary>
        public void SetData(RuneItemData rune)
        {
            if (rune == null) return;

            if (_runeNameText != null) _runeNameText.text = rune.RuneName;
            if (_effectText   != null) _effectText.text   = rune.EffectDescription;

            if (_runeIcon != null)
            {
                if (rune.Icon != null)
                {
                    _runeIcon.sprite = rune.Icon;
                    _runeIcon.color  = Color.white;
                }
                else
                {
                    _runeIcon.color = Color.cyan;
                }
            }
        }

        /// <summary>장착 버튼 라벨과 활성 여부를 설정한다.</summary>
        public void SetEquipButtonState(string label, bool interactable)
        {
            if (_equipButtonText != null) _equipButtonText.text = label;
            if (_equipButton     != null) _equipButton.interactable = interactable;
        }
    }
}
