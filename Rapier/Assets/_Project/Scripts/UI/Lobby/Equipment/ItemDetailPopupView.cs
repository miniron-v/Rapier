using System;
using System.Collections.Generic;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 아이템 상세 팝업 View.
    /// 아이콘, 스탯창(메인+서브 고정 크기), 룬 소켓 행, 설명, 장착/닫기 버튼을 세로 정렬로 표시한다.
    /// 로직 없음 — 표시 및 이벤트 발행만 담당. (MVP View 규칙)
    /// </summary>
    public class ItemDetailPopupView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("기본 정보")]
        [SerializeField] private TextMeshProUGUI _itemNameText;
        [SerializeField] private Image           _itemIcon;

        [Header("스탯창")]
        [SerializeField] private TextMeshProUGUI _mainStatText;
        [SerializeField] private List<TextMeshProUGUI> _subStatTexts = new();

        [Header("룬 소켓")]
        [SerializeField] private List<Button> _runeSocketButtons = new();
        [SerializeField] private List<Image>  _runeSocketIcons   = new();

        [Header("설명")]
        [SerializeField] private TextMeshProUGUI _descriptionText;

        [Header("버튼")]
        [SerializeField] private Button          _equipButton;
        [SerializeField] private TextMeshProUGUI _equipButtonText;
        [SerializeField] private Button          _closeButton;

        // ── 이벤트 (View → Presenter) ────────────────────────────────────────

        /// <summary>장착/해제 버튼 클릭</summary>
        public event Action OnEquipClicked;

        /// <summary>닫기 버튼 클릭</summary>
        public event Action OnCloseClicked;

        /// <summary>룬 소켓 클릭 (소켓 인덱스)</summary>
        public event Action<int> OnRuneSocketClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _equipButton?.onClick.AddListener(() => OnEquipClicked?.Invoke());
            _closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());

            for (int i = 0; i < _runeSocketButtons.Count; i++)
            {
                int idx = i;
                _runeSocketButtons[idx]?.onClick.AddListener(() => OnRuneSocketClicked?.Invoke(idx));
            }
        }

        private void OnDestroy()
        {
            _equipButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.RemoveAllListeners();
            foreach (var btn in _runeSocketButtons)
                btn?.onClick.RemoveAllListeners();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>
        /// LobbyHudSetup 에서 참조를 주입한다.
        /// </summary>
        public void InitReferences(
            TextMeshProUGUI itemNameText,
            Image           itemIcon,
            TextMeshProUGUI mainStatText,
            List<TextMeshProUGUI> subStatTexts,
            List<Button>    runeSocketButtons,
            List<Image>     runeSocketIcons,
            TextMeshProUGUI descriptionText,
            Button          equipButton,
            TextMeshProUGUI equipButtonText,
            Button          closeButton)
        {
            _itemNameText      = itemNameText;
            _itemIcon          = itemIcon;
            _mainStatText      = mainStatText;
            _subStatTexts      = subStatTexts ?? new List<TextMeshProUGUI>();
            _runeSocketButtons = runeSocketButtons ?? new List<Button>();
            _runeSocketIcons   = runeSocketIcons   ?? new List<Image>();
            _descriptionText   = descriptionText;
            _equipButton       = equipButton;
            _equipButtonText   = equipButtonText;
            _closeButton       = closeButton;

            // 런타임 주입 후 리스너 재등록
            _equipButton?.onClick.RemoveAllListeners();
            _equipButton?.onClick.AddListener(() => OnEquipClicked?.Invoke());
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());

            for (int i = 0; i < _runeSocketButtons.Count; i++)
            {
                int idx = i;
                _runeSocketButtons[idx]?.onClick.RemoveAllListeners();
                _runeSocketButtons[idx]?.onClick.AddListener(() => OnRuneSocketClicked?.Invoke(idx));
            }
        }

        // ── Public 메서드 (Presenter → View) ─────────────────────────────────

        /// <summary>팝업을 표시하거나 숨긴다.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        /// <summary>
        /// 아이템 상세 정보를 표시한다.
        /// isAccessory: 장신구면 RolledMainStat 사용, 아니면 SO MainStat 사용.
        /// </summary>
        public void SetData(EquipmentInstance instance, bool isEquipped, bool equippedByOther)
        {
            if (instance == null) return;

            var data = instance.Data;

            // 이름
            _itemNameText.text = data.ItemName;

            // 아이콘
            if (data.Icon != null)
            {
                _itemIcon.sprite = data.Icon;
                _itemIcon.color  = Color.white;
            }
            else
            {
                _itemIcon.sprite = null;
                _itemIcon.color  = EquipmentGradeHelper.GetGradeColor(instance.Grade);
            }

            // 메인 스탯
            bool isAccessory = data.SlotType == EquipmentSlotType.Necklace
                            || data.SlotType == EquipmentSlotType.Ring;

            StatEntry mainStat;
            if (isAccessory && instance.RolledMainStat.HasValue)
                mainStat = instance.RolledMainStat.Value;
            else
                mainStat = data.MainStat;

            _mainStatText.text = FormatStatEntry(mainStat);

            // 서브 스탯 (고정 크기 영역 — 없으면 비워둠)
            var subStats = instance.SubStats;
            for (int i = 0; i < _subStatTexts.Count; i++)
            {
                if (_subStatTexts[i] == null) continue;
                if (subStats != null && i < subStats.Count)
                {
                    _subStatTexts[i].text     = FormatStatEntry(subStats[i]);
                    _subStatTexts[i].gameObject.SetActive(true);
                }
                else
                {
                    _subStatTexts[i].text     = string.Empty;
                    _subStatTexts[i].gameObject.SetActive(true); // 고정 크기 유지
                }
            }

            // 룬 소켓 행
            int socketCount = instance.EquippedRunes != null ? instance.EquippedRunes.Length : 0;
            for (int i = 0; i < _runeSocketButtons.Count; i++)
            {
                bool active = i < socketCount;
                _runeSocketButtons[i]?.gameObject.SetActive(active);
                if (!active) continue;

                var rune = instance.EquippedRunes[i];
                if (i < _runeSocketIcons.Count && _runeSocketIcons[i] != null)
                    _runeSocketIcons[i].color = rune != null ? Color.cyan : Color.gray;
            }

            // 설명
            _descriptionText.text = data.Description;

            // 장착/해제 버튼 라벨 + 색상
            // 해제: 빨강 (닫기 버튼과 동일) / 장착: 녹색
            _equipButtonText.text = isEquipped ? "해제" : "장착";
            if (_equipButton != null && _equipButton.image != null)
            {
                _equipButton.image.color = isEquipped
                    ? new Color(0.5f, 0.2f, 0.2f)   // 해제 = 닫기와 동일 빨강
                    : new Color(0.2f, 0.7f, 0.3f);  // 장착 = 녹색
            }
        }

        // ── Private 유틸 ─────────────────────────────────────────────────────

        private static string FormatStatEntry(StatEntry entry)
        {
            string label = GetStatLabel(entry.statType);
            if (entry.flatValue != 0f)
                return $"{label} +{entry.flatValue:F0}";
            if (entry.percentValue != 0f)
                return $"{label} +{entry.percentValue:F1}%";
            return label;
        }

        private static string GetStatLabel(StatType type)
        {
            return type switch
            {
                StatType.HP                  => "HP",
                StatType.ATK                 => "공격력",
                StatType.MoveSpeed           => "이동속도",
                StatType.DodgeCDR            => "회피 쿨다운",
                StatType.ChargeTimeReduction => "차지 시간 단축",
                StatType.InvincibilityBonus  => "무적 시간",
                StatType.CritChance          => "치명타 확률",
                StatType.CritDamage          => "치명타 피해",
                StatType.SkillDamage         => "스킬 피해",
                _                            => type.ToString()
            };
        }
    }
}
