using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 2 — 캐릭터 관리 패널 View.
    ///
    /// [Phase 19 업데이트]
    ///   - Assassin 슬롯 활성화 (Rapier + Assassin 2칸 활성, 나머지 2칸 Coming Soon)
    ///   - OnCharacterSelected 이벤트 추가: Presenter 에서 구독해 lastCharacterId 저장
    ///   - SetHighlight(string) 로 현재 선택된 슬롯 강조 표시
    ///
    /// [B2 hook]
    ///   EquipmentPanelRoot 하위에 장비 슬롯 8개, 인벤토리 패널을 추가할 것.
    ///
    /// [B3 hook]
    ///   LevelUpPanelRoot 하위에 레벨업/스킬 강화 패널을 추가할 것.
    /// </summary>
    public class CharacterTabView : LobbyTabViewBase
    {
        [Header("Character Slots")]
        [SerializeField] private GameObject _rapierSlot;
        [SerializeField] private GameObject _assassinSlot;
        [SerializeField] private GameObject _slot3ComingSoon;
        [SerializeField] private GameObject _slot4ComingSoon;

        // ── B2/B3 Hook Roots ─────────────────────────────────────
        /// <summary>[B2] 이 GameObject 하위에 장비 슬롯 8개 + 인벤토리 패널 추가</summary>
        [SerializeField] private GameObject _equipmentPanelRoot;

        /// <summary>[B3] 이 GameObject 하위에 레벨업/스킬 강화 패널 추가</summary>
        [SerializeField] private GameObject _levelUpPanelRoot;

        // ── 하이라이트 색상 ──────────────────────────────────────
        private static readonly Color COLOR_SELECTED   = new Color(0.95f, 0.85f, 0.20f, 1.00f); // 금색
        private static readonly Color COLOR_DESELECTED = new Color(0.25f, 0.30f, 0.40f, 1.00f); // 기본 슬롯

        /// <summary>B2 hook: 장비 패널이 붙을 루트 GameObject.</summary>
        public GameObject EquipmentPanelRoot => _equipmentPanelRoot;

        /// <summary>B3 hook: 레벨업/스킬 영역이 붙을 루트 GameObject.</summary>
        public GameObject LevelUpPanelRoot => _levelUpPanelRoot;

        // ── 이벤트 ──────────────────────────────────────────────
        /// <summary>
        /// 플레이어가 캐릭터 슬롯을 선택했을 때 발행. 파라미터: characterId ("Rapier"/"Assassin").
        /// CharacterTabPresenter 에서 구독해 SaveData.lastCharacterId 를 갱신한다.
        /// </summary>
        public event Action<string> OnCharacterSelected;

        /// <summary>CharacterTabPresenter가 초기화 시 호출한다.</summary>
        public void Init(
            GameObject rapierSlot,
            GameObject assassinSlot,
            GameObject slot3,
            GameObject slot4,
            GameObject equipmentPanelRoot,
            GameObject levelUpPanelRoot)
        {
            _rapierSlot         = rapierSlot;
            _assassinSlot       = assassinSlot;
            _slot3ComingSoon    = slot3;
            _slot4ComingSoon    = slot4;
            _equipmentPanelRoot = equipmentPanelRoot;
            _levelUpPanelRoot   = levelUpPanelRoot;
        }

        /// <summary>
        /// Rapier + Assassin 슬롯 활성화, 나머지 2칸 Coming Soon 상태로 표시한다.
        /// 버튼 리스너도 여기서 등록한다.
        /// </summary>
        public void SetupCharacterSlots()
        {
            SetSlotActive(_rapierSlot,      isActive: true);
            SetSlotActive(_assassinSlot,    isActive: true);
            SetSlotActive(_slot3ComingSoon, isActive: false);
            SetSlotActive(_slot4ComingSoon, isActive: false);

            RegisterSlotButton(_rapierSlot,   "Rapier");
            RegisterSlotButton(_assassinSlot, "Assassin");
        }

        /// <summary>
        /// 지정된 characterId 의 슬롯을 강조 표시하고 나머지를 기본 색상으로 되돌린다.
        /// </summary>
        /// <param name="characterId">선택된 캐릭터 식별자.</param>
        public void SetHighlight(string characterId)
        {
            ApplyHighlight(_rapierSlot,   characterId == "Rapier");
            ApplyHighlight(_assassinSlot, characterId == "Assassin");
        }

        // ── Private 메서드 ────────────────────────────────────────
        private void SetSlotActive(GameObject slot, bool isActive)
        {
            if (slot == null) return;

            var button = slot.GetComponentInChildren<Button>();
            if (button != null)
                button.interactable = isActive;

            var comingSoonLabel = slot.transform.Find("ComingSoonLabel");
            if (comingSoonLabel != null)
                comingSoonLabel.gameObject.SetActive(!isActive);
        }

        private void RegisterSlotButton(GameObject slot, string characterId)
        {
            if (slot == null) return;
            var button = slot.GetComponentInChildren<Button>();
            if (button == null) return;

            // 중복 등록 방지를 위해 기존 리스너를 제거 후 재등록
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnCharacterSelected?.Invoke(characterId));
        }

        private void ApplyHighlight(GameObject slot, bool isSelected)
        {
            if (slot == null) return;
            var img = slot.GetComponent<Image>();
            if (img == null) img = slot.GetComponentInChildren<Image>();
            if (img == null) return;

            img.color = isSelected ? COLOR_SELECTED : COLOR_DESELECTED;
        }
    }
}
