using System.Collections.Generic;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 장비/룬 인스턴스 저장·복원 인터페이스 스텁.
    /// 실제 JSON 저장 구현은 Phase 12-B3에서 담당한다.
    /// </summary>
    public interface IEquipmentSaveProvider
    {
        /// <summary>전체 장비 인벤토리를 저장한다.</summary>
        void SaveInventory(IReadOnlyList<EquipmentInstance> equipment,
                           IReadOnlyList<RuneItemData> runes);

        /// <summary>저장된 장비 인벤토리를 복원한다.</summary>
        (List<EquipmentInstance> equipment, List<RuneItemData> runes) LoadInventory();

        /// <summary>특정 캐릭터의 슬롯 장착 상태를 저장한다.</summary>
        void SaveCharacterEquipment(CharacterEquipmentSet equipmentSet);

        /// <summary>특정 캐릭터의 슬롯 장착 상태를 복원한다.</summary>
        CharacterEquipmentSet LoadCharacterEquipment(string characterId,
                                                     IReadOnlyList<EquipmentInstance> inventoryPool);
    }
}
