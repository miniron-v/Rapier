#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Game.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Game.Editor
{
    /// <summary>
    /// 로비 씬 Canvas 자동 생성 도우미.
    ///
    /// [생성 구성]
    ///   LobbyCanvas (Canvas + LobbyManager)
    ///     - 배경
    ///     - 타이틀 텍스트 ("RAPIER")
    ///     - 시작 버튼 → SceneController.LoadGame()
    ///   EventSystem (없을 때만 생성, InputSystemUIInputModule)
    ///
    /// [연결 목록]
    ///   LobbyManager._startButton : Init(btn)으로 주입
    ///
    /// [직렬화 보장]
    ///   SetDirty(lobbyManager) + MarkSceneDirty + SaveScene
    ///
    /// [실행]
    ///   Rapier/Lobby/Create Lobby HUD
    ///   Rapier/Lobby/Rebuild Lobby HUD
    ///
    /// [확장]
    ///   캐릭터 선택 / 성장 UI 등은 담당 팀원이 LobbyCanvas 하위에 추가한다.
    /// </summary>
    public static class LobbyHudSetup
    {
        private const string SPRITE_BASE =
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/";

        private static readonly Color BG_COLOR       = new Color(0.06f, 0.06f, 0.10f, 1.00f);
        private static readonly Color BTN_COLOR      = new Color(0.90f, 0.75f, 0.10f, 1.00f);
        private static readonly Color BTN_TEXT_COLOR = new Color(0.10f, 0.05f, 0.00f, 1.00f);

        [MenuItem("Rapier/Lobby/Create Lobby HUD")]
        public static void CreateHud()  => BuildHud(false);

        [MenuItem("Rapier/Lobby/Rebuild Lobby HUD")]
        public static void RebuildHud() => BuildHud(true);

        private static void BuildHud(bool forceRebuild)
        {
            var sq = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Square.png");
            Debug.Log($"[LobbyHudSetup] Square={sq != null}");

            // ── 기존 Canvas 제거 ──────────────────────────────────
            var existing = GameObject.Find("LobbyCanvas");
            if (existing != null)
            {
                if (!forceRebuild)
                {
                    Debug.LogWarning("[LobbyHudSetup] 이미 존재. Rebuild를 사용하세요.");
                    return;
                }
                Undo.DestroyObjectImmediate(existing);
            }

            // ── EventSystem (없을 때만 생성) ──────────────────────
            EnsureEventSystem();

            // ── Root Canvas ───────────────────────────────────────
            var cvGo = new GameObject("LobbyCanvas");
            Undo.RegisterCreatedObjectUndo(cvGo, "Create LobbyCanvas");

            var cv = cvGo.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 10;

            var scaler = cvGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;

            cvGo.AddComponent<GraphicRaycaster>();

            // ── 배경 ──────────────────────────────────────────────
            var bg    = new GameObject("Background");
            bg.transform.SetParent(cvGo.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color  = BG_COLOR;
            bgImg.sprite = sq;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            // ── 타이틀 텍스트 ──────────────────────────────────────
            var titleGo        = new GameObject("TitleText");
            titleGo.transform.SetParent(cvGo.transform, false);
            var titleTmp       = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text      = "RAPIER";
            titleTmp.fontSize  = 120;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color     = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.55f);
            titleRt.anchorMax = new Vector2(1f, 0.80f);
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

            // ── 시작 버튼 ─────────────────────────────────────────
            var btnGo  = new GameObject("StartButton");
            btnGo.transform.SetParent(cvGo.transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.sprite = sq;
            btnImg.color  = BTN_COLOR;
            var btn    = btnGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.3f);
            colors.pressedColor     = new Color(0.7f, 0.6f, 0.05f);
            btn.colors = colors;
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin        = new Vector2(0.2f, 0.5f);
            btnRt.anchorMax        = new Vector2(0.8f, 0.5f);
            btnRt.pivot            = new Vector2(0.5f, 1f);
            btnRt.sizeDelta        = new Vector2(0f, 150f);
            btnRt.anchoredPosition = new Vector2(0f, -20f);

            var btnTextGo      = new GameObject("ButtonText");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTmp         = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnTmp.text        = "시작";
            btnTmp.fontSize    = 56;
            btnTmp.fontStyle   = FontStyles.Bold;
            btnTmp.color       = BTN_TEXT_COLOR;
            btnTmp.alignment   = TextAlignmentOptions.Center;
            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = btnTextRt.offsetMax = Vector2.zero;

            // ── LobbyManager 부착 및 Init() 주입 ─────────────────
            var lobbyManager = cvGo.AddComponent<LobbyManager>();
            lobbyManager.Init(btn);
            EditorUtility.SetDirty(lobbyManager);

            // ── 씬 저장 ───────────────────────────────────────────
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = cvGo;
            Debug.Log("[LobbyHudSetup] LobbyCanvas 생성 및 씬 저장 완료!");
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
            Debug.Log("[LobbyHudSetup] EventSystem 생성 (InputSystemUIInputModule).");
#else
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[LobbyHudSetup] EventSystem 생성 (StandaloneInputModule).");
#endif
        }
    }
}
#endif
