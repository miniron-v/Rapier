#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Game.UI;
using Game.Enemies;
using Game.Core.Stage;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Game.Editor
{
    /// <summary>
    /// 보스 HUD Canvas 자동 생성 도우미.
    ///
    /// [생성 구성]
    ///   BossHudCanvas (Canvas + BossHudView)
    ///     - 상단 보스 HP 영역: 보스 이름 + 페이즈 텍스트 + HP 바 + 스테이지 텍스트
    ///     - 승리 패널: STAGE X CLEAR + 다음 스테이지 버튼
    ///     - 결과 패널: ALL CLEAR / GAME OVER + 로비로 버튼
    ///   EventSystem (없을 때만 생성, InputSystemUIInputModule)
    ///
    /// [연결 목록]
    ///   BossHudView : Init()으로 UI 참조 전체 주입
    ///   BossRushManager : InitHudView()로 _hudView 주입 (씬에 존재할 경우)
    ///   ProgressionManager : SetBossHud()로 _bossHud 주입 (씬에 존재할 경우)
    ///
    /// [레거시 마이그레이션]
    ///   Rebuild 실행 시 "BossRushHudCanvas" 이름의 기존 오브젝트도 탐색하여 제거.
    ///
    /// [직렬화 보장]
    ///   SetDirty(hudView) + SetDirty(manager/progressionManager) + MarkSceneDirty + SaveScene
    ///
    /// [실행]
    ///   Rapier/Boss HUD/Create Boss HUD
    ///   Rapier/Boss HUD/Rebuild Boss HUD
    /// </summary>
    public static class BossHudSetup
    {
        private const string SPRITE_BASE =
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/";

        private const string FONT_ASSET_PATH =
            "Assets/_Project/ScriptableObjects/Fonts/NEXONLv1Gothic Regular SDF.asset";

        private static TMP_FontAsset _font;

        private static TMP_FontAsset GetFont()
        {
            if (_font == null)
                _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
            return _font;
        }

        private static readonly Color BG_DARK         = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        private static readonly Color HP_BG_COLOR     = new Color(0.12f, 0.12f, 0.12f, 0.90f);
        private static readonly Color HP_FILL_COLOR   = new Color(0.90f, 0.20f, 0.20f, 1.00f);
        private static readonly Color PANEL_BG_COLOR  = new Color(0.00f, 0.00f, 0.00f, 0.75f);
        private static readonly Color BTN_COLOR       = new Color(0.90f, 0.75f, 0.10f, 1.00f);
        private static readonly Color BTN_TEXT_COLOR  = new Color(0.10f, 0.05f, 0.00f, 1.00f);
        private static readonly Color BTN_LOBBY_COLOR = new Color(0.30f, 0.55f, 0.90f, 1.00f);

        [MenuItem("Rapier/Boss HUD/Create Boss HUD")]
        public static void CreateHud()  => BuildHud(false);

        [MenuItem("Rapier/Boss HUD/Rebuild Boss HUD")]
        public static void RebuildHud() => BuildHud(true);

        private static void BuildHud(bool forceRebuild)
        {
            _font = null; // 매 빌드마다 재로드
            var sq = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Square.png");
            Debug.Log($"[BossHudSetup] Square={sq != null}, Font={GetFont() != null}");

            // ── 기존 Canvas 제거 (신규 이름 + 레거시 이름 모두 탐색) ──
            var existingNew    = GameObject.Find("BossHudCanvas");
            var existingLegacy = GameObject.Find("BossRushHudCanvas");

            if (existingNew != null || existingLegacy != null)
            {
                if (!forceRebuild)
                {
                    Debug.LogWarning("[BossHudSetup] 이미 존재. Rebuild를 사용하세요.");
                    return;
                }
                if (existingNew    != null) Undo.DestroyObjectImmediate(existingNew);
                if (existingLegacy != null) Undo.DestroyObjectImmediate(existingLegacy);
            }

            // ── EventSystem (없을 때만 생성) ──────────────────────
            EnsureEventSystem();

            // ── Root Canvas ───────────────────────────────────────
            var cvGo = new GameObject("BossHudCanvas");
            Undo.RegisterCreatedObjectUndo(cvGo, "Create BossHudCanvas");

            var cv = cvGo.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 20;

            var scaler = cvGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;

            cvGo.AddComponent<GraphicRaycaster>();

            // ── Safe Area 컨테이너 ───────────────────────────────
            // 모든 HUD 콘텐츠의 부모. 런타임에 SafeAreaFitter 가
            // Screen.safeArea 를 읽어 anchor 를 축소하므로 노치/제스처바
            // 영역에 콘텐츠가 걸리지 않는다.
            var safeAreaGo = new GameObject("SafeAreaPanel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(safeAreaGo, "Create SafeAreaPanel");
            safeAreaGo.transform.SetParent(cvGo.transform, false);
            var safeAreaRt = safeAreaGo.GetComponent<RectTransform>();
            safeAreaRt.anchorMin        = Vector2.zero;
            safeAreaRt.anchorMax        = Vector2.one;
            safeAreaRt.offsetMin        = Vector2.zero;
            safeAreaRt.offsetMax        = Vector2.zero;
            safeAreaRt.anchoredPosition = Vector2.zero;
            safeAreaGo.AddComponent<SafeAreaFitter>();

            // ── 상단 보스 HP 영역 ──────────────────────────────────
            // [레이아웃 원칙]
            //  - 모든 자식은 단일 anchor(=min==max) + 고정 sizeDelta 로 배치.
            //    anchor stretch + anchoredPosition 병용은 RectTransform 상호작용이
            //    꼬여 예측이 어려우므로 단순화한다.
            //  - topBar 총 높이 200. 내부 Y 배치(상단 원점, pivot top 기준):
            //      [-10 ~ -70] BossName(좌) / Phase(우)  60
            //      [-80 ~ -110] StageText                 30
            //      [-140 ~ -180] HpBg                     40   (하단에서 20 띄움)
            var topBar = CreatePanel(safeAreaGo.transform, "BossHpArea", BG_DARK, sq);
            var topRt  = topBar.GetComponent<RectTransform>();
            topRt.anchorMin        = new Vector2(0f, 1f);
            topRt.anchorMax        = new Vector2(1f, 1f);
            topRt.pivot            = new Vector2(0.5f, 1f);
            topRt.offsetMin        = Vector2.zero;
            topRt.offsetMax        = Vector2.zero;
            topRt.sizeDelta        = new Vector2(0f, 200f);
            topRt.anchoredPosition = Vector2.zero;

            var bossNameGo = CreateTMPText(topBar.transform, "BossNameText", "BOSS NAME", 48, FontStyles.Bold, Color.white);
            var bossNameRt = bossNameGo.GetComponent<RectTransform>();
            bossNameRt.anchorMin        = new Vector2(0f, 1f);
            bossNameRt.anchorMax        = new Vector2(0f, 1f);
            bossNameRt.pivot            = new Vector2(0f, 1f);
            bossNameRt.sizeDelta        = new Vector2(700f, 60f);
            bossNameRt.anchoredPosition = new Vector2(40f, -10f);
            bossNameGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

            var phaseGo = CreateTMPText(topBar.transform, "BossPhaseText", "PHASE 1", 36, FontStyles.Bold, Color.white);
            var phaseRt = phaseGo.GetComponent<RectTransform>();
            phaseRt.anchorMin        = new Vector2(1f, 1f);
            phaseRt.anchorMax        = new Vector2(1f, 1f);
            phaseRt.pivot            = new Vector2(1f, 1f);
            phaseRt.sizeDelta        = new Vector2(400f, 60f);
            phaseRt.anchoredPosition = new Vector2(-40f, -10f);
            phaseGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;

            var stageGo = CreateTMPText(topBar.transform, "StageText", "STAGE 1 / 2", 28, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f));
            var stageRt = stageGo.GetComponent<RectTransform>();
            stageRt.anchorMin        = new Vector2(0.5f, 1f);
            stageRt.anchorMax        = new Vector2(0.5f, 1f);
            stageRt.pivot            = new Vector2(0.5f, 1f);
            stageRt.sizeDelta        = new Vector2(800f, 30f);
            stageRt.anchoredPosition = new Vector2(0f, -80f);
            stageGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            var hpBgGo = CreatePanel(topBar.transform, "BossHpBg", HP_BG_COLOR, sq);
            var hpBgRt = hpBgGo.GetComponent<RectTransform>();
            hpBgRt.anchorMin        = new Vector2(0.5f, 0f);
            hpBgRt.anchorMax        = new Vector2(0.5f, 0f);
            hpBgRt.pivot            = new Vector2(0.5f, 0f);
            hpBgRt.sizeDelta        = new Vector2(1020f, 40f);
            hpBgRt.anchoredPosition = new Vector2(0f, 20f);

            var hpFillGo  = CreatePanel(hpBgGo.transform, "BossHpFill", HP_FILL_COLOR, sq);
            var hpFillImg = hpFillGo.GetComponent<Image>();
            hpFillImg.type       = Image.Type.Filled;
            hpFillImg.fillMethod = Image.FillMethod.Horizontal;
            hpFillImg.fillOrigin = 0;
            hpFillImg.fillAmount = 1f;
            var hpFillRt = hpFillGo.GetComponent<RectTransform>();
            hpFillRt.anchorMin = Vector2.zero;
            hpFillRt.anchorMax = Vector2.one;
            hpFillRt.pivot     = new Vector2(0f, 0.5f);
            hpFillRt.offsetMin = hpFillRt.offsetMax = Vector2.zero;

            // 보스 HP 수치 텍스트 (HP Bg 중앙, Fill 과 독립 — fill 축소에 영향받지 않음)
            var bossHpTextGo = CreateTMPText(hpBgGo.transform, "BossHpText", "0", 26, FontStyles.Bold, Color.white);
            var bossHpTextRt = bossHpTextGo.GetComponent<RectTransform>();
            bossHpTextRt.anchorMin        = Vector2.zero;
            bossHpTextRt.anchorMax        = Vector2.one;
            bossHpTextRt.pivot            = new Vector2(0.5f, 0.5f);
            bossHpTextRt.offsetMin        = bossHpTextRt.offsetMax = Vector2.zero;
            bossHpTextRt.anchoredPosition = Vector2.zero;
            bossHpTextGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            // ── 승리 패널 ─────────────────────────────────────────
            var victoryPanel   = CreatePanel(safeAreaGo.transform, "VictoryPanel", PANEL_BG_COLOR, sq);
            var victoryPanelRt = victoryPanel.GetComponent<RectTransform>();
            victoryPanelRt.anchorMin        = new Vector2(0.5f, 0.5f);
            victoryPanelRt.anchorMax        = new Vector2(0.5f, 0.5f);
            victoryPanelRt.pivot            = new Vector2(0.5f, 0.5f);
            victoryPanelRt.sizeDelta        = new Vector2(700f, 500f);
            victoryPanelRt.anchoredPosition = Vector2.zero;
            victoryPanel.SetActive(false);

            var victoryTextGo = CreateTMPText(victoryPanel.transform, "VictoryText", "STAGE CLEAR!", 52, FontStyles.Bold, Color.yellow);
            var victoryTextRt = victoryTextGo.GetComponent<RectTransform>();
            victoryTextRt.anchorMin        = new Vector2(0f, 0.5f);
            victoryTextRt.anchorMax        = new Vector2(1f, 1f);
            victoryTextRt.pivot            = new Vector2(0.5f, 1f);
            victoryTextRt.offsetMin        = new Vector2(20f, 0f);
            victoryTextRt.offsetMax        = new Vector2(-20f, 0f);
            victoryTextRt.anchoredPosition = Vector2.zero;
            victoryTextGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            var nextBtnGo = CreateButton(victoryPanel.transform, "NextStageButton", "다음 스테이지", 40, BTN_COLOR, BTN_TEXT_COLOR, sq);
            var nextBtnRt = nextBtnGo.GetComponent<RectTransform>();
            nextBtnRt.anchorMin        = new Vector2(0.1f, 0f);
            nextBtnRt.anchorMax        = new Vector2(0.9f, 0f);
            nextBtnRt.pivot            = new Vector2(0.5f, 0f);
            nextBtnRt.sizeDelta        = new Vector2(0f, 120f);
            nextBtnRt.anchoredPosition = new Vector2(0f, 40f);

            // ── 결과 패널 ─────────────────────────────────────────
            var resultPanel   = CreatePanel(safeAreaGo.transform, "ResultPanel", PANEL_BG_COLOR, sq);
            var resultPanelRt = resultPanel.GetComponent<RectTransform>();
            resultPanelRt.anchorMin = Vector2.zero;
            resultPanelRt.anchorMax = Vector2.one;
            resultPanelRt.offsetMin = resultPanelRt.offsetMax = Vector2.zero;
            resultPanel.SetActive(false);

            var resultTextGo = CreateTMPText(resultPanel.transform, "ResultText", "ALL CLEAR!", 80, FontStyles.Bold, Color.yellow);
            var resultTextRt = resultTextGo.GetComponent<RectTransform>();
            resultTextRt.anchorMin = new Vector2(0f, 0.45f);
            resultTextRt.anchorMax = new Vector2(1f, 0.60f);
            resultTextRt.offsetMin = resultTextRt.offsetMax = Vector2.zero;
            resultTextGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            var lobbyBtnGo = CreateButton(resultPanel.transform, "ToLobbyButton", "로비로", 44, BTN_LOBBY_COLOR, Color.white, sq);
            var lobbyBtnRt = lobbyBtnGo.GetComponent<RectTransform>();
            lobbyBtnRt.anchorMin        = new Vector2(0.2f, 0.5f);
            lobbyBtnRt.anchorMax        = new Vector2(0.8f, 0.5f);
            lobbyBtnRt.pivot            = new Vector2(0.5f, 1f);
            lobbyBtnRt.sizeDelta        = new Vector2(0f, 130f);
            lobbyBtnRt.anchoredPosition = new Vector2(0f, -20f);

            // ── BossHudView 부착 및 Init() 주입 ───────────────────
            var hudView = cvGo.AddComponent<BossHudView>();
            hudView.Init(
                hpFillImg,
                bossHpTextGo.GetComponent<TextMeshProUGUI>(),
                bossNameGo.GetComponent<TextMeshProUGUI>(),
                phaseGo.GetComponent<TextMeshProUGUI>(),
                stageGo.GetComponent<TextMeshProUGUI>(),
                victoryPanel,
                victoryTextGo.GetComponent<TextMeshProUGUI>(),
                nextBtnGo.GetComponent<Button>(),
                resultPanel,
                resultTextGo.GetComponent<TextMeshProUGUI>(),
                lobbyBtnGo.GetComponent<Button>()
            );
            EditorUtility.SetDirty(hudView);

            // ── BossRushManager에 HudView 연결 (씬에 존재할 경우) ─
            var bossRushManager = Object.FindObjectOfType<BossRushManager>();
            if (bossRushManager != null)
            {
                bossRushManager.InitHudView(hudView);
                EditorUtility.SetDirty(bossRushManager);
                Debug.Log("[BossHudSetup] BossRushManager._hudView 연결 완료.");
            }
            else
            {
                Debug.Log("[BossHudSetup] BossRushManager 없음. ProgressionManager 씬이면 정상.");
            }

            // ── ProgressionManager에 HudView 연결 (씬에 존재할 경우) ─
            var progressionManager = Object.FindObjectOfType<ProgressionManager>();
            if (progressionManager != null)
            {
                progressionManager.SetBossHud(hudView);
                EditorUtility.SetDirty(progressionManager);
                Debug.Log("[BossHudSetup] ProgressionManager._bossHud 연결 완료.");
            }
            else
            {
                Debug.Log("[BossHudSetup] ProgressionManager 없음. BossRushDemo 씬이면 정상.");
            }

            if (bossRushManager == null && progressionManager == null)
                Debug.LogWarning("[BossHudSetup] BossRushManager / ProgressionManager 모두 없음. 수동 연결 필요.");

            // ── 씬 저장 ───────────────────────────────────────────
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = cvGo;
            Debug.Log("[BossHudSetup] BossHudCanvas 생성 및 씬 저장 완료!");
        }

        // ── EventSystem 보장 ──────────────────────────────────────
        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var esGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();

#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[BossHudSetup] EventSystem 생성 (InputSystemUIInputModule).");
#else
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[BossHudSetup] EventSystem 생성 (StandaloneInputModule).");
#endif
        }

        // ── 헬퍼 ──────────────────────────────────────────────────
        private static GameObject CreatePanel(Transform parent, string name, Color color, Sprite sprite)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color  = color;
            img.sprite = sprite;
            return go;
        }

        private static GameObject CreateTMPText(Transform parent, string name, string text,
                                                  int fontSize, FontStyles style, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp       = go.AddComponent<TextMeshProUGUI>();
            var font      = GetFont();
            if (font != null)
                tmp.font  = font;
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = style;
            tmp.color     = color;
            return go;
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
                                                int fontSize, Color btnColor, Color textColor, Sprite sprite)
        {
            var go     = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img    = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color  = btnColor;
            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(Mathf.Min(btnColor.r + 0.15f, 1f), Mathf.Min(btnColor.g + 0.15f, 1f), Mathf.Min(btnColor.b + 0.15f, 1f));
            colors.pressedColor     = new Color(btnColor.r * 0.75f, btnColor.g * 0.75f, btnColor.b * 0.75f);
            btn.colors = colors;

            var textGo = CreateTMPText(go.transform, "ButtonText", label, fontSize, FontStyles.Bold, textColor);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            textGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            return go;
        }
    }
}
#endif
