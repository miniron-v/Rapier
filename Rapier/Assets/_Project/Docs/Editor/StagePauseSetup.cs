#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Game.UI.Stage.Pause;

namespace Game.Editor
{
    /// <summary>
    /// Phase 23-B 스테이지 일시정지 UI 자동 생성 도우미.
    ///
    /// [생성 구성]
    ///   [UI] Canvas (기존 StageDemo Canvas에 추가)
    ///     └─ PauseRoot            (PausePanelPresenter, PausePanelView)
    ///          ├─ PauseButton     (우상단 HUD 버튼, 40×40)
    ///          └─ PausePanel      (전체화면 반투명 오버레이, 기본 비활성화)
    ///               ├─ BgOverlay  (검정 반투명 Image)
    ///               ├─ ResumeButton    ("재개")
    ///               └─ ExitButton      ("나가기")
    ///
    /// [timeScale 제어]
    ///   PausePanelPresenter.HandlePauseClicked → Time.timeScale = 0f
    ///   HandleResume / HandleExitToLobby       → Time.timeScale = _prevTimeScale 복원
    ///   SceneController.LoadLobby()            → 내부에서 1f 재확인 (이중 안전)
    ///
    /// [실행]
    ///   Rapier/Stage/Pause UI/Add to StageDemo
    ///   Rapier/Stage/Pause UI/Rebuild in StageDemo
    /// </summary>
    public static class StagePauseSetup
    {
        // ── 상수 ─────────────────────────────────────────────────────
        private const string FONT_ASSET_PATH =
            "Assets/_Project/ScriptableObjects/Fonts/NEXONLv1Gothic Regular SDF.asset";

        private const string SCENE_SAVE_PATH =
            "Assets/_Project/Scenes/StageDemo.unity";

        private const string CANVAS_GO_NAME  = "[UI]";
        private const string PAUSE_ROOT_NAME = "PauseRoot";

        // ── 색상 ─────────────────────────────────────────────────────
        private static readonly Color OVERLAY_COLOR   = new Color(0f,   0f,   0f,   0.75f);
        private static readonly Color BTN_PAUSE_COLOR = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color BTN_RESUME      = new Color(0.20f, 0.70f, 0.35f, 1.00f);
        private static readonly Color BTN_EXIT        = new Color(0.85f, 0.20f, 0.15f, 1.00f);
        private static readonly Color BTN_TEXT        = Color.white;

        // ── 폰트 캐시 ─────────────────────────────────────────────────
        private static TMP_FontAsset _font;
        private static TMP_FontAsset GetFont()
        {
            if (_font == null)
                _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
            return _font;
        }

        // ── 메뉴 항목 ────────────────────────────────────────────────
        [MenuItem("Rapier/Stage/Pause UI/Add to StageDemo",     priority = 10)]
        public static void AddPauseUI()    => BuildPauseUI(false);

        [MenuItem("Rapier/Stage/Pause UI/Rebuild in StageDemo", priority = 11)]
        public static void RebuildPauseUI() => BuildPauseUI(true);

        // ── 진입점 ───────────────────────────────────────────────────
        /// <param name="forceRebuild">true 이면 기존 PauseRoot 제거 후 재생성.</param>
        /// <param name="skipSceneNameCheck">StageSceneSetup 내부에서 호출 시 true — 씬 이름 검사 생략.</param>
        public static void BuildPauseUI(bool forceRebuild, bool skipSceneNameCheck = false)
        {
            _font = null;
            Debug.Log($"[StagePauseSetup] Font={GetFont() != null}");

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!skipSceneNameCheck && activeScene.name != "StageDemo")
            {
                Debug.LogWarning("[StagePauseSetup] StageDemo 씬이 활성화 상태가 아닙니다. 씬을 먼저 열어주세요.");
                return;
            }

            // ── 기존 Canvas 탐색 ──────────────────────────────────────
            var canvasGo = GameObject.Find(CANVAS_GO_NAME);
            if (canvasGo == null)
            {
                Debug.LogWarning($"[StagePauseSetup] '{CANVAS_GO_NAME}' Canvas를 찾지 못했습니다. StageSceneSetup을 먼저 실행하세요.");
                return;
            }

            // ── 기존 PauseRoot 제거 (Rebuild 모드) ────────────────────
            var existingRoot = canvasGo.transform.Find(PAUSE_ROOT_NAME);
            if (existingRoot != null)
            {
                if (!forceRebuild)
                {
                    Debug.LogWarning("[StagePauseSetup] PauseRoot가 이미 존재합니다. Rebuild를 사용하세요.");
                    return;
                }
                Undo.DestroyObjectImmediate(existingRoot.gameObject);
            }

            // ── PauseRoot ─────────────────────────────────────────────
            var rootGo = new GameObject(PAUSE_ROOT_NAME, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rootGo, "Create PauseRoot");
            rootGo.transform.SetParent(canvasGo.transform, false);
            SetFullStretch(rootGo);

            var presenter = rootGo.AddComponent<PausePanelPresenter>();
            var view      = rootGo.AddComponent<PausePanelView>();

