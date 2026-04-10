using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 2 — 캐릭터 관리 패널 View.
    ///
    /// [B1 구현]
    ///   - 캐릭터 슬롯 4칸: Rapier 활성, 나머지 3칸 Coming Soon
    ///   - EquipmentPanelRoot / LevelUpPanelRoot: B2/B3 hook용 빈 GameObject
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
        [SerializeField] private GameObject _slot2ComingSoon;
        [SerializeField] private GameObject _slot3ComingSoon;
        [SerializeField] private GameObject _slot4ComingSoon;

        // ── B2/B3 Hook Roots ─────────────────────────────────────
        // [B2] 이 GameObject 하위에 장비 슬롯 8개 + 인벤토리 패널 추가
        [SerializeField] private GameObject _equipmentPanelRoot;

        // [B3] 이 GameObject 하위에 레벨업/스킬 강화 패널 추가
        [SerializeField] private GameObject _levelUpPanelRoot;

        /// <summary>B2 hook: 장비 패널이 붙을 루트 GameObject.</summary>
        public GameObject EquipmentPanelRoot => _equipmentPanelRoot;

        /// <summary>B3 hook: 레벨업/스킬 영역이 붙을 루트 GameObject.</summary>
        public GameObject LevelUpPanelRoot => _levelUpPanelRoot;

        /// <summary>
        /// CharacterTabPresenter가 초기화 시 호출한다.
        /// </summary>
        public void Init(
            GameObject rapierSlot,
            GameObject slot2,
            GameObject slot3,
            GameObject slot4,
            GameObject equipmentPanelRoot,
            GameObject levelUpPanelRoot)
        {
            _rapierSlot        = rapierSlot;
            _slot2ComingSoon   = slot2;
            _slot3ComingSoon   = slot3;
            _slot4ComingSoon   = slot4;
            _equipmentPanelRoot = equipmentPanelRoot;
            _levelUpPanelRoot   = levelUpPanelRoot;
        }

        /// <summary>Rapier 슬롯 활성화, 나머지 3칸 Coming Soon 상태로 표시한다.</summary>
        public void SetupCharacterSlots()
        {
            SetSlotActive(_rapierSlot,      isActive: true);
            SetSlotActive(_slot2ComingSoon, isActive: false);
            SetSlotActive(_slot3ComingSoon, isActive: false);
            SetSlotActive(_slot4ComingSoon, isActive: false);
        }

        // View는 표시만 담당 — 슬롯 인터랙션 로직은 Presenter가 처리한다.
        private void SetSlotActive(GameObject slot, bool isActive)
        {
            if (slot == null) return;

            // Rapier 슬롯: 정상 표시
            // Coming Soon 슬롯: 비활성(dimmed) 표시 — Interactable 끄기만 하고 이미지는 유지
            var button = slot.GetComponentInChildren<Button>();
            if (button != null)
                button.interactable = isActive;

            var comingSoonLabel = slot.transform.Find("ComingSoonLabel");
            if (comingSoonLabel != null)
                comingSoonLabel.gameObject.SetActive(!isActive);
        }
    }
}
