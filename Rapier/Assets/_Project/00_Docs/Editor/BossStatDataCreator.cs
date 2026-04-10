#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Game.Enemies;

namespace Game.Editor
{
    /// <summary>
    /// 보스 SO 에셋 및 BossRushDemo 씬 자동 조립 도우미.
    ///
    /// [변경 이력]
    ///   attackWindupDuration, attackHitDuration 필드 제거.
    ///   해당 값은 각 EnemyAttackAction(MeleeAttackAction 등)의
    ///   windupDuration 필드로 이전됨.
    /// </summary>
    public static class BossStatDataCreator
    {
        private const string SPRITE_BASE =
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/";

        // ── SO 에셋 생성 ──────────────────────────────────────────
        [MenuItem("Rapier/BossRush/Create Boss Stat Assets")]
        public static void CreateBossStatAssets()
        {
            CreateTitanData();
            CreateSpecterData();
            CreateStubData("PyromancerStatData", "Pyromancer");
            CreateStubData("BerserkerStatData",  "Berserker");
            CreateStubData("StormcallerStatData","Stormcaller");
            CreateStubData("GravekeeperStatData","Gravekeeper");
            CreateStubData("TwinPhantomsStatData","TwinPhantoms");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BossStatDataCreator] 보스 SO 7종 생성 완료. 각 SO 선택 후 Rapier/Dev/Boss Setup 메뉴로 밸런싱 적용.");
        }

        private static void CreateStubData(string fileName, string enemyName)
        {
            string path = $"Assets/_Project/30_ScriptableObjects/Enemies/Boss/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<BossStatData>(path) != null)
            {
                Debug.LogWarning($"[BossStatDataCreator] {fileName} 이미 존재.");
                return;
            }
            var data = ScriptableObject.CreateInstance<BossStatData>();
            data.enemyName = enemyName;
            AssetDatabase.CreateAsset(data, path);
        }

        private static void CreateTitanData()
        {
            const string path = "Assets/_Project/30_ScriptableObjects/Enemies/Boss/TitanStatData.asset";
            if (AssetDatabase.LoadAssetAtPath<BossStatData>(path) != null)
            {
                Debug.LogWarning("[BossStatDataCreator] TitanStatData 이미 존재.");
                return;
            }

            var hexSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "HexagonFlatTop.png");

            var data = ScriptableObject.CreateInstance<BossStatData>();
            data.enemyName               = "Titan";
            data.maxHp                   = 1200f;
            data.attackPower             = 80f;
            data.moveSpeed               = 1.8f;
            data.attackRange             = 2.0f;
            data.postAttackDelay         = 0.8f;
            data.approachAngleVariance   = 10f;
            data.bossScale               = 2.5f;
            data.phase1Color             = new Color(0.85f, 0.15f, 0.15f);
            data.phase2Color             = new Color(1.0f,  0.40f, 0.0f);
            data.phase2SpeedMultiplier   = 1.4f;
            data.phase2AttackMultiplier  = 1.5f;
            data.phaseTransitionDuration = 1.2f;
            if (hexSprite != null) data.sprite = hexSprite;

