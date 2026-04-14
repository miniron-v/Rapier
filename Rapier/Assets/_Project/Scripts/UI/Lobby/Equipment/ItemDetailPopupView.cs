using System;
using System.Collections;
using System.Collections.Generic;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 아이템 상세 팝업 View.
    /// 아이콘, 스탯창(메인+서브 고정 크기), 룬 소켓 행, 설명, 장착/강화/닫기 버튼을 세로 정렬로 표시한다.
    /// 로직 없음 — 표시 및 이벤트 발행만 담당. (MVP View 규칙)
    /// Phase 25-C: 3버튼화 (장착/해제 · 강화 · 닫기), disableActions, 서브스탯 노란 펄스 지원.
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
        [SerializeField] private Button          _enhanceButton;
        [SerializeField] private TextMeshProUGUI _enhanceButtonText;
        [SerializeField] private Button          _closeButton;

        // ── 이벤트 (View → Presenter) ────────────────────────────────────────

        /// <summary>장착/해제 버튼 클릭</summary>
        public event Action OnEquipClicked;

        /// <summary>강화 버튼 클릭</summary>
        public event Action OnEnhanceClicked;

        /// <summary>닫기 버튼 클릭</summary>
        public event Action OnCloseClicked;

        /// <summary>룬 소켓 클릭 (소켓 인덱스)</summary>
        public event Action<int> OnRuneSocketClicked;

        // ── Private ──────────────────────────────────────────────────────────

        [System.NonSerialized] private Coroutine _subStatPulseCoroutine;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _equipButton?.onClick.AddListener(() => OnEquipClicked?.Invoke());
            _enhanceButton?.onClick.AddListener(() => OnEnhanceClicked?.Invoke());
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
            _enhanceButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.RemoveAllListeners();
            foreach (var btn in _runeSocketButtons)
                btn?.onClick.RemoveAllListeners();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>
        /// LobbyHudSetup 에서 참조를 주입한다.
        /// Phase 25-C: enhanceButton, enhanceButtonText 추가.
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
            Button          enhanceButton,
            TextMeshProUGUI enhanceButtonText,
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
            _enhanceButton     = enhanceButton;
            _enhanceButtonText = enhanceButtonText;
            _closeButton       = closeButton;

            // 런타임 주입 후 리스너 재등록
            _equipButton?.onClick.RemoveAllListeners();
            _equipButton?.onClick.AddListener(() => OnEquipClicked?.Invoke());
            _enhanceButton?.onClick.RemoveAllListeners();
            _enhanceButton?.onClick.AddListener(() => OnEnhanceClicked?.Invoke());
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
        /// disableActions: 분해 모드 진입 시 true — 장착/강화 모두 비활성.
        /// </summary>
        public void SetData(EquipmentInstance instance, bool isEquipped, bool equippedByOther,
                            bool disableActions = false)
        {
            if (instance == null) return;

            var data = instance.Data;

            // 이름 (+N 강화 단계 표시, 0 이면 생략)
            _itemNameText.text = instance.EnhanceLevel > 0
                ? $"{data.ItemName} +{instance.EnhanceLevel}"
                : data.ItemName;

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

            StatEntry rawMainStat;
            if (isAccessory && instance.RolledMainStat.HasValue)
                rawMainStat = instance.RolledMainStat.Value;
            else
                rawMainStat = data.MainStat;

            // 강화 배율 적용 (enhanceLevel 0이면 ×1.0 — 변화 없음)
            float enhanceMult = 1f + 0.10f * instance.EnhanceLevel;
            _mainStatText.text = FormatStatEntryWithEnhance(rawMainStat, enhanceMult);

            // 서브 스탯 (고정 크기 영역 — 없으면 비워둠)
            var subStats = instance.SubStats;
            for (int i = 0; i < _subStatTexts.Count; i++)
            {
                if (_subStatTexts[i] == null) continue;
                if (subStats != null && i < subStats.Count)
                {
                    _subStatTexts[i].text     = FormatStatEntry(subStats[i]);
                    _subStatTexts[i].gameObject.SetActive(true);
                    // 펄스 중이 아닌 경우 기본 색 복원
                    if (_subStatPulseCoroutine == null)
                        _subStatTexts[i].color = new Color(0.8f, 0.8f, 0.8f);
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

            // 장착/해제 버튼 상태
            if (disableActions)
            {
                SetEquipButtonDisabled();
                SetEnhanceButtonDisabled();
            }
            else
            {
                // 장착/해제 버튼 라벨 + 색상
                _equipButtonText.text = isEquipped ? "해제" : "장착";
                if (_equipButton != null)
                {
                    _equipButton.interactable = true;
                    if (_equipButton.image != null)
                    {
                        _equipButton.image.color = isEquipped
                            ? new Color(0.5f, 0.2f, 0.2f)   // 해제 = 빨강
                            : new Color(0.2f, 0.7f, 0.3f);  // 장착 = 녹색
                    }
                }

                // 강화 버튼 활성 조건
                bool atMax = instance.EnhanceLevel >= instance.MaxEnhanceLevel;
                if (atMax)
                    SetEnhanceButtonDisabled();
                else
                    SetEnhanceButtonEnabled();
            }
        }

        /// <summary>
        /// 서브스탯 강화 발동 시 지정 인덱스 텍스트를 1.5초 노란 펄스로 강조한다.
        /// HasUpgradedSubStat=true 인 서브스탯을 ItemDetailPopupPresenter 가 hint 전달.
        /// </summary>
        public void PulseSubStatHighlight(int subStatIndex)
        {
            if (subStatIndex < 0 || subStatIndex >= _subStatTexts.Count) return;
            if (_subStatTexts[subStatIndex] == null) return;

            if (_subStatPulseCoroutine != null)
                StopCoroutine(_subStatPulseCoroutine);

            _subStatPulseCoroutine = StartCoroutine(SubStatPulseRoutine(subStatIndex, 1.5f));
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void SetEquipButtonDisabled()
        {
            if (_equipButton != null)
            {
                _equipButton.interactable = false;
                if (_equipButton.image != null)
                    _equipButton.image.color = new Color(0.4f, 0.4f, 0.4f);
            }
        }

        private void SetEnhanceButtonEnabled()
        {
            if (_enhanceButton != null)
            {
                _enhanceButton.interactable = true;
                if (_enhanceButton.image != null)
                    _enhanceButton.image.color = new Color(0.8f, 0.55f, 0.1f); // 강화 = 주황
            }
            if (_enhanceButtonText != null)
                _enhanceButtonText.color = Color.white;
        }

        private void SetEnhanceButtonDisabled()
        {
            if (_enhanceButton != null)
            {
                _enhanceButton.interactable = false;
                if (_enhanceButton.image != null)
                    _enhanceButton.image.color = new Color(0.4f, 0.4f, 0.4f);
            }
            if (_enhanceButtonText != null)
                _enhanceButtonText.color = new Color(0.6f, 0.6f, 0.6f);
        }

        private IEnumerator SubStatPulseRoutine(int index, float duration)
        {
            var tmp        = _subStatTexts[index];
            var normalColor = new Color(0.8f, 0.8f, 0.8f);
            var pulseColor  = Color.yellow;

            float elapsed  = 0f;
            float period   = 0.4f; // 펄스 주기

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t    = (elapsed % period) / period;
                float ping = Mathf.PingPong(t * 2f, 1f); // 0→1→0
                tmp.color  = Color.Lerp(normalColor, pulseColor, ping);
                yield return null;
            }

            // 복원
            if (tmp != null) tmp.color = normalColor;
            _subStatPulseCoroutine = null;
        }

        // ── Private 유틸 ─────────────────────────────────────────────────────

        private static string FormatStatEntry(StatEntry entry)
        {
            string label = GetStatLabel(entry.statType);
            if (entry.flatValue != 0f)
                return $"{label} +{entry.flatValue:F0}";
            if (entry.percentValue != 0f)
                return $"{label} +{entry.percentValue:F1}%";  // percentValue 단위: 0~100, 직접 표시
            return label;
        }

        /// <summary>강화 배율이 적용된 StatEntry를 포맷한다.</summary>
        private static string FormatStatEntryWithEnhance(StatEntry entry, float enhanceMultiplier)
        {
            var enhanced = new StatEntry
            {
                statType     = entry.statType,
                flatValue    = entry.flatValue    * enhanceMultiplier,
                percentValue = entry.percentValue * enhanceMultiplier,
            };
            return FormatStatEntry(enhanced);
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
