#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Core;
using Game.Data.Equipment;

namespace Game.DevTools
{
    /// <summary>
    /// Phase 15-A 테스트용 디버그 메뉴.
    /// Play Mode 에서 EquipmentDatabase 의 8슬롯 장비를 "Rapier" 캐릭터에 자동 장착 + Save 한다.
    /// 룬은 기존대로 인벤토리 추가만 수행한다.
    /// </summary>
    public static class AddDebugEquipmentMenu
    {
        [MenuItem("Rapier/Dev/Add Debug Equipment")]
        public static void AddDebugEquipment()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Dev] Add Debug Equipment 는 Play Mode 에서만 동작합니다.");
                return;
            }

            var db = Resources.Load<EquipmentDatabase>("EquipmentDatabase");
            if (db == null)
            {
                Debug.LogError("[Dev] Resources/EquipmentDatabase 를 찾을 수 없습니다. " +
                               "Assets/_Project/Resources/EquipmentDatabase.asset 이 있는지 확인.");
                return;
            }

            var manager = ServiceLocator.TryGet<EquipmentManager>();
            if (manager == null)
            {
                Debug.LogError("[Dev] ServiceLocator 에 EquipmentManager 미등록. GameBootstrap 초기화 확인.");
                return;
            }

            const string characterId = "Rapier";

            // 슬롯별 최초 매칭 SO 를 추적 (슬롯 → 인스턴스)
            var slotMap = new Dictionary<EquipmentSlotType, EquipmentInstance>();

            // 1. 인벤토리 추가 + 슬롯별 최초 SO 기록
            foreach (var data in db.AllEquipment)
            {
                if (data == null) continue;
                var instance = new EquipmentInstance(data);
                manager.AddEquipmentToInventory(instance);

                var slot = data.SlotType;
                if (!slotMap.ContainsKey(slot))
                    slotMap[slot] = instance;
            }

            // 2. 8 슬롯 전부 Equip — TrySave → SaveManager.Save() 체인이 내부에서 호출된다.
            int equippedCount = 0;
            var allSlots = System.Enum.GetValues(typeof(EquipmentSlotType));
            foreach (EquipmentSlotType slot in allSlots)
            {
                if (slotMap.TryGetValue(slot, out var inst))
                {
                    manager.Equip(characterId, inst);
                    equippedCount++;
                }
                else
                {
                    Debug.LogWarning($"[Dev] 슬롯 {slot} 에 매칭되는 장비 SO 없음 — 건너뜀.");
                }
            }

            // 3. 룬 주입 (인벤토리 추가만, 장착 자동화 불필요)
            int runeCount = 0;
            foreach (var rune in db.AllRunes)
            {
                if (rune == null) continue;
                manager.AddRuneToInventory(rune);
                runeCount++;
            }

            Debug.Log($"[Dev] Equipped {equippedCount}/8 slots + added {runeCount} runes. save.json updated.");
        }
    }
}
#endif
