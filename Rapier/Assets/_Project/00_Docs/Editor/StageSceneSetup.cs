#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.Rendering.Universal;
using Game.Core;
using Game.Core.Stage;
using Game.Input;
using Game.Enemies;
using Game.UI;
using Game.UI.Intermission;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Game.Editor
{
    /// <summary>
    /// Phase 12-D 스테이지 데모 씬 자동 생성 도우미.
    ///
    /// [생성 구성]
    ///   Global Light 2D — URP 2D 전역 조명
    ///   InputManager    — GestureRecognizer + InputSystemInitializer
    ///   VirtualJoystick — 가상 조이스틱 (런타임에 자체 Canvas 생성)
    ///   [Core]          — StageManager + ProgressionManager + StageBuilder
    ///   Player          — Rapier_Player 프리팹
    ///   Main Camera     — Camera + CameraFollow (_target = Player)
    ///   [UI] Canvas     — IntermissionManager + IntermissionView + DeathPopupView + StageClearView
    ///   BossRushHudCanvas — BossRushHudSetup.CreateHud() 재사용
    ///   EventSystem
    ///
    /// [RoomNode 배열 (8방)]
    ///   [0] IntermissionRoom  (시작 스탯 선택)
    ///   [1] BossRoom          Specter
    ///   [2] IntermissionRoom
    ///   [3] BossRoom          Berserker
    ///   [4] IntermissionRoom
    ///   [5] BossRoom          Gravekeeper
    ///   [6] IntermissionRoom
    ///   [7] BossRoom          Titan
    ///
    /// [실행]
    ///   Rapier/Stage/Create Stage Demo Scene
    ///   Rapier/Stage/Rebuild Stage Demo Scene
    /// </summary>
    public static class StageSceneSetup
    {
        // ── 에셋 경로 ─────────────────────────────────────────────
        private const string FONT_ASSET_PATH =
            "Assets/_Project/30_ScriptableObjects/Fonts/NEXONLv1Gothic Regular SDF.asset";
        private const string SCENE_SAVE_PATH =
            "Assets/_Project/40_Scenes/StageDemo.unity";

        private const string PLAYER_PREFAB_PATH =
            "Assets/_Project/20_Prefabs/Rapier_Player.prefab";

        private const string SPECTER_PREFAB_PATH     = "Assets/_Project/20_Prefabs/Specter_Boss.prefab";
        private const string BERSERKER_PREFAB_PATH   = "Assets/_Project/20_Prefabs/Berserker_Boss.prefab";
        private const string GRAVEKEEPER_PREFAB_PATH = "Assets/_Project/20_Prefabs/Gravekeeper_Boss.prefab";
        private const string TITAN_PREFAB_PATH       = "Assets/_Project/20_Prefabs/Titan_Boss.prefab";

        private const string SPECTER_STAT_PATH     =
            "Assets/_Project/30_ScriptableObjects/Enemies/Boss/SpecterStatData.asset";
        private const string BERSERKER_STAT_PATH   =
            "Assets/_Project/30_ScriptableObjects/Enemies/Boss/BerserkerStatData.asset";
        private const string GRAVEKEEPER_STAT_PATH =
            "Assets/_Project/30_ScriptableObjects/Enemies/Boss/GravekeeperStatData.asset";
        private const string TITAN_STAT_PATH       =
            "Assets/_Project/30_ScriptableObjects/Enemies/Boss/TitanStatData.asset";

        // ── 폰트 캐시 ─────────────────────────────────────────────
        private static TMP_FontAsset _font;
        private static TMP_FontAsset GetFont()
        {
            if (_font == null)
                _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
            return _font;
        }

        // ── 색상 팔레트 ───────────────────────────────────────────
        private static readonly Color OVERLAY_BG    = new Color(0.00f, 0.00f, 0.00f, 0.80f);
        private static readonly Color CARD_BG       = new Color(0.12f, 0.18f, 0.28f, 0.95f);
        private static readonly Color BTN_PRIMARY   = new Color(0.90f, 0.75f, 0.10f, 1.00f);
        private static readonly Color BTN_DANGER    = new Color(0.85f, 0.20f, 0.15f, 1.00f);
        private static readonly Color BTN_LOBBY     = new Color(0.30f, 0.55f, 0.90f, 1.00f);
        private static readonly Color BTN_SAFE      = new Color(0.20f, 0.70f, 0.35f, 1.00f);
        private static readonly Color BTN_TEXT_DARK = new Color(0.08f, 0.04f, 0.00f, 1.00f);

        [MenuItem("Rapier/Stage/Create Stage Demo Scene")]
        public static void CreateStageScene() => BuildScene(false);

        [MenuItem("Rapier/Stage/Rebuild Stage Demo Scene")]
        public static void RebuildStageScene() => BuildScene(true);

        private static void BuildScene(bool forceRebuild)
        {
            _font = null;
            Debug.Log($"[StageSceneSetup] Font={GetFont() != null}");

            var existingScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (existingScene.name == "StageDemo" && !forceRebuild)
            {
                Debug.LogWarning("[StageSceneSetup] StageDemo 씬이 이미 활성화됩니다. Rebuild를 사용하세요.");
                return;
            }

            // 새 빈 씬
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Global Light 2D ───────────────────────────────────
            var lightGo = new GameObject("Global Light 2D");
            Undo.RegisterCreatedObjectUndo(lightGo, "Create Global Light 2D");
            var light2D = lightGo.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.intensity = 1f;

            // ── EventSystem ───────────────────────────────────────
            EnsureEventSystem();

            // ── InputManager ──────────────────────────────────────
            var inputManagerGo = new GameObject("InputManager");
            Undo.RegisterCreatedObjectUndo(inputManagerGo, "Create InputManager");
            inputManagerGo.AddComponent<GestureRecognizer>();
            inputManagerGo.AddComponent<InputSystemInitializer>();

            // ── VirtualJoystick ───────────────────────────────────
            var vjGo = new GameObject("VirtualJoystick");
            Undo.RegisterCreatedObjectUndo(vjGo, "Create VirtualJoystick");
            vjGo.AddComponent<VirtualJoystick>();

            // ── [Core] ────────────────────────────────────────────
            var coreGo = new GameObject("[Core]");
            Undo.RegisterCreatedObjectUndo(coreGo, "Create [Core]");
            var stageManager       = coreGo.AddComponent<StageManager>();
            var progressionManager = coreGo.AddComponent<ProgressionManager>();
            var stageBuilder       = coreGo.AddComponent<StageBuilder>();

            // ── Player ────────────────────────────────────────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            GameObject playerGo = null;
            if (playerPrefab != null)
            {
                playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                Undo.RegisterCreatedObjectUndo(playerGo, "Create Player");
                playerGo.name = "Player";
                playerGo.transform.position = new Vector3(0f, -3f, 0f);
            }
            else
            {
                Debug.LogWarning($"[StageSceneSetup] Player 프리팹 없음: {PLAYER_PREFAB_PATH}");
            }

            // ── Main Camera ───────────────────────────────────────
            var cameraGo = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(cameraGo, "Create Main Camera");
            cameraGo.tag = "MainCamera";
            var cam              = cameraGo.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 10f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.10f, 0.10f, 0.15f, 1f);
            cam.nearClipPlane    = -100f;
            cam.farClipPlane     = 100f;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var camFollow = cameraGo.AddComponent<CameraFollow>();
            if (playerGo != null)
            {
                var camSo = new SerializedObject(camFollow);
                camSo.FindProperty("_target").objectReferenceValue = playerGo.transform;
                camSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(camFollow);
            }

            // ── [UI] Canvas ───────────────────────────────────────
            var canvasGo = new GameObject("[UI]");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create [UI]");
            var canvas          = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler                 = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var intermissionManager = canvasGo.AddComponent<IntermissionManager>();

            // ── IntermissionView ──────────────────────────────────
            var ivGo = new GameObject("IntermissionPanel");
            ivGo.transform.SetParent(canvasGo.transform, false);
            SetFullStretch(ivGo);
            var intermissionView = ivGo.AddComponent<IntermissionView>();

            var ivPanel = CreatePanel(ivGo.transform, "Panel", OVERLAY_BG);
            ivPanel.SetActive(false);
            SetFullStretch(ivPanel);

            var healNotice   = CreateTMPText(ivPanel.transform, "HealNoticeText",
                "HP가 100% 회복되었습니다!", 34, FontStyles.Normal, new Color(0.5f, 1f, 0.5f));
            var healRt       = healNotice.GetComponent<RectTransform>();
            healRt.anchorMin = new Vector2(0f, 0.75f);
            healRt.anchorMax = new Vector2(1f, 0.85f);
            healRt.offsetMin = healRt.offsetMax = Vector2.zero;
            healNotice.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            var cardAGo = CreateCard(ivPanel.transform, "CardA", "스탯 A", "설명 A");
            var cardARt = cardAGo.GetComponent<RectTransform>();
            cardARt.anchorMin = new Vector2(0.05f, 0.35f);
            cardARt.anchorMax = new Vector2(0.47f, 0.72f);
            cardARt.offsetMin = cardARt.offsetMax = Vector2.zero;

            var cardBGo = CreateCard(ivPanel.transform, "CardB", "스탯 B", "설명 B");
            var cardBRt = cardBGo.GetComponent<RectTransform>();
            cardBRt.anchorMin = new Vector2(0.53f, 0.35f);
            cardBRt.anchorMax = new Vector2(0.95f, 0.72f);
            cardBRt.offsetMin = cardBRt.offsetMax = Vector2.zero;

            var ivSo = new SerializedObject(intermissionView);
            ivSo.FindProperty("_panel").objectReferenceValue               = ivPanel;
            ivSo.FindProperty("_cardAButton").objectReferenceValue         = cardAGo.GetComponent<Button>();
            ivSo.FindProperty("_cardATitle").objectReferenceValue          = cardAGo.transform.Find("Title").GetComponent<TextMeshProUGUI>();
            ivSo.FindProperty("_cardADesc").objectReferenceValue           = cardAGo.transform.Find("Desc").GetComponent<TextMeshProUGUI>();
            ivSo.FindProperty("_cardBButton").objectReferenceValue         = cardBGo.GetComponent<Button>();
            ivSo.FindProperty("_cardBTitle").objectReferenceValue          = cardBGo.transform.Find("Title").GetComponent<TextMeshProUGUI>();
            ivSo.FindProperty("_cardBDesc").objectReferenceValue           = cardBGo.transform.Find("Desc").GetComponent<TextMeshProUGUI>();
            ivSo.FindProperty("_healNoticeText").objectReferenceValue      = healNotice.GetComponent<TextMeshProUGUI>();
            ivSo.ApplyModifiedProperties();

            // ── DeathPopupView ────────────────────────────────────
            var dpGo = new GameObject("DeathPopupPanel");
            dpGo.transform.SetParent(canvasGo.transform, false);
            SetFullStretch(dpGo);
            var deathPopupView = dpGo.AddComponent<DeathPopupView>();

            var dpPanel = CreatePanel(dpGo.transform, "Panel", OVERLAY_BG);
            dpPanel.SetActive(false);
            SetFullStretch(dpPanel);

            var dpTitle   = CreateTMPText(dpPanel.transform, "TitleText",
                "전투 불능!", 72, FontStyles.Bold, Color.red);
            var dpTitleRt = dpTitle.GetComponent<RectTransform>();
            dpTitleRt.anchorMin = new Vector2(0f, 0.55f);
            dpTitleRt.anchorMax = new Vector2(1f, 0.70f);
            dpTitleRt.offsetMin = dpTitleRt.offsetMax = Vector2.zero;
            dpTitle.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            var dpContinueBtn = CreateButton(dpPanel.transform, "ContinueButton",
                "이어하기", 44, BTN_SAFE, Color.white);
            var dpContinueRt  = dpContinueBtn.GetComponent<RectTransform>();
            dpContinueRt.anchorMin        = new Vector2(0.10f, 0.35f);
            dpContinueRt.anchorMax        = new Vector2(0.90f, 0.35f);
            dpContinueRt.pivot            = new Vector2(0.5f, 1f);
            dpContinueRt.sizeDelta        = new Vector2(0f, 120f);
            dpContinueRt.anchoredPosition = Vector2.zero;

            var dpLobbyBtn = CreateButton(dpPanel.transform, "ReturnToLobbyButton",
                "로비로 돌아가기", 40, BTN_DANGER, Color.white);
            var dpLobbyRt  = dpLobbyBtn.GetComponent<RectTransform>();
            dpLobbyRt.anchorMin        = new Vector2(0.10f, 0.20f);
            dpLobbyRt.anchorMax        = new Vector2(0.90f, 0.20f);
            dpLobbyRt.pivot            = new Vector2(0.5f, 1f);
            dpLobbyRt.sizeDelta        = new Vector2(0f, 110f);
            dpLobbyRt.anchoredPosition = Vector2.zero;

            var dpSo = new SerializedObject(deathPopupView);
            dpSo.FindProperty("_panel").objectReferenceValue               = dpPanel;
            dpSo.FindProperty("_titleText").objectReferenceValue           = dpTitle.GetComponent<TextMeshProUGUI>();
            dpSo.FindProperty("_continueButton").objectReferenceValue      = dpContinueBtn.GetComponent<Button>();
            dpSo.FindProperty("_returnToLobbyButton").objectReferenceValue = dpLobbyBtn.GetComponent<Button>();
            dpSo.ApplyModifiedProperties();

            // ── StageClearView ────────────────────────────────────
            var scGo = new GameObject("StageClearPanel");
            scGo.transform.SetParent(canvasGo.transform, false);
            SetFullStretch(scGo);
            var stageClearView = scGo.AddComponent<StageClearView>();

            var scPanel = CreatePanel(scGo.transform, "Panel", OVERLAY_BG);
            scPanel.SetActive(false);
            SetFullStretch(scPanel);

            var scTitle   = CreateTMPText(scPanel.transform, "TitleText",
                "STAGE CLEAR!", 80, FontStyles.Bold, Color.yellow);
            var scTitleRt = scTitle.GetComponent<RectTransform>();
            scTitleRt.anchorMin = new Vector2(0f, 0.55f);
            scTitleRt.anchorMax = new Vector2(1f, 0.70f);
            scTitleRt.offsetMin = scTitleRt.offsetMax = Vector2.zero;
            scTitle.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            var scLobbyBtn = CreateButton(scPanel.transform, "ReturnToLobbyButton",
                "로비로", 44, BTN_LOBBY, Color.white);
            var scLobbyRt  = scLobbyBtn.GetComponent<RectTransform>();
            scLobbyRt.anchorMin        = new Vector2(0.10f, 0.30f);
            scLobbyRt.anchorMax        = new Vector2(0.90f, 0.30f);
            scLobbyRt.pivot            = new Vector2(0.5f, 1f);
            scLobbyRt.sizeDelta        = new Vector2(0f, 120f);
            scLobbyRt.anchoredPosition = Vector2.zero;

            var scNextBtn = CreateButton(scPanel.transform, "NextStageButton",
                "다음 스테이지", 44, BTN_PRIMARY, BTN_TEXT_DARK);
            var scNextRt  = scNextBtn.GetComponent<RectTransform>();
            scNextRt.anchorMin        = new Vector2(0.10f, 0.45f);
            scNextRt.anchorMax        = new Vector2(0.90f, 0.45f);
            scNextRt.pivot            = new Vector2(0.5f, 1f);
            scNextRt.sizeDelta        = new Vector2(0f, 120f);
            scNextRt.anchoredPosition = Vector2.zero;

            var scSo = new SerializedObject(stageClearView);
            scSo.FindProperty("_panel").objectReferenceValue               = scPanel;
            scSo.FindProperty("_titleText").objectReferenceValue           = scTitle.GetComponent<TextMeshProUGUI>();
            scSo.FindProperty("_returnToLobbyButton").objectReferenceValue = scLobbyBtn.GetComponent<Button>();
            scSo.FindProperty("_nextStageButton").objectReferenceValue     = scNextBtn.GetComponent<Button>();
            scSo.ApplyModifiedProperties();

            // ── IntermissionManager 배선 ──────────────────────────
            var imSo = new SerializedObject(intermissionManager);
            imSo.FindProperty("_intermissionView").objectReferenceValue  = intermissionView;
            imSo.FindProperty("_deathPopupView").objectReferenceValue    = deathPopupView;
            imSo.FindProperty("_stageClearView").objectReferenceValue    = stageClearView;
            imSo.FindProperty("_stageManagerRef").objectReferenceValue   = stageManager;
            imSo.ApplyModifiedProperties();

            // ── ProgressionManager 배선 ───────────────────────────
            var pmSo = new SerializedObject(progressionManager);
            pmSo.FindProperty("_stageManager").objectReferenceValue        = stageManager;
            pmSo.FindProperty("_intermissionManager").objectReferenceValue = intermissionManager;
            pmSo.ApplyModifiedProperties();

            // ── StageBuilder 배선 + RoomNodes ─────────────────────
            var sbSo = new SerializedObject(stageBuilder);
            sbSo.FindProperty("_stageManager").objectReferenceValue = stageManager;
            SetupRoomNodes(sbSo);
            sbSo.ApplyModifiedProperties();

            // SetDirty
            EditorUtility.SetDirty(intermissionView);
            EditorUtility.SetDirty(deathPopupView);
            EditorUtility.SetDirty(stageClearView);
            EditorUtility.SetDirty(intermissionManager);
            EditorUtility.SetDirty(progressionManager);
            EditorUtility.SetDirty(stageBuilder);

            // ── 씬 저장 (BossRushHudSetup 호출 전에 경로 확정) ────
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SCENE_SAVE_PATH);

            // ── BossRushHudCanvas 생성 (재사용) ───────────────────
            // BossRushManager가 없으므로 HP 자동 업데이트는 Phase 12-E에서 연결.
            // 씬이 이미 StageDemo.unity 경로로 저장됐으므로 여기서 저장해도 동일 파일.
            BossRushHudSetup.CreateHud();

            Selection.activeGameObject = coreGo;

            Debug.Log("[StageSceneSetup] StageDemo.unity 생성 완료!\n" +
                      "  [0] IntermissionRoom → [1] Specter → [2] 인터미션 → " +
                      "[3] Berserker → [4] 인터미션 → [5] Gravekeeper → [6] 인터미션 → [7] Titan\n" +
                      "  주의: Build Settings에 StageDemo 씬을 추가하세요.");
        }

        // ── RoomNode 배열 설정 ────────────────────────────────────
        private static void SetupRoomNodes(SerializedObject sbSo)
        {
            var specterPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>(SPECTER_PREFAB_PATH);
            var berserkerPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(BERSERKER_PREFAB_PATH);
            var gravekeeperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GRAVEKEEPER_PREFAB_PATH);
            var titanPrefab       = AssetDatabase.LoadAssetAtPath<GameObject>(TITAN_PREFAB_PATH);

            var specterStat     = AssetDatabase.LoadAssetAtPath<BossStatData>(SPECTER_STAT_PATH);
            var berserkerStat   = AssetDatabase.LoadAssetAtPath<BossStatData>(BERSERKER_STAT_PATH);
            var gravekeeperStat = AssetDatabase.LoadAssetAtPath<BossStatData>(GRAVEKEEPER_STAT_PATH);
            var titanStat       = AssetDatabase.LoadAssetAtPath<BossStatData>(TITAN_STAT_PATH);

            LogAssetLoad("Specter 프리팹",     specterPrefab);
            LogAssetLoad("Berserker 프리팹",   berserkerPrefab);
            LogAssetLoad("Gravekeeper 프리팹", gravekeeperPrefab);
            LogAssetLoad("Titan 프리팹",       titanPrefab);
            LogAssetLoad("Specter StatData",     specterStat);
            LogAssetLoad("Berserker StatData",   berserkerStat);
            LogAssetLoad("Gravekeeper StatData", gravekeeperStat);
            LogAssetLoad("Titan StatData",       titanStat);

            var roomsProp = sbSo.FindProperty("_roomNodes");
            roomsProp.arraySize = 8;

            // [0] 시작 인터미션 (HP 회복 없음 — 최초 진입이므로 의미 없지만 스탯 선택은 가능)
            SetIntermissionRoom(roomsProp, 0, "시작 인터미션");
            SetBossRoom(roomsProp, 1, "Specter",     specterPrefab,     specterStat);
            SetIntermissionRoom(roomsProp, 2, "인터미션 1");
            SetBossRoom(roomsProp, 3, "Berserker",   berserkerPrefab,   berserkerStat);
            SetIntermissionRoom(roomsProp, 4, "인터미션 2");
            SetBossRoom(roomsProp, 5, "Gravekeeper", gravekeeperPrefab, gravekeeperStat);
            SetIntermissionRoom(roomsProp, 6, "인터미션 3");
            SetBossRoom(roomsProp, 7, "Titan",       titanPrefab,       titanStat);
        }

        private static void SetBossRoom(SerializedProperty roomsProp, int index,
                                        string name, GameObject prefab, BossStatData stat)
        {
            var room = roomsProp.GetArrayElementAtIndex(index);
            room.FindPropertyRelative("roomType").enumValueIndex           = 0; // BossRoom
            room.FindPropertyRelative("bossPrefab").objectReferenceValue   = prefab;
            room.FindPropertyRelative("bossStatData").objectReferenceValue = stat;
            room.FindPropertyRelative("displayName").stringValue           = name;
        }

        private static void SetIntermissionRoom(SerializedProperty roomsProp, int index, string name)
        {
            var room = roomsProp.GetArrayElementAtIndex(index);
            room.FindPropertyRelative("roomType").enumValueIndex           = 1; // IntermissionRoom
            room.FindPropertyRelative("bossPrefab").objectReferenceValue   = null;
            room.FindPropertyRelative("bossStatData").objectReferenceValue = null;
            room.FindPropertyRelative("displayName").stringValue           = name;
        }

        private static void LogAssetLoad(string label, Object asset)
        {
            if (asset == null)
                Debug.LogWarning($"[StageSceneSetup] {label} 로드 실패 — 경로 확인 필요.");
        }

        // ── EventSystem ───────────────────────────────────────────
        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var esGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();

