using System;
using System.Collections.Generic;
using Game.Data.Equipment;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 단일 장비 슬롯 UI. 등급 색상 테두리 + 룬 소켓 아이콘을 표시한다.
    /// 로직 없음 — 표시만 담당. (MVP View 규칙)
    /// </summary>
    public class EquipmentSlotView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private Image _itemIcon;
        [SerializeField] private Image _gradeBorder;
        [SerializeField] private Image _emptyIcon;
        [SerializeField] private List<Image> _runeSocketIcons = new();
        [SerializeField] private Button _slotButton;

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentSlotType _slotType;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>슬롯 버튼 클릭 이벤트 (슬롯 타입 전달)</summary>
        public event Action<EquipmentSlotType> OnClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _slotButton.onClick.AddListener(HandleButtonClicked);
        }

        private void OnDestroy()
        {
            _slotButton.onClick.RemoveListener(HandleButtonClicked);
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// 런타임 생성 시 SerializeField 참조를 외부에서 주입한다 (LobbyHudSetup 에서 호출).
        /// </summary>
        /// <param name="itemIcon">장비 아이콘 Image.</param>
        /// <param name="gradeBorder">등급 테두리 Image.</param>
        /// <param name="emptyIcon">빈 슬롯 아이콘 Image.</param>
        /// <param name="runeSocketIcons">룬 소켓 아이콘 목록 (최대 3개).</param>
        /// <param name="slotButton">슬롯 클릭 Button.</param>
        public void InitReferences(Image itemIcon, Image gradeBorder, Image emptyIcon,
                                   List<Image> runeSocketIcons, Button slotButton)
        {
            _itemIcon        = itemIcon;
            _gradeBorder     = gradeBorder;
            _emptyIcon       = emptyIcon;
            _runeSocketIcons = runeSocketIcons ?? new List<Image>();
            _slotButton      = slotButton;
        }

        /// <summary>슬롯 타입을 초기화한다.</summary>
        public void Init(EquipmentSlotType slotType)
        {
            _slotType = slotType;
        }

        /// <summary>장비 인스턴스를 표시한다. null이면 빈 슬롯으로 표시.</summary>
        public void Refresh(EquipmentInstance instance)
        {
            bool hasItem = instance != null;
            _emptyIcon.gameObject.SetActive(!hasItem);
            _itemIcon.gameObject.SetActive(hasItem);
            _gradeBorder.gameObject.SetActive(hasItem);

            if (!hasItem)
            {
                ClearRuneSockets();
                return;
            }

            // 아이콘
            _itemIcon.sprite = instance.Data.Icon;
            _itemIcon.color  = Color.white;

            // 등급 테두리 색상
            if (ColorUtility.TryParseHtmlString(
                    EquipmentGradeHelper.GetGradeColorHex(instance.Data.Grade),
                    out var gradeColor))
                _gradeBorder.color = gradeColor;

            // 룬 소켓 아이콘
            RefreshRuneSockets(instance);
        }

        /// <summary>선택 하이라이트를 켜거나 끈다.</summary>
        public void SetSelected(bool selected)
        {
            _gradeBorder.color = selected
                ? Color.yellow
                : _gradeBorder.color; // 선택 시 노란 테두리 (Presenter 재호출로 복원)
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private void RefreshRuneSockets(EquipmentInstance instance)
        {
            int socketCount = instance.Data.RuneSocketCount;
            for (int i = 0; i < _runeSocketIcons.Count; i++)
            {
                bool active = i < socketCount;
                _runeSocketIcons[i].gameObject.SetActive(active);
                if (!active) continue;

                var rune = instance.EquippedRunes[i];
                _runeSocketIcons[i].color = rune != null ? Color.cyan : Color.gray;
                _runeSocketIcons[i].sprite = rune?.Icon;
            }
        }

        private void ClearRuneSockets()
        {
            foreach (var icon in _runeSocketIcons)
                icon.gameObject.SetActive(false);
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleButtonClicked()
        {
            OnClicked?.Invoke(_slotType);
        }
    }
}