            // attackSequence / phase2Sequence 는 인스펙터에서 직접 설정
            AssetDatabase.CreateAsset(data, path);
        }

        private static void CreateSpecterData()
        {
            const string path = "Assets/_Project/30_ScriptableObjects/Enemies/Boss/SpecterStatData.asset";
            if (AssetDatabase.LoadAssetAtPath<BossStatData>(path) != null)
            {
                Debug.LogWarning("[BossStatDataCreator] SpecterStatData 이미 존재.");
                return;
            }

            var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Circle.png");

            var data = ScriptableObject.CreateInstance<BossStatData>();
            data.enemyName               = "Specter";
            data.maxHp                   = 600f;
            data.attackPower             = 50f;
            data.moveSpeed               = 5.5f;
            data.attackRange             = 1.4f;
            data.postAttackDelay         = 0.2f;
            data.approachAngleVariance   = 30f;
            data.bossScale               = 1.8f;
            data.phase1Color             = new Color(0.5f, 0.1f, 0.9f);
            data.phase2Color             = new Color(0.8f, 0.0f, 1.0f);
            data.phase2SpeedMultiplier   = 1.6f;
            data.phase2AttackMultiplier  = 1.2f;
            data.phaseTransitionDuration = 0.8f;
            if (circleSprite != null) data.sprite = circleSprite;

            // attackSequence / phase2Sequence 는 인스펙터에서 직접 설정
            AssetDatabase.CreateAsset(data, path);
        }

        // ── BossRushDemo 씬 조립 ──────────────────────────────────
        [MenuItem("Rapier/BossRush/Setup BossRushDemo Scene")]
        public static void SetupBossRushScene()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.name.Contains("BossRushDemo"))
            {
                Debug.LogError("[BossStatDataCreator] BossRushDemo 씬을 먼저 열어주세요.");
                return;
            }

            // ── Global Light 2D (중복 가드) ───────────────────────
            if (GameObject.Find("Global Light 2D") != null)
                Debug.Log("[BossStatDataCreator] Global Light 2D 이미 존재 — 스킵.");
            else
            {
                var lightGo = new GameObject("Global Light 2D");
                Undo.RegisterCreatedObjectUndo(lightGo, "Create Global Light");
                var light2D = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
                light2D.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
                light2D.intensity = 1f;
            }

            // ── Main Camera (중복 가드) ───────────────────────────
            if (GameObject.Find("Main Camera") != null)
                Debug.Log("[BossStatDataCreator] Main Camera 이미 존재 — 스킵.");
            else
            {
                var camGo  = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
                camGo.tag  = "MainCamera";
                var cam    = camGo.AddComponent<Camera>();
                cam.orthographic     = true;
                cam.orthographicSize = 8f;
                cam.backgroundColor  = new Color(0.05f, 0.05f, 0.08f);
                cam.clearFlags       = CameraClearFlags.SolidColor;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                camGo.AddComponent<Game.Core.CameraFollow>();
                camGo.transform.position = new Vector3(0, 0, -10);
            }

            // ── InputManager (중복 가드) ──────────────────────────
            if (GameObject.Find("InputManager") != null)
                Debug.Log("[BossStatDataCreator] InputManager 이미 존재 — 스킵.");
            else
            {
                var inputGo = new GameObject("InputManager");
                Undo.RegisterCreatedObjectUndo(inputGo, "Create InputManager");
                inputGo.AddComponent<Game.Input.GestureRecognizer>();
                inputGo.AddComponent<Game.Core.InputSystemInitializer>();
            }

            // ── Stage (중복 가드) ─────────────────────────────────
            if (GameObject.Find("Stage") != null)
                Debug.Log("[BossStatDataCreator] Stage 이미 존재 — 스킵.");
            else
            {
                var stageGo = new GameObject("Stage");
                Undo.RegisterCreatedObjectUndo(stageGo, "Create Stage");
                stageGo.AddComponent<Game.Core.StageBuilder>();
            }

            // ── VirtualJoystick (중복 가드) ───────────────────────
            if (GameObject.Find("VirtualJoystick") != null)
                Debug.Log("[BossStatDataCreator] VirtualJoystick 이미 존재 — 스킵.");
            else
            {
                var vjGo = new GameObject("VirtualJoystick");
                Undo.RegisterCreatedObjectUndo(vjGo, "Create VirtualJoystick");
                vjGo.AddComponent<Game.UI.VirtualJoystick>();
            }

            // ── Player (중복 가드) ────────────────────────────────
            if (GameObject.Find("Player") != null)
                Debug.Log("[BossStatDataCreator] Player 이미 존재 — 스킵.");
            else
            {
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Rapier_Player.prefab");
                GameObject playerGo;
                if (playerPrefab != null)
                {
                    playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                    Undo.RegisterCreatedObjectUndo(playerGo, "Create Player");
                    playerGo.name = "Player";
                }
                else
                {
                    Debug.LogWarning("[BossStatDataCreator] Rapier_Player.prefab 없음. 빈 오브젝트 생성.");
                    playerGo = new GameObject("Player");
                    Undo.RegisterCreatedObjectUndo(playerGo, "Create Player");
                }
            }

            // ── BossRushManager (중복 가드) ───────────────────────
            if (GameObject.Find("BossRushManager") != null)
                Debug.Log("[BossStatDataCreator] BossRushManager 이미 존재 — 스킵. 7종 보스 배선은 재실행하지 않음.");
            else
            {
                var bossManagerGo = new GameObject("BossRushManager");
                Undo.RegisterCreatedObjectUndo(bossManagerGo, "Create BossRushManager");
                var bossManager = bossManagerGo.AddComponent<Game.Enemies.BossRushManager>();

                WireBosses(bossManager);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[BossStatDataCreator] BossRushDemo 씬 기본 오브젝트 배치 완료!");
            Debug.Log("  다음 단계: Rapier/BossRush/Create Boss Rush HUD 메뉴 실행 후 BossRushManager에 HudView 연결");
        }

        // ── 7종 보스 배선 ─────────────────────────────────────────
        /// <summary>
        /// BossRushManager._bossPrefabs / _bossStatDatas 에
        /// Titan → Specter → Berserker → Gravekeeper → Pyromancer → Stormcaller → TwinPhantoms
        /// 순서로 7개 주입한다.
        ///
        /// 신규 5종 프리팹이 아직 없으면 null-safe 하게 경고 로그만 남기고 계속 진행.
        /// </summary>
        private static void WireBosses(Game.Enemies.BossRushManager bossManager)
        {
            // ── 프리팹 로드 ────────────────────────────────────────
            var titanPrefab        = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Titan_Boss.prefab");
            var specterPrefab      = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Specter_Boss.prefab");
            var berserkerPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Berserker_Boss.prefab");
            var gravekeeperPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Gravekeeper_Boss.prefab");
            var pyromancerPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Pyromancer_Boss.prefab");
            var stormcallerPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Stormcaller_Boss.prefab");
            var twinPhantomsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/TwinPhantoms_Boss.prefab");

            // ── StatData 로드 ──────────────────────────────────────
            var titanData        = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/TitanStatData.asset");
            var specterData      = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/SpecterStatData.asset");
            var berserkerData    = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/BerserkerStatData.asset");
            var gravekeeperData  = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/GravekeeperStatData.asset");
            var pyromancerData   = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/PyromancerStatData.asset");
            var stormcallerData  = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/StormcallerStatData.asset");
            var twinPhantomsData = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/TwinPhantomsStatData.asset");

            // ── null 경고 ──────────────────────────────────────────
            LogMissingWarning("Titan_Boss.prefab",       titanPrefab);
            LogMissingWarning("Specter_Boss.prefab",     specterPrefab);
            LogMissingWarning("Berserker_Boss.prefab",   berserkerPrefab);
            LogMissingWarning("Gravekeeper_Boss.prefab", gravekeeperPrefab);
            LogMissingWarning("Pyromancer_Boss.prefab",  pyromancerPrefab);
            LogMissingWarning("Stormcaller_Boss.prefab", stormcallerPrefab);
            LogMissingWarning("TwinPhantoms_Boss.prefab",twinPhantomsPrefab);

            LogMissingWarning("TitanStatData.asset",        titanData);
            LogMissingWarning("SpecterStatData.asset",      specterData);
            LogMissingWarning("BerserkerStatData.asset",    berserkerData);
            LogMissingWarning("GravekeeperStatData.asset",  gravekeeperData);
            LogMissingWarning("PyromancerStatData.asset",   pyromancerData);
            LogMissingWarning("StormcallerStatData.asset",  stormcallerData);
            LogMissingWarning("TwinPhantomsStatData.asset", twinPhantomsData);

            // ── 리플렉션으로 private 배열 주입 ────────────────────
            var bmType = typeof(Game.Enemies.BossRushManager);
            var flags  = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            bmType.GetField("_bossPrefabs", flags)?.SetValue(bossManager,
                new GameObject[]
                {
                    titanPrefab,
                    specterPrefab,
                    berserkerPrefab,
                    gravekeeperPrefab,
                    pyromancerPrefab,
                    stormcallerPrefab,
                    twinPhantomsPrefab
                });

            bmType.GetField("_bossStatDatas", flags)?.SetValue(bossManager,
                new BossStatData[]
                {
                    titanData,
                    specterData,
                    berserkerData,
                    gravekeeperData,
                    pyromancerData,
                    stormcallerData,
                    twinPhantomsData
                });

            EditorUtility.SetDirty(bossManager);
            Debug.Log("[BossStatDataCreator] BossRushManager 7종 배선 완료 " +
                      "(Titan→Specter→Berserker→Gravekeeper→Pyromancer→Stormcaller→TwinPhantoms).");
        }

        private static void LogMissingWarning(string assetName, Object asset)
        {
            if (asset == null)
                Debug.LogWarning($"[BossStatDataCreator] {assetName} 없음 — null로 배선됩니다. " +
                                 "Rapier/BossRush/Create New Boss Prefabs 를 먼저 실행하세요.");
        }
    }
}
#endif
