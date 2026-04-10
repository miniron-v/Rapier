#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Game.Enemies;

namespace Game.Editor
{
    public static class EnemyDataSetup
    {
        // ── 일반 적 ───────────────────────────────────────────────
        [MenuItem("Rapier/Dev/Setup Normal Enemy Sequence")]
        public static void SetupNormalEnemySequence()
        {
            const string path = "Assets/_Project/ScriptableObjects/Enemies/NormalEnemyStatData.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyStatData>(path);
            if (data == null)
            {
                Debug.LogError("[EnemyDataSetup] NormalEnemyStatData.asset 없음.");
                return;
            }

            // 일반 적: 좁은 부채꼴 근접 공격 1종
            const float RANGE    = 1.2f;
            const float WINDUP   = 0.5f;
            const float ANGLE    = 90f;

            data.attackRange    = RANGE;
            data.attackSequence = null;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            data.attackSequence = new List<EnemyAttackAction>
            {
                new MeleeAttackAction
                {
                    windupDuration         = WINDUP,
                    lockIndicatorDirection = false,
                    hitRange               = RANGE,
                    indicators             = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape       = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = RANGE, angle = ANGLE }
                        }
                    }
                }
            };

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyDataSetup] NormalEnemyStatData 시퀀스 설정 완료!");
        }

        // ── 스펙터 ─────────────────────────────────────────────────
        [MenuItem("Rapier/Dev/Setup Specter Sequence")]
        public static void SetupSpecterSequence()
        {
            const string path = "Assets/_Project/ScriptableObjects/Enemies/Boss/SpecterStatData.asset";
            var data = AssetDatabase.LoadAssetAtPath<BossStatData>(path);
            if (data == null)
            {
                Debug.LogError("[EnemyDataSetup] SpecterStatData.asset 없음.");
                return;
            }

            // 스펙터: 빠르고 좁은 근접 공격
            const float RANGE  = 1.4f;
            const float ANGLE  = 80f;

            AttackIndicatorEntry MakeSectorEntry(float windupDur) => new AttackIndicatorEntry
            {
                shape       = AttackIndicatorShape.Sector,
                angleOffset = 0f,
                sectorData  = new SectorIndicatorData { range = RANGE, angle = ANGLE }
            };

            MeleeAttackAction MakeMelee(float windupDur) => new MeleeAttackAction
            {
                windupDuration         = windupDur,
                lockIndicatorDirection = false,
                hitRange               = RANGE,
                indicators             = new List<AttackIndicatorEntry> { MakeSectorEntry(windupDur) }
            };

            data.attackRange     = RANGE;
            data.attackSequence  = null;
            data.phase2Sequence  = null;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            // 1페이즈: 빠른 근접 공격 (windupDuration 0.25)
            data.attackSequence = new List<EnemyAttackAction>
            {
                MakeMelee(0.25f)
            };

            // 2페이즈: 순간이동 후 근접 공격
            // TeleportAttackAction은 인디케이터 없음 (순간이동 자체가 예고)
            var teleport = new TeleportAttackAction
            {
                windupDuration         = 0.15f,
                lockIndicatorDirection = false,
                teleportOffset         = 1.2f,
                fadeTime               = 0.15f,
                indicators             = new List<AttackIndicatorEntry>() // 인디케이터 없음
            };

            data.phase2Sequence = new List<EnemyAttackAction>
            {
                teleport,
                MakeMelee(0.25f)
            };

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyDataSetup] SpecterStatData 시퀀스 설정 완료!");
        }
    }
}
#endif
