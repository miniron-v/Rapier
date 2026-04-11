#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Core.Services;
using Game.Data.Equipment;

namespace Game.DevTools
{
    /// <summary>
    /// Phase 14 테스트용 디버그 메뉴.
    /// Play Mode 에서 EquipmentDatabase 의 모든 장비/룬 SO 를 EquipmentManager 인벤토리에 주입한다.
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

            // 장비 주입
            int equipCount = 0;
            foreach (var data in db.AllEquipment)
            {
                if (data == null) continue;
                var instance = new EquipmentInstance(data);
                manager.AddEquipmentToInventory(instance);
                equipCount++;
            }

            // 룬 주입
            int runeCount = 0;
            foreach (var rune in db.AllRunes)
            {
                if (rune == null) continue;
                manager.AddRuneToInventory(rune);
                runeCount++;
            }

            Debug.Log($"[Dev] Added {equipCount} equipment + {runeCount} runes to inventory.");
        }
    }
}
#endif
