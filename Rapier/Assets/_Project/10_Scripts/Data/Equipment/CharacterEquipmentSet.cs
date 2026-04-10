using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 특정 캐릭터의 8슬롯 장착 상태 + 룬 장착 상태를 메모리에서 관리한다.
    /// 저장/복원은 IEquipmentSaveProvider(B3 담당)가 처리한다.
    /// </summary>
    public class CharacterEquipmentSet
    {
        private readonly string _characterId;

        // 슬롯 → 장착된 EquipmentInstance 매핑
        private readonly Dictionary<EquipmentSlotType, EquipmentInstance> _slots
            = new Dictionary<EquipmentSlotType, EquipmentInstance>();

        /// <summary>이 장착 세트 소유자의 캐릭터 ID</summary>
        public string CharacterId => _characterId;

        public CharacterEquipmentSet(string characterId)
        {
            _characterId = characterId;
        }

        // ── 장착 / 해제 ────────────────────────────────────────────────────

        /// <summary>
        /// 장비를 해당 슬롯에 장착한다.
        /// 이미 다른 장비가 장착되어 있으면 해제 후 교체한다.
        /// </summary>
        /// <returns>교체로 해제된 이전 장비 인스턴스 (없으면 null)</returns>
        public EquipmentInstance Equip(EquipmentInstance instance)
        {
            if (instance == null)
            {
                Debug.LogWarning("[CharacterEquipmentSet] null 장비는 장착할 수 없습니다.");
                return null;
            }

            var slot = instance.Data.SlotType;
            _slots.TryGetValue(slot, out var previous);
            _slots[slot] = instance;
            return previous;
        }

        /// <summary>지정 슬롯의 장비를 해제한다. 해제된 인스턴스를 반환.</summary>
        public EquipmentInstance Unequip(EquipmentSlotType slot)
        {
            if (!_slots.TryGetValue(slot, out var instance))
                return null;
            _slots.Remove(slot);
            return instance;
        }

        /// <summary>지정 슬롯에 장착된 장비를 반환한다. 없으면 null.</summary>
        public EquipmentInstance GetEquipped(EquipmentSlotType slot)
        {
            _slots.TryGetValue(slot, out var instance);
            return instance;
        }

        /// <summary>현재 장착된 모든 슬롯을 순회한다.</summary>
        public IEnumerable<KeyValuePair<EquipmentSlotType, EquipmentInstance>> GetAllEquipped()
            => _slots;

        // ── 룬 ─────────────────────────────────────────────────────────────

        /// <summary>특정 슬롯의 특정 소켓에 룬을 장착한다.</summary>
        public bool EquipRune(EquipmentSlotType slot, int socketIndex, RuneItemData rune)
        {
            if (!_slots.TryGetValue(slot, out var instance))
            {
                Debug.LogWarning($"[CharacterEquipmentSet] 슬롯 {slot}에 장착된 장비 없음");
                return false;
            }
            return instance.EquipRune(socketIndex, rune);
        }

        /// <summary>특정 슬롯의 특정 소켓 룬을 해제한다.</summary>
        public bool UnequipRune(EquipmentSlotType slot, int socketIndex)
        {
            if (!_slots.TryGetValue(slot, out var instance))
                return false;
            return instance.UnequipRune(socketIndex);
        }
    }
}
