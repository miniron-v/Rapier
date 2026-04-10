#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Enemies.Editor
{
    /// <summary>
    /// Phase 12-C 신규 보스 5종 + Specter 공격 시퀀스 초기값 설정 스크립트.
    /// 각 보스 SO를 선택한 후 해당 메뉴를 실행하면 attackSequence 가 덮어써진다.
    ///
    /// 메뉴 경로: Rapier/Dev/Boss Setup/[보스명]
    ///
    /// [사용법]
    ///   1. Project 창에서 해당 보스의 BossStatData SO 에셋 선택
    ///   2. 메뉴 실행
    ///   3. Inspector 에서 수치 미세 조정
    /// </summary>
    public static class BossDataSetup
    {
        // ── Specter 밸런싱 ────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Specter — 밸런싱 적용")]
        private static void SetupSpecterSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Specter")) return;

            // [Phase1] Melee 단독 (빠른 압박)
            so.attackSequence = new List<EnemyAttackAction>
            {
                new MeleeAttackAction
                {
                    windupDuration        = 0.55f,   // Titan(0.5) + 0.05초: 예측 가능성 확보
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.4f, angle = 80f }
                        }
                    }
                }
            };

            // [Phase2] Teleport → Melee
            so.phase2Sequence = new List<EnemyAttackAction>
            {
                new TeleportAttackAction
                {
                    teleportOffset = 1.3f,    // 플레이어 바로 옆: 1.2(너무 가깝) → 1.3
                    fadeTime       = 0.2f,    // 0.15 → 0.2: 예고 시간 확보
                    windupDuration = 0f
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.55f,
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.4f, angle = 80f }
                        }
                    }
                }
            };

            so.moveSpeed       = 4.5f;   // 빠른 이동속도 (Titan: 1.5, 베이스: 1.5)
            so.attackRange     = 1.2f;   // Phase1: 근접 범위. Phase2에서 텔레포트는 어디서든 발동
            so.postAttackDelay = 0.25f;
            so.phase2SpeedMultiplier  = 1.4f;
            so.phase2AttackMultiplier = 1.3f;

            SaveSO(so, "Specter");
        }

        // ── Pyromancer ────────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Pyromancer — 초기값 설정")]
        private static void SetupPyromancerSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Pyromancer")) return;

            // [Phase1] Projectile → GroundHazard
            so.attackSequence = new List<EnemyAttackAction>
            {
                new ProjectileAttackAction
                {
                    windupDuration      = 0.7f,
                    projectileSpeed     = 7f,
                    maxRange            = 12f,
                    hitRadius           = 0.4f,
                    damageMultiplier    = 1f,
                    homingStrength      = 0f,
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape    = AttackIndicatorShape.Rectangle,
                            angleOffset = 0f,
                            rectData = new RectIndicatorData { range = 12f, width = 0.8f }
                        }
                    }
                },
                new GroundHazardAttackAction
                {
                    windupDuration  = 0.8f,
                    duration        = 4f,
                    tickInterval    = 0.5f,
                    tickDamage      = 0.3f,
                    hazardRadius    = 1.5f
                }
            };

            // [Phase2] Projectile → GroundHazard → MultiDirectional
            so.phase2Sequence = new List<EnemyAttackAction>
            {
                new ProjectileAttackAction
                {
                    windupDuration      = 0.65f,
                    projectileSpeed     = 10f,
                    maxRange            = 15f,
                    hitRadius           = 0.4f,
                    damageMultiplier    = 1.2f,
                    homingStrength      = 0f,
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape    = AttackIndicatorShape.Rectangle,
                            angleOffset = 0f,
                            rectData = new RectIndicatorData { range = 15f, width = 0.8f }
                        }
                    }
                },
                new GroundHazardAttackAction
                {
                    windupDuration  = 0.8f,
                    duration        = 5f,
                    tickInterval    = 0.4f,
                    tickDamage      = 0.35f,
                    hazardRadius    = 1.8f
                },
                new MultiDirectionalAttackAction
                {
                    windupDuration      = 0.8f,
                    hitRange            = 2f,
                    damageMultiplier    = 1.2f,
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 2f, angle = 60f }
                        },
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 90f,
                            sectorData  = new SectorIndicatorData { range = 2f, angle = 60f }
                        },
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = -90f,
                            sectorData  = new SectorIndicatorData { range = 2f, angle = 60f }
                        }
                    }
                }
            };

            so.maxHp           = 2000f;
            so.attackPower     = 80f;
            so.moveSpeed       = 1.2f;   // 원거리이므로 약간 더 느림
            so.attackRange     = 8f;     // 원거리 공격 사거리
            so.postAttackDelay = 0.5f;
            so.phase2SpeedMultiplier  = 1.3f;
            so.phase2AttackMultiplier = 1.3f;
            so.bossScale       = 2.5f;
            so.phase1Color     = new Color(1f, 0.4f, 0.1f);
            so.phase2Color     = new Color(1f, 0.1f, 0.05f);

            SaveSO(so, "Pyromancer");
        }

        // ── Berserker ─────────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Berserker — 초기값 설정")]
        private static void SetupBerserkerSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Berserker")) return;

            // [Phase1] 3연타 Melee 콤보
            so.attackSequence = new List<EnemyAttackAction>
            {
                new MeleeAttackAction
                {
                    windupDuration        = 0.35f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.5f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.5f, angle = 100f }
                        }
                    }
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.35f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.5f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 40f,   // 약간 측면 방향
                            sectorData  = new SectorIndicatorData { range = 1.5f, angle = 100f }
                        }
                    }
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.35f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.5f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = -40f,
                            sectorData  = new SectorIndicatorData { range = 1.5f, angle = 100f }
                        }
                    }
                }
            };

            // [Phase2] 4연타 + Charge (광폭화)
            so.phase2Sequence = new List<EnemyAttackAction>
            {
                new MeleeAttackAction
                {
                    windupDuration        = 0.3f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.6f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.6f, angle = 110f }
                        }
                    }
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.3f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.6f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 50f,
                            sectorData  = new SectorIndicatorData { range = 1.6f, angle = 110f }
                        }
                    }
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.3f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.6f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = -50f,
                            sectorData  = new SectorIndicatorData { range = 1.6f, angle = 110f }
                        }
                    }
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.3f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.6f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.6f, angle = 120f }
                        }
                    }
                },
                new ChargeAttackAction
                {
                    windupDuration          = 0.6f,
                    chargeSpeed             = 16f,
                    chargeHitRange          = 1.8f,
                    chargeDamageMultiplier  = 2.2f,
                    chargeMaxDistance       = 20f,
                    grogyDuration           = 2f,
                    lockIndicatorDirection   = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape    = AttackIndicatorShape.Rectangle,
                            angleOffset = 0f,
                            rectData = new RectIndicatorData { range = 10f, width = 1.5f }
                        }
                    }
                }
            };

            so.maxHp           = 2000f;
            so.attackPower     = 90f;   // 근접 강조
            so.moveSpeed       = 3.5f;  // 빠른 추격
            so.attackRange     = 1.3f;
            so.postAttackDelay = 0.2f;  // 빠른 콤보 연결
            so.phase2SpeedMultiplier  = 1.8f;
            so.phase2AttackMultiplier = 1.4f;
            so.bossScale       = 2.2f;
            so.phase1Color     = new Color(0.8f, 0.2f, 0.1f);
            so.phase2Color     = new Color(1f, 0.05f, 0.05f);

            SaveSO(so, "Berserker");
        }

        // ── Stormcaller ───────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Stormcaller — 초기값 설정")]
        private static void SetupStormcallerSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Stormcaller")) return;

            // [Phase1] MultiDirectional (4방향, +형) → Projectile
            so.attackSequence = new List<EnemyAttackAction>
            {
                new MultiDirectionalAttackAction
                {
                    windupDuration      = 0.9f,
                    hitRange            = 2f,
                    damageMultiplier    = 1f,
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 0f,    sectorData = new SectorIndicatorData { range = 2.5f, angle = 50f } },
                        new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 90f,   sectorData = new SectorIndicatorData { range = 2.5f, angle = 50f } },
                        new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 180f,  sectorData = new SectorIndicatorData { range = 2.5f, angle = 50f } },
                        new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 270f,  sectorData = new SectorIndicatorData { range = 2.5f, angle = 50f } }
                    }
                },
                new ProjectileAttackAction
                {
                    windupDuration      = 0.7f,
                    projectileSpeed     = 8f,
                    maxRange            = 15f,
                    hitRadius           = 0.45f,
                    damageMultiplier    = 1f,
                    homingStrength      = 0f,
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape    = AttackIndicatorShape.Rectangle,
                            angleOffset = 0f,
                            rectData = new RectIndicatorData { range = 15f, width = 0.9f }
                        }
                    }
                }
            };

            // [Phase2] MultiDirectional(4방향) → Projectile(유도)
            so.phase2Sequence = new List<EnemyAttackAction>
            {
                new MultiDirectionalAttackAction
                {
                    windupDuration      = 0.85f,
                    hitRange            = 2f,
                    damageMultiplier    = 1.1f,
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 0f,    sectorData = new SectorIndicatorData { range = 2.5f, angle = 55f } },
                        new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 90f,   sectorData = new SectorIndicatorData { range = 2.5f, angle = 55f } },
                        new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 180f,  sectorData = new SectorIndicatorData { range = 2.5f, angle = 55f } },
                        new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 270f,  sectorData = new SectorIndicatorData { range = 2.5f, angle = 55f } }
                    }
                },
                new ProjectileAttackAction
                {
                    windupDuration      = 0.65f,
                    projectileSpeed     = 10f,
                    maxRange            = 20f,
                    hitRadius           = 0.45f,
                    damageMultiplier    = 1.2f,
                    homingStrength      = 45f,  // 유도 활성화
                    lockIndicatorDirection = true,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape    = AttackIndicatorShape.Rectangle,
                            angleOffset = 0f,
                            rectData = new RectIndicatorData { range = 15f, width = 0.9f }
                        }
                    }
                }
            };

            so.maxHp           = 2200f;
            so.attackPower     = 80f;
            so.moveSpeed       = 1.8f;
            so.attackRange     = 6f;
            so.postAttackDelay = 0.5f;
            so.phase2SpeedMultiplier  = 1.4f;
            so.phase2AttackMultiplier = 1.3f;
            so.bossScale       = 2.5f;
            so.phase1Color     = new Color(0.4f, 0.7f, 1f);
            so.phase2Color     = new Color(0.2f, 0.4f, 1f);

            SaveSO(so, "Stormcaller");
        }

        // ── Gravekeeper ───────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Gravekeeper — 초기값 설정")]
        private static void SetupGravekeeperSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Gravekeeper")) return;

            // [Phase1] Summon → Melee
            // 주의: SummonAttackAction.MinionPrefab / MinionData 는 [NonSerialized] 이므로
            //       GravekeeperBossPresenter Inspector 에서 _minionPrefab / _minionData 를 설정한다.
            so.attackSequence = new List<EnemyAttackAction>
            {
                new SummonAttackAction
                {
                    windupDuration = 1.0f,
                    minionCount    = 2,
                    spawnRadius    = 2f
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.6f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.5f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.5f, angle = 90f }
                        }
                    }
                }
            };

            // [Phase2] Summon → Summon → Melee → Melee
            so.phase2Sequence = new List<EnemyAttackAction>
            {
                new SummonAttackAction
                {
                    windupDuration = 0.9f,
                    minionCount    = 2,
                    spawnRadius    = 2f
                },
                new SummonAttackAction
                {
                    windupDuration = 0.9f,
                    minionCount    = 2,
                    spawnRadius    = 3f
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.6f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.5f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.5f, angle = 90f }
                        }
                    }
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.6f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.5f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.5f, angle = 90f }
                        }
                    }
                }
            };

            so.maxHp           = 2000f;
            so.attackPower     = 75f;
            so.moveSpeed       = 1.5f;
            so.attackRange     = 1.3f;
            so.postAttackDelay = 0.4f;
            so.phase2SpeedMultiplier  = 1.3f;
            so.phase2AttackMultiplier = 1.25f;
            so.bossScale       = 2.5f;
            so.phase1Color     = new Color(0.3f, 0.6f, 0.3f);
            so.phase2Color     = new Color(0.1f, 0.9f, 0.3f);

            SaveSO(so, "Gravekeeper");
        }

        // ── Twin Phantoms ─────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/TwinPhantoms — 초기값 설정 (Body A)")]
        private static void SetupTwinPhantomsSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "TwinPhantoms")) return;

            // [1페이즈] Melee × 2 (두 체가 독립으로 실행)
            // phase2Sequence 는 비워둔다 — TwinPhantomsBossPresenter 가 파트너 사망으로 강화
            so.attackSequence = new List<EnemyAttackAction>
            {
                new MeleeAttackAction
                {
                    windupDuration        = 0.5f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.3f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = 0f,
                            sectorData  = new SectorIndicatorData { range = 1.3f, angle = 90f }
                        }
                    }
                },
                new MeleeAttackAction
                {
                    windupDuration        = 0.45f,
                    lockIndicatorDirection = true,
                    hitRange              = 1.3f,
                    indicators = new List<AttackIndicatorEntry>
                    {
                        new AttackIndicatorEntry
                        {
                            shape      = AttackIndicatorShape.Sector,
                            angleOffset = -30f,
                            sectorData  = new SectorIndicatorData { range = 1.3f, angle = 90f }
                        }
                    }
                }
            };

            // phase2Sequence: 비워둠 (파트너 사망 → TwinPhantomsBossPresenter 에서 스탯 강화로 처리)
            so.phase2Sequence = new List<EnemyAttackAction>();

            so.maxHp           = 1500f;  // 2체이므로 총 HP = 3000
            so.attackPower     = 75f;
            so.moveSpeed       = 3f;
            so.attackRange     = 1.2f;
            so.postAttackDelay = 0.3f;
            so.phase2SpeedMultiplier  = 1f;   // 미사용 (TwinPhantomsBossPresenter 에서 처리)
            so.phase2AttackMultiplier = 1f;
            so.bossScale       = 2f;
            so.phase1Color     = new Color(0.7f, 0.7f, 1f);
            so.phase2Color     = new Color(0.7f, 0.7f, 1f);  // 파트너 사망 강화는 보라색으로 별도 처리

            SaveSO(so, "TwinPhantoms (Body)");
        }

        // ── 공통 유틸 ─────────────────────────────────────────────
        private static bool ValidateBossData(BossStatData so, string expectedName)
        {
            if (so == null)
            {
                Debug.LogError($"[BossDataSetup] Project 창에서 {expectedName} BossStatData SO 를 먼저 선택하세요.");
                return false;
            }
            return true;
        }

        private static void SaveSO(BossStatData so, string label)
        {
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BossDataSetup] {label} 설정 완료: {AssetDatabase.GetAssetPath(so)}");
        }
    }
}
#endif
