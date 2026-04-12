#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Game.Enemies;

namespace Game.Editor
{
    public static class TitanDataSetup
    {
        [MenuItem("Rapier/Dev/Setup Titan Attack Sequence")]
        public static void SetupTitanSequence()
        {
            const string path = "Assets/_Project/ScriptableObjects/Enemies/Boss/TitanStatData.asset";
            var data = AssetDatabase.LoadAssetAtPath<BossStatData>(path);
            if (data == null)
            {
                Debug.LogError("[TitanDataSetup] TitanStatData.asset 없음.");
                return;
            }

            const float ATTACK_RANGE    = 4f;
            const float WINDUP_DURATION = 1f;
            const float SECTOR_ANGLE    = 120f;

            AttackIndicatorEntry MakeSectorEntry() => new AttackIndicatorEntry
            {
                shape       = AttackIndicatorShape.Sector,
                angleOffset = 0f,
                sectorData  = new SectorIndicatorData { range = ATTACK_RANGE, angle = SECTOR_ANGLE }
            };

            MeleeAttackAction MakeMelee() => new MeleeAttackAction
            {
                windupDuration         = WINDUP_DURATION,
                lockIndicatorDirection = true,
                hitRange               = ATTACK_RANGE,
                indicators             = new List<AttackIndicatorEntry> { MakeSectorEntry() }
            };

            // 기존 직렬화 데이터 먼저 초기화
            data.attackRange    = ATTACK_RANGE;
            data.attackSequence  = null;
            data.phase2Sequence  = null;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            // 재할당
            data.attackSequence = new List<EnemyAttackAction>
            {
                MakeMelee(), MakeMelee(), MakeMelee()
            };

            var charge = new ChargeAttackAction
            {
                windupDuration         = WINDUP_DURATION,
                lockIndicatorDirection = true,
                chargeSpeed            = 14f,
                chargeHitRange         = 1.8f,
                damagePercent          = 200,
                chargeMaxDistance      = 20f,
                grogyDuration          = 2.5f,
                indicators             = new List<AttackIndicatorEntry>
                {
                    new AttackIndicatorEntry
                    {
                        shape    = AttackIndicatorShape.Rectangle,
                        angleOffset = 0f,
                        rectData = new RectIndicatorData { range = 20f, width = 3.6f }
                    }
                }
            };

            data.phase2Sequence = new List<EnemyAttackAction>
            {
                MakeMelee(), MakeMelee(), MakeMelee(), charge
            };

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TitanDataSetup] 완료! attackRange=4 windupDuration=1 lockDirection=true");
        }
    }
}
#endif