            // ── Pause 버튼 (HUD 우상단) ───────────────────────────────
            var pauseBtnGo = new GameObject("PauseButton", typeof(RectTransform));
            pauseBtnGo.transform.SetParent(rootGo.transform, false);
            pauseBtnGo.AddComponent<Image>().color = BTN_PAUSE_COLOR;
            var pbBtn    = pauseBtnGo.AddComponent<Button>();
            var pbColors = pbBtn.colors;
            pbColors.highlightedColor = new Color(0.30f, 0.30f, 0.30f, 0.95f);
            pbColors.pressedColor     = new Color(0.08f, 0.08f, 0.08f, 1.00f);
            pbBtn.colors = pbColors;

            // 우상단 앵커 + 안전 영역 여백 40px
            var pbRt         = pauseBtnGo.GetComponent<RectTransform>();
            pbRt.anchorMin   = new Vector2(1f, 1f);
            pbRt.anchorMax   = new Vector2(1f, 1f);
            pbRt.pivot       = new Vector2(1f, 1f);
            pbRt.sizeDelta   = new Vector2(100f, 100f);
            pbRt.anchoredPosition = new Vector2(-40f, -60f);

            // Pause 버튼 텍스트 (II 기호)
            var pbTextGo = CreateTMPText(pauseBtnGo.transform, "PauseIcon", "II", 36,
                                         FontStyles.Bold, BTN_TEXT);
            var pbTextRt     = pbTextGo.GetComponent<RectTransform>();
            pbTextRt.anchorMin = Vector2.zero;
            pbTextRt.anchorMax = Vector2.one;
            pbTextRt.offsetMin = pbTextRt.offsetMax = Vector2.zero;
            pbTextGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            // ── PausePanel (전체화면 오버레이) ────────────────────────
            var panelGo = new GameObject("PausePanel", typeof(RectTransform));
            panelGo.transform.SetParent(rootGo.transform, false);
            SetFullStretch(panelGo);
            panelGo.SetActive(false); // 기본 비활성화

            // 배경 오버레이
            var bgGo = new GameObject("BgOverlay", typeof(RectTransform));
            bgGo.transform.SetParent(panelGo.transform, false);
            bgGo.AddComponent<Image>().color = OVERLAY_COLOR;
            SetFullStretch(bgGo);

            // ── 재개 버튼 ─────────────────────────────────────────────
            var resumeBtnGo = CreateButton(panelGo.transform, "ResumeButton",
                                           "재개", 52, BTN_RESUME, BTN_TEXT);
            var resumeRt          = resumeBtnGo.GetComponent<RectTransform>();
            resumeRt.anchorMin    = new Vector2(0.15f, 0.50f);
            resumeRt.anchorMax    = new Vector2(0.85f, 0.50f);
            resumeRt.pivot        = new Vector2(0.5f,  0f);
            resumeRt.sizeDelta    = new Vector2(0f, 130f);
            resumeRt.anchoredPosition = new Vector2(0f, 20f);

            // ── 나가기 버튼 ───────────────────────────────────────────
            var exitBtnGo = CreateButton(panelGo.transform, "ExitButton",
                                         "나가기", 48, BTN_EXIT, BTN_TEXT);
            var exitRt          = exitBtnGo.GetComponent<RectTransform>();
            exitRt.anchorMin    = new Vector2(0.15f, 0.50f);
            exitRt.anchorMax    = new Vector2(0.85f, 0.50f);
            exitRt.pivot        = new Vector2(0.5f,  1f);
            exitRt.sizeDelta    = new Vector2(0f, 120f);
            exitRt.anchoredPosition = new Vector2(0f, -20f);

            // ── SerializedObject 배선 ────────────────────────────────
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_panel").objectReferenceValue            = panelGo;
            viewSo.FindProperty("_resumeButton").objectReferenceValue     = resumeBtnGo.GetComponent<Button>();
            viewSo.FindProperty("_exitToLobbyButton").objectReferenceValue = exitBtnGo.GetComponent<Button>();
            viewSo.ApplyModifiedProperties();

            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("_pausePanelView").objectReferenceValue = view;
            presenterSo.FindProperty("_pauseButton").objectReferenceValue    = pbBtn;
            presenterSo.ApplyModifiedProperties();

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(presenter);

            // ── 씬 저장 ───────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene, SCENE_SAVE_PATH);

            Debug.Log("[StagePauseSetup] 일시정지 UI 생성 완료!\n" +
                      "  PauseRoot\n" +
                      "    PauseButton   (우상단, 100×100)\n" +
                      "    PausePanel    (전체화면 오버레이, 기본 비활성)\n" +
                      "      BgOverlay\n" +
                      "      ResumeButton\n" +
                      "      ExitButton");
        }

        // ── 레이아웃 유틸 ─────────────────────────────────────────────
        private static void SetFullStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreateTMPText(Transform parent, string name, string text,
                                                 int fontSize, FontStyles style, Color color)
        {
            var go  = new GameObject(name, typeof(RectTransform));
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
            var go  = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = btnColor;
            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(
                Mathf.Min(btnColor.r + 0.15f, 1f),
                Mathf.Min(btnColor.g + 0.15f, 1f),
                Mathf.Min(btnColor.b + 0.15f, 1f));
            colors.pressedColor = new Color(
                btnColor.r * 0.75f,
                btnColor.g * 0.75f,
                btnColor.b * 0.75f);
            btn.colors = colors;

            var textGo = CreateTMPText(go.transform, "ButtonText", label, fontSize,
                                       FontStyles.Bold, textColor);
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
