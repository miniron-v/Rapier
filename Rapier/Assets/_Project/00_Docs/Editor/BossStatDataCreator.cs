#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Game.Enemies;

namespace Game.Editor
{
    /// <summary>
    /// 보스 SO 에셋 및 BossRushDemo 씬 자동 조립 도우미.
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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BossStatDataCreator] TitanStatData, SpecterStatData 생성 완료!");
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
            data.attackWindupDuration    = 0.6f;
            data.attackHitDuration       = 0.05f;
            data.postAttackDelay         = 0.8f;
            data.approachAngleVariance   = 10f;
            data.bossScale               = 2.5f;
            data.phase1Color             = new Color(0.85f, 0.15f, 0.15f);
            data.phase2Color             = new Color(1.0f,  0.40f, 0.0f);
            data.phase2SpeedMultiplier   = 1.4f;
            data.phase2AttackMultiplier  = 1.5f;
            data.phaseTransitionDuration = 1.2f;
            if (hexSprite != null) data.sprite = hexSprite;

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
            data.attackWindupDuration    = 0.25f;
            data.attackHitDuration       = 0.05f;
            data.postAttackDelay         = 0.2f;
            data.approachAngleVariance   = 30f;
            data.bossScale               = 1.8f;
            data.phase1Color             = new Color(0.5f, 0.1f, 0.9f);
            data.phase2Color             = new Color(0.8f, 0.0f, 1.0f);
            data.phase2SpeedMultiplier   = 1.6f;
            data.phase2AttackMultiplier  = 1.2f;
            data.phaseTransitionDuration = 0.8f;
            if (circleSprite != null) data.sprite = circleSprite;

            AssetDatabase.CreateAsset(data, path);
        }

        // ── BossRushDemo 씬 조립 ──────────────────────────────────
        [MenuItem("Rapier/BossRush/Setup BossRushDemo Scene")]
        public static void SetupBossRushScene()
        {
            // 현재 씬이 BossRushDemo인지 확인
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.name.Contains("BossRushDemo"))
            {
                Debug.LogError("[BossStatDataCreator] BossRushDemo 씬을 먼저 열어주세요.");
                return;
            }

            // ── Global Light 2D ───────────────────────────────────
            var lightGo = new GameObject("Global Light 2D");
            Undo.RegisterCreatedObjectUndo(lightGo, "Create Global Light");
            var light2D = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light2D.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            light2D.intensity = 1f;

            // ── Main Camera ───────────────────────────────────────
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

            // ── InputManager ──────────────────────────────────────
            var inputGo = new GameObject("InputManager");
            Undo.RegisterCreatedObjectUndo(inputGo, "Create InputManager");
            inputGo.AddComponent<Game.Input.GestureRecognizer>();
            inputGo.AddComponent<Game.Core.InputSystemInitializer>();

            // ── Stage ─────────────────────────────────────────────
            var stageGo = new GameObject("Stage");
            Undo.RegisterCreatedObjectUndo(stageGo, "Create Stage");
            stageGo.AddComponent<Game.Core.StageBuilder>();

            // ── VirtualJoystick ───────────────────────────────────
            var vjPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Rapier_Player.prefab");
            // VirtualJoystick은 씬에 직접 GameObject로 생성
            var vjGo = new GameObject("VirtualJoystick");
            Undo.RegisterCreatedObjectUndo(vjGo, "Create VirtualJoystick");
            vjGo.AddComponent<Game.UI.VirtualJoystick>();

            // ── Player ────────────────────────────────────────────
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

            // ── BossRushManager ───────────────────────────────────
            var bossManagerGo = new GameObject("BossRushManager");
            Undo.RegisterCreatedObjectUndo(bossManagerGo, "Create BossRushManager");
            var bossManager = bossManagerGo.AddComponent<Game.Enemies.BossRushManager>();

            // 보스 프리팹/SO 연결
            var titanPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Titan_Boss.prefab");
            var specterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/20_Prefabs/Specter_Boss.prefab");
            var titanData     = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/TitanStatData.asset");
            var specterData   = AssetDatabase.LoadAssetAtPath<BossStatData>("Assets/_Project/30_ScriptableObjects/Enemies/Boss/SpecterStatData.asset");

            var bmType  = typeof(Game.Enemies.BossRushManager);
            var flags   = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            if (titanPrefab != null && specterPrefab != null)
                bmType.GetField("_bossPrefabs", flags)?.SetValue(bossManager,
                    new GameObject[] { titanPrefab, specterPrefab });

            if (titanData != null && specterData != null)
                bmType.GetField("_bossStatDatas", flags)?.SetValue(bossManager,
                    new BossStatData[] { titanData, specterData });

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[BossStatDataCreator] BossRushDemo 씬 기본 오브젝트 배치 완료!");
            Debug.Log("  다음 단계: Rapier/BossRush/Create Boss Rush HUD 메뉴 실행 후 BossRushManager에 HudView 연결");
        }
    }
}
#endif
