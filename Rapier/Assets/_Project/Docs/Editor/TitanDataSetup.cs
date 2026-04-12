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

            data.attackRange = ATTACK_RANGE;

            data.phases = new List<PhaseEntry>
            {
                // [Phase1] Melee x3
                new PhaseEntry
                {
                    hpThreshold = 1f,
                    color = new Color(0.9f, 0.3f, 0.3f),
                    speedMultiplier = 1f,
                    attackMultiplier = 1f,
                    sequence = new List<EnemyAttackAction>
                    {
                        MakeMelee(), MakeMelee(), MakeMelee()
                    }
                },
                // [Phase2] Melee x3 + Charge
                new PhaseEntry
                {
                    hpThreshold = 0.5f,
                    color = new Color(1f, 0.15f, 0.1f),
                    speedMultiplier = 1.5f,
                    attackMultiplier = 1.3f,
                    sequence = new List<EnemyAttackAction>
                    {
                        MakeMelee(), MakeMelee(), MakeMelee(),
                        new ChargeAttackAction
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
                        }
                    }
                }
            };

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TitanDataSetup] 완료! phases[0]=Melee×3, phases[1]=Melee×3+Charge");
        }
    }
}
#endif
