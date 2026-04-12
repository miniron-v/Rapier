#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Enemies.Editor
{
    /// <summary>
    /// 보스 SO phases 초기값 설정 스크립트.
    /// 각 보스 SO를 선택한 후 해당 메뉴를 실행하면 phases 가 덮어써진다.
    ///
    /// 메뉴 경로: Rapier/Dev/Boss Setup/[보스명]
    /// </summary>
    public static class BossDataSetup
    {
        // ── Specter 밸런싱 ────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Specter — 밸런싱 적용")]
        private static void SetupSpecterSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Specter")) return;

            so.phases = new List<PhaseEntry>
            {
                // [Phase1] Melee 단독 (빠른 압박)
                new PhaseEntry
                {
                    hpThreshold = 1f,
                    color = new Color(0.9f, 0.3f, 0.3f),
                    speedMultiplier = 1f,
                    attackMultiplier = 1f,
                    sequence = new List<EnemyAttackAction>
                    {
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
                    }
                },
                // [Phase2] Teleport → Melee
                new PhaseEntry
                {
                    hpThreshold = 0.5f,
                    color = new Color(0.7f, 0.1f, 0.1f),
                    speedMultiplier = 1.4f,
                    attackMultiplier = 1.3f,
                    sequence = new List<EnemyAttackAction>
                    {
                        new TeleportAttackAction
                        {
                            teleportOffset = 1.3f,
                            fadeTime       = 0.2f,
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
                    }
                }
            };

            so.moveSpeed       = 4.5f;
            so.attackRange     = 1.2f;
            so.postAttackDelay = 0.25f;

            SaveSO(so, "Specter");
        }

        // ── Pyromancer ────────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Pyromancer — 초기값 설정")]
        private static void SetupPyromancerSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Pyromancer")) return;

            so.phases = new List<PhaseEntry>
            {
                // [Phase1] Projectile → GroundHazard
                new PhaseEntry
                {
                    hpThreshold = 1f,
                    color = new Color(1f, 0.4f, 0.1f),
                    speedMultiplier = 1f,
                    attackMultiplier = 1f,
                    sequence = new List<EnemyAttackAction>
                    {
                        new ProjectileAttackAction
                        {
                            windupDuration      = 0.7f,
                            projectileSpeed     = 7f,
                            maxRange            = 12f,
                            hitRadius           = 0.4f,
                            damagePercent       = 100,
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
                            windupDuration     = 0.8f,
                            duration           = 4f,
                            tickInterval       = 0.5f,
                            tickDamagePercent  = 30,
                            hazardRadius       = 1.5f
                        }
                    }
                },
                // [Phase2] Projectile → GroundHazard → MultiDirectional
                new PhaseEntry
                {
                    hpThreshold = 0.5f,
                    color = new Color(1f, 0.1f, 0.05f),
                    speedMultiplier = 1.3f,
                    attackMultiplier = 1.3f,
                    sequence = new List<EnemyAttackAction>
                    {
                        new ProjectileAttackAction
                        {
                            windupDuration      = 0.65f,
                            projectileSpeed     = 10f,
                            maxRange            = 15f,
                            hitRadius           = 0.4f,
                            damagePercent       = 120,
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
                            windupDuration     = 0.8f,
                            duration           = 5f,
                            tickInterval       = 0.4f,
                            tickDamagePercent  = 35,
                            hazardRadius       = 1.8f
                        },
                        new MultiDirectionalAttackAction
                        {
                            windupDuration      = 0.8f,
                            hitRange            = 2f,
                            damagePercent       = 120,
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
                    }
                }
            };

            so.maxHp           = 2000f;
            so.attackPower     = 80f;
            so.moveSpeed       = 1.2f;
            so.attackRange     = 8f;
            so.postAttackDelay = 0.5f;
            so.bossScale       = 2.5f;

            SaveSO(so, "Pyromancer");
        }

        // ── Berserker ─────────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Berserker — 초기값 설정")]
        private static void SetupBerserkerSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Berserker")) return;

            so.phases = new List<PhaseEntry>
            {
                // [Phase1] 3연타 Melee 콤보
                new PhaseEntry
                {
                    hpThreshold = 1f,
                    color = new Color(0.8f, 0.2f, 0.1f),
                    speedMultiplier = 1f,
                    attackMultiplier = 1f,
                    sequence = new List<EnemyAttackAction>
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
                                    angleOffset = 40f,
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
                    }
                },
                // [Phase2] 4연타 + Charge (광폭화)
                new PhaseEntry
                {
                    hpThreshold = 0.5f,
                    color = new Color(1f, 0.05f, 0.05f),
                    speedMultiplier = 1.8f,
                    attackMultiplier = 1.4f,
                    sequence = new List<EnemyAttackAction>
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
                            damagePercent           = 220,
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
                    }
                }
            };

            so.maxHp           = 2000f;
            so.attackPower     = 90f;
            so.moveSpeed       = 3.5f;
            so.attackRange     = 1.3f;
            so.postAttackDelay = 0.2f;
            so.bossScale       = 2.2f;

            SaveSO(so, "Berserker");
        }

        // ── Stormcaller ───────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Stormcaller — 초기값 설정")]
        private static void SetupStormcallerSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Stormcaller")) return;

            so.phases = new List<PhaseEntry>
            {
                // [Phase1] MultiDirectional (4방향, +형) → Projectile
                new PhaseEntry
                {
                    hpThreshold = 1f,
                    color = new Color(0.4f, 0.7f, 1f),
                    speedMultiplier = 1f,
                    attackMultiplier = 1f,
                    sequence = new List<EnemyAttackAction>
                    {
                        new MultiDirectionalAttackAction
                        {
                            windupDuration      = 0.9f,
                            hitRange            = 2f,
                            damagePercent       = 100,
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
                            damagePercent       = 100,
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
                    }
                },
                // [Phase2] MultiDirectional(4방향) → Projectile(유도)
                new PhaseEntry
                {
                    hpThreshold = 0.5f,
                    color = new Color(0.2f, 0.4f, 1f),
                    speedMultiplier = 1.4f,
                    attackMultiplier = 1.3f,
                    sequence = new List<EnemyAttackAction>
                    {
                        new MultiDirectionalAttackAction
                        {
                            windupDuration      = 0.85f,
                            hitRange            = 2f,
                            damagePercent       = 110,
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
                            damagePercent       = 120,
                            homingStrength      = 45f,
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
                    }
                },
                // [Phase3] 8방향 MultiDirectional → 유도 Projectile
                new PhaseEntry
                {
                    hpThreshold = 0.25f,
                    color = new Color(0.1f, 0.2f, 0.9f),
                    speedMultiplier = 1.6f,
                    attackMultiplier = 1.5f,
                    sequence = new List<EnemyAttackAction>
                    {
                        new MultiDirectionalAttackAction
                        {
                            windupDuration      = 0.8f,
                            hitRange            = 2.5f,
                            damagePercent       = 130,
                            lockIndicatorDirection = true,
                            indicators = new List<AttackIndicatorEntry>
                            {
                                new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 0f,    sectorData = new SectorIndicatorData { range = 3f, angle = 45f } },
                                new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 45f,   sectorData = new SectorIndicatorData { range = 3f, angle = 45f } },
                                new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 90f,   sectorData = new SectorIndicatorData { range = 3f, angle = 45f } },
                                new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 135f,  sectorData = new SectorIndicatorData { range = 3f, angle = 45f } },
                                new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 180f,  sectorData = new SectorIndicatorData { range = 3f, angle = 45f } },
                                new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 225f,  sectorData = new SectorIndicatorData { range = 3f, angle = 45f } },
                                new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 270f,  sectorData = new SectorIndicatorData { range = 3f, angle = 45f } },
                                new AttackIndicatorEntry { shape = AttackIndicatorShape.Sector, angleOffset = 315f,  sectorData = new SectorIndicatorData { range = 3f, angle = 45f } }
                            }
                        },
                        new ProjectileAttackAction
                        {
                            windupDuration      = 0.6f,
                            projectileSpeed     = 12f,
                            maxRange            = 20f,
                            hitRadius           = 0.5f,
                            damagePercent       = 140,
                            homingStrength      = 60f,
                            lockIndicatorDirection = true,
                            indicators = new List<AttackIndicatorEntry>
                            {
                                new AttackIndicatorEntry
                                {
                                    shape    = AttackIndicatorShape.Rectangle,
                                    angleOffset = 0f,
                                    rectData = new RectIndicatorData { range = 15f, width = 1f }
                                }
                            }
                        }
                    }
                }
            };

            so.maxHp           = 2200f;
            so.attackPower     = 80f;
            so.moveSpeed       = 1.8f;
            so.attackRange     = 6f;
            so.postAttackDelay = 0.5f;
            so.bossScale       = 2.5f;

            SaveSO(so, "Stormcaller");
        }

        // ── Gravekeeper ───────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/Gravekeeper — 초기값 설정")]
        private static void SetupGravekeeperSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "Gravekeeper")) return;

            so.phases = new List<PhaseEntry>
            {
                // [Phase1] Summon → Melee
                new PhaseEntry
                {
                    hpThreshold = 1f,
                    color = new Color(0.3f, 0.6f, 0.3f),
                    speedMultiplier = 1f,
                    attackMultiplier = 1f,
                    sequence = new List<EnemyAttackAction>
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
                    }
                },
                // [Phase2] Summon → Summon → Melee → Melee
                new PhaseEntry
                {
                    hpThreshold = 0.5f,
                    color = new Color(0.1f, 0.9f, 0.3f),
                    speedMultiplier = 1.3f,
                    attackMultiplier = 1.25f,
                    sequence = new List<EnemyAttackAction>
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
                    }
                }
            };

            so.maxHp           = 2000f;
            so.attackPower     = 75f;
            so.moveSpeed       = 1.5f;
            so.attackRange     = 1.3f;
            so.postAttackDelay = 0.4f;
            so.bossScale       = 2.5f;

            SaveSO(so, "Gravekeeper");
        }

        // ── Twin Phantoms ─────────────────────────────────────────
        [MenuItem("Rapier/Dev/Boss Setup/TwinPhantoms — 초기값 설정 (Body A)")]
        private static void SetupTwinPhantomsSequence()
        {
            var so = Selection.activeObject as BossStatData;
            if (!ValidateBossData(so, "TwinPhantoms")) return;

            // 1페이즈만. 파트너 사망으로 강화하므로 HP 기반 전환 없음.
            so.phases = new List<PhaseEntry>
            {
                new PhaseEntry
                {
                    hpThreshold = 1f,
                    color = new Color(0.7f, 0.7f, 1f),
                    speedMultiplier = 1f,
                    attackMultiplier = 1f,
                    sequence = new List<EnemyAttackAction>
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
                    }
                }
            };

            so.maxHp           = 1500f;
            so.attackPower     = 75f;
            so.moveSpeed       = 3f;
            so.attackRange     = 1.2f;
            so.postAttackDelay = 0.3f;
            so.bossScale       = 2f;

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
