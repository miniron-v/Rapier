using System;
using Game.Data.Equipment;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 인벤토리 내 단일 장비 아이템 UI.
    /// 아이콘, 등급 색상 배경, 메인 스탯 텍스트를 표시한다.
    /// 로직 없음 — 표시만 담당. (MVP View 규칙)
    /// </summary>
    public class InventoryItemView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private Image _itemIcon;
        [SerializeField] private Image _gradeBackground;
        [SerializeField] private TextMeshProUGUI _itemNameText;
        [SerializeField] private TextMeshProUGUI _mainStatText;
        [SerializeField] private Button _itemButton;

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentInstance _instance;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>아이템 클릭 이벤트 (장비 인스턴스 전달)</summary>
        public event Action<EquipmentInstance> OnClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _itemButton.onClick.AddListener(HandleButtonClicked);
        }

        private void OnDestroy()
        {
            _itemButton.onClick.RemoveListener(HandleButtonClicked);
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// 런타임 생성 시 SerializeField 참조를 외부에서 주입한다 (LobbyHudSetup 에서 호출).
        /// </summary>
        /// <param name="itemIcon">아이템 아이콘 Image.</param>
        /// <param name="gradeBackground">등급 배경 Image.</param>
        /// <param name="itemNameText">아이템 이름 TMP 텍스트.</param>
        /// <param name="mainStatText">메인 스탯 TMP 텍스트.</param>
        /// <param name="itemButton">아이템 클릭 Button.</param>
        public void InitReferences(Image itemIcon, Image gradeBackground,
                                   TextMeshProUGUI itemNameText, TextMeshProUGUI mainStatText,
                                   Button itemButton)
        {
            _itemIcon        = itemIcon;
            _gradeBackground = gradeBackground;
            _itemNameText    = itemNameText;
            _mainStatText    = mainStatText;
            _itemButton      = itemButton;
        }

        /// <summary>장비 인스턴스를 기반으로 UI를 갱신한다.</summary>
        public void Refresh(EquipmentInstance instance)
        {
            _instance = instance;

            if (instance == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            _itemIcon.sprite  = instance.Data.Icon;
            _itemNameText.text = instance.Data.ItemName;

            // 등급 배경 색상
            if (ColorUtility.TryParseHtmlString(
                    EquipmentGradeHelper.GetGradeColorHex(instance.Data.Grade),
                    out var gradeColor))
                _gradeBackground.color = gradeColor;

            // 메인 스탯 텍스트 간단 표시
            var ms = instance.Data.MainStat;
            _mainStatText.text = ms.flatValue > 0
                ? $"{ms.statType} +{ms.flatValue}"
                : $"{ms.statType} +{ms.percentValue * 100f:F0}%";
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleButtonClicked()
        {
            OnClicked?.Invoke(_instance);
        }
    }
}
