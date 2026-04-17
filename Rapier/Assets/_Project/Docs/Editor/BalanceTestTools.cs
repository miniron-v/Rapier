#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Game.Core;
using Game.Core.Services;
using Game.Data.Equipment;

namespace Game.Editor.DevTools
{
    /// <summary>
    /// 밸런싱 테스트용 에디터 메뉴.
    /// 재화·재료·보스 유니크 장비를 즉시 지급하여 장착/강화 플로우를 빠르게 검증한다.
    /// Play 모드 + ServiceLocator 초기화(GameBootstrap) 완료 상태에서 사용.
    /// </summary>
    internal static class BalanceTestTools
    {
        private const int CRYSTAL_GRANT = 10000;
        private const int DUST_GRANT    = 50000;

        [MenuItem("Rapier/Dev/Grant Crystal (+10000)")]
        private static void GrantCrystal()
        {
            if (!EnsurePlayMode()) return;
            var currency = ServiceLocator.TryGet<CurrencyService>();
            if (currency == null)
            {
                Debug.LogWarning("[BalanceTestTools] CurrencyService 미등록. GameBootstrap 완료 전인지 확인.");
                return;
            }
            currency.AddCrystal(CRYSTAL_GRANT);
            Debug.Log($"[BalanceTestTools] Crystal +{CRYSTAL_GRANT} (총 {currency.Crystal})");
        }

        [MenuItem("Rapier/Dev/Grant Dust (+50000)")]
        private static void GrantDust()
        {
            if (!EnsurePlayMode()) return;
            var em = ServiceLocator.TryGet<EquipmentManager>();
            if (em == null)
            {
                Debug.LogWarning("[BalanceTestTools] EquipmentManager 미등록. GameBootstrap 완료 전인지 확인.");
                return;
            }
            em.AddDust(DUST_GRANT);
            Debug.Log($"[BalanceTestTools] Dust +{DUST_GRANT} (총 {em.Dust})");
        }

        [MenuItem("Rapier/Dev/Grant Boss Unique Items")]
        private static void GrantBossUniqueItems()
        {
            if (!EnsurePlayMode()) return;
            var em = ServiceLocator.TryGet<EquipmentManager>();
            if (em == null)
            {
                Debug.LogWarning("[BalanceTestTools] EquipmentManager 미등록. GameBootstrap 완료 전인지 확인.");
                return;
            }

            var database = Resources.Load<EquipmentDatabase>("EquipmentDatabase");
            if (database == null)
            {
                Debug.LogError("[BalanceTestTools] Resources/EquipmentDatabase.asset 없음.");
                return;
            }

            int granted = 0;
            foreach (var data in database.AllEquipment)
            {
                if (data == null) continue;
                if (data.Grade != EquipmentGrade.Unique) continue;
                if (!data.IsBossItem) continue;

                em.AddEquipmentToInventory(new EquipmentInstance(data));
                granted++;
                Debug.Log($"[BalanceTestTools] 지급: {data.name}");
            }

            if (granted == 0)
                Debug.LogWarning("[BalanceTestTools] 보스 유니크 장비 0건. EquipmentDatabase 등록 상태 확인.");
            else
                Debug.Log($"[BalanceTestTools] 보스 유니크 장비 {granted}건 지급 완료.");
        }

        private static bool EnsurePlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning("[BalanceTestTools] Play 모드에서만 동작. 게임 실행 후 사용.");
            return false;
        }
    }
}
#endif