#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        // ── 레이아웃 유틸 ─────────────────────────────────────────
        private static void SetFullStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ── UI 헬퍼 ───────────────────────────────────────────────
        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static GameObject CreateTMPText(Transform parent, string name, string text,
                                                 int fontSize, FontStyles style, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var f   = GetFont();
            if (f != null) tmp.font = f;
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = style;
            tmp.color     = color;
            return go;
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
                                               int fontSize, Color btnColor, Color textColor)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = btnColor;
            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(
                Mathf.Min(btnColor.r + 0.15f, 1f),
                Mathf.Min(btnColor.g + 0.15f, 1f),
                Mathf.Min(btnColor.b + 0.15f, 1f));
            colors.pressedColor = new Color(btnColor.r * 0.75f, btnColor.g * 0.75f, btnColor.b * 0.75f);
            btn.colors = colors;

            var textGo = CreateTMPText(go.transform, "ButtonText", label, fontSize, FontStyles.Bold, textColor);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            textGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            return go;
        }

        /// <summary>스탯 선택 카드. Find("Title") / Find("Desc")로 자식 접근 가능.</summary>
        private static GameObject CreateCard(Transform parent, string name,
                                             string titleText, string descText)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = CARD_BG;
            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.25f, 0.35f, 0.50f, 1f);
            colors.pressedColor     = new Color(0.08f, 0.12f, 0.20f, 1f);
            btn.colors = colors;

            var title   = CreateTMPText(go.transform, "Title", titleText, 40, FontStyles.Bold, Color.white);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.65f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(16f, 0f);
            titleRt.offsetMax = new Vector2(-16f, -12f);
            title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            var desc   = CreateTMPText(go.transform, "Desc", descText, 28, FontStyles.Normal,
                                       new Color(0.85f, 0.85f, 0.85f));
            var descRt = desc.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 0.65f);
            descRt.offsetMin = new Vector2(16f, 16f);
            descRt.offsetMax = new Vector2(-16f, 0f);
            desc.GetComponent<TextMeshProUGUI>().alignment       = TextAlignmentOptions.TopJustified;
            desc.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;

            return go;
        }
    }
}
#endif
