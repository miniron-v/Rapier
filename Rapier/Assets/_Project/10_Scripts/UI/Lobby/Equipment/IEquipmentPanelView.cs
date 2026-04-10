using System;
using System.Collections.Generic;
using Game.Data.Equipment;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 장비 패널 View 계약 인터페이스.
    /// Presenter는 이 인터페이스를 통해서만 View와 통신한다. (DIP)
    /// </summary>
    public interface IEquipmentPanelView
    {
        // ── 이벤트 (View → Presenter) ────────────────────────────────────────

        /// <summary>슬롯 클릭 (어떤 슬롯이 선택되었는가)</summary>
        event Action<EquipmentSlotType> OnSlotClicked;

        /// <summary>인벤토리 아이템 클릭 (어떤 장비 인스턴스가 선택되었는가)</summary>
        event Action<EquipmentInstance> OnInventoryItemClicked;

        /// <summary>룬 소켓 클릭 (슬롯, 소켓 인덱스)</summary>
        event Action<EquipmentSlotType, int> OnRuneSocketClicked;

        // ── 메서드 (Presenter → View) ────────────────────────────────────────

        /// <summary>8슬롯을 전달된 장착 상태로 갱신한다.</summary>
        void RefreshSlots(IReadOnlyDictionary<EquipmentSlotType, EquipmentInstance> equipped);

        /// <summary>인벤토리 목록을 갱신한다.</summary>
        void RefreshInventory(IReadOnlyList<EquipmentInstance> inventory);

        /// <summary>특정 슬롯 뷰의 선택 하이라이트를 켜거나 끈다.</summary>
        void SetSlotSelected(EquipmentSlotType slot, bool selected);

        /// <summary>패널 전체를 표시하거나 숨긴다.</summary>
        void SetVisible(bool visible);
    }
}
