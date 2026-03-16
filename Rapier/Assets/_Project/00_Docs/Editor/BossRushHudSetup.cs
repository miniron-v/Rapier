#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Game.UI;
using Game.Enemies;

namespace Game.Editor
{
    /// <summary>
    /// 보스 러시 HUD Canvas 자동 생성 도우미.
    ///
    /// [생성 구성]
    ///   - Screen Space Overlay Canvas (ScaleWithScreenSize, 1080x1920)
    ///   - 상단 보스 HP 영역: 보스 이름 + 페이즈 텍스트 + 대형 HP 바 + 스테이지 텍스트
    ///   - 승리 패널: STAGE X CLEAR 텍스트 + 다음 스테이지 버튼
    ///   - 전체 클리어 패널
    ///
    /// [실행]
    ///   Rapier/BossRush/Create Boss Rush HUD
    ///   Rapier/BossRush/Rebuild Boss Rush HUD
    /// </summary>
    public static class BossRushHudSetup
    {
        private const string SPRITE_BASE =
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/";

        // ── 색상 정의 ─────────────────────────────────────────────
        private static readonly Color BG_DARK         = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        private static readonly Color HP_BG_COLOR     = new Color(0.12f, 0.12f, 0.12f, 0.90f);
        private static readonly Color HP_FILL_COLOR   = new Color(0.90f, 0.20f, 0.20f, 1.00f);
        private static readonly Color HP_PHASE2_COLOR = new Color(1.00f, 0.50f, 0.00f, 1.00f);
        private static readonly Color PANEL_BG_COLOR  = new Color(0.00f, 0.00f, 0.00f, 0.75f);
        private static readonly Color BTN_COLOR       = new Color(0.90f, 0.75f, 0.10f, 1.00f);
        private static readonly Color BTN_TEXT_COLOR  = new Color(0.10f, 0.05f, 0.00f, 1.00f);

        // ── 메뉴 ──────────────────────────────────────────────────
        [MenuItem("Rapier/BossRush/Create Boss Rush HUD")]
        public static void CreateHud()   => BuildHud(false);

        [MenuItem("Rapier/BossRush/Rebuild Boss Rush HUD")]
        public static void RebuildHud()  => BuildHud(true);

        // ── HUD 생성 ──────────────────────────────────────────────
        private static void BuildHud(bool forceRebuild)
        {
            var sq = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Square.png");
            Debug.Log($"[BossRushHudSetup] 내장 Sprite — Square={sq != null}");

            // 기존 제거
            var existing = GameObject.Find("BossRushHudCanvas");
            if (existing != null)
            {
                if (!forceRebuild)
                {
                    Debug.LogWarning("[BossRushHudSetup] BossRushHudCanvas 이미 존재. Rebuild를 사용하세요.");
                    return;
                }
                Undo.DestroyObjectImmediate(existing);
            }

            // ── Root Canvas ───────────────────────────────────────
            var cvGo = new GameObject("BossRushHudCanvas");
            Undo.RegisterCreatedObjectUndo(cvGo, "Create BossRushHudCanvas");

            var cv = cvGo.AddComponent<Canvas>();
            cv.renderMode   = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 20;

            var scaler = cvGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;

            cvGo.AddComponent<GraphicRaycaster>();

            // ── 상단 보스 HP 영역 ──────────────────────────────────
            // 전체 너비, 상단 고정, 높이 160px
            var topBar = CreatePanel(cvGo.transform, "BossHpArea", BG_DARK, sq);
            var topRt  = topBar.GetComponent<RectTransform>();
            topRt.anchorMin        = new Vector2(0f, 1f);
            topRt.anchorMax        = new Vector2(1f, 1f);
            topRt.pivot            = new Vector2(0.5f, 1f);
            topRt.offsetMin        = new Vector2(0f, 0f);
            topRt.offsetMax        = new Vector2(0f, 0f);
            topRt.sizeDelta        = new Vector2(0f, 160f);
            topRt.anchoredPosition = Vector2.zero;

            // 보스 이름 텍스트 (상단 좌측)
            var bossNameGo   = CreateTMPText(topBar.transform, "BossNameText", "BOSS NAME",
                                              48, FontStyles.Bold, Color.white);
            var bossNameRt   = bossNameGo.GetComponent<RectTransform>();
            bossNameRt.anchorMin        = new Vector2(0f, 1f);
            bossNameRt.anchorMax        = new Vector2(0.6f, 1f);
            bossNameRt.pivot            = new Vector2(0f, 1f);
            bossNameRt.offsetMin        = new Vector2(40f, 0f);
            bossNameRt.offsetMax        = new Vector2(0f, 0f);
            bossNameRt.sizeDelta        = new Vector2(0f, 60f);
            bossNameRt.anchoredPosition = new Vector2(40f, -10f);

            // 페이즈 텍스트 (상단 우측)
            var phaseGo   = CreateTMPText(topBar.transform, "BossPhaseText", "PHASE 1",
                                          36, FontStyles.Bold, Color.white);
            var phaseRt   = phaseGo.GetComponent<RectTransform>();
            phaseRt.anchorMin        = new Vector2(0.6f, 1f);
            phaseRt.anchorMax        = new Vector2(1f, 1f);
            phaseRt.pivot            = new Vector2(1f, 1f);
            phaseRt.offsetMin        = new Vector2(0f, 0f);
            phaseRt.offsetMax        = new Vector2(-40f, 0f);
            phaseRt.sizeDelta        = new Vector2(0f, 60f);
            phaseRt.anchoredPosition = new Vector2(-40f, -10f);
            phaseGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;

            // HP 바 배경
            var hpBgGo  = CreatePanel(topBar.transform, "BossHpBg", HP_BG_COLOR, sq);
            var hpBgRt  = hpBgGo.GetComponent<RectTransform>();
            hpBgRt.anchorMin        = new Vector2(0f, 0f);
            hpBgRt.anchorMax        = new Vector2(1f, 0f);
            hpBgRt.pivot            = new Vector2(0.5f, 0f);
            hpBgRt.offsetMin        = new Vector2(30f, 0f);
            hpBgRt.offsetMax        = new Vector2(-30f, 0f);
            hpBgRt.sizeDelta        = new Vector2(0f, 40f);
            hpBgRt.anchoredPosition = new Vector2(0f, 20f);

            // HP 바 Fill
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

            // 스테이지 텍스트 (HP 바 아래)
            var stageGo = CreateTMPText(topBar.transform, "StageText", "STAGE 1 / 2",
                                         28, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f));
            var stageRt = stageGo.GetComponent<RectTransform>();
            stageRt.anchorMin        = new Vector2(0f, 0f);
            stageRt.anchorMax        = new Vector2(1f, 0f);
            stageRt.pivot            = new Vector2(0.5f, 1f);
            stageRt.sizeDelta        = new Vector2(0f, 35f);
            stageRt.anchoredPosition = new Vector2(0f, 62f);
            stageGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            // ── 승리 패널 (화면 중앙) ─────────────────────────────
            var victoryPanel   = CreatePanel(cvGo.transform, "VictoryPanel", PANEL_BG_COLOR, sq);
            var victoryPanelRt = victoryPanel.GetComponent<RectTransform>();
            victoryPanelRt.anchorMin        = new Vector2(0.5f, 0.5f);
            victoryPanelRt.anchorMax        = new Vector2(0.5f, 0.5f);
            victoryPanelRt.pivot            = new Vector2(0.5f, 0.5f);
            victoryPanelRt.sizeDelta        = new Vector2(700f, 500f);
            victoryPanelRt.anchoredPosition = Vector2.zero;
            victoryPanel.SetActive(false);

            // 승리 텍스트
            var victoryTextGo = CreateTMPText(victoryPanel.transform, "VictoryText",
                                               "STAGE CLEAR!", 52, FontStyles.Bold, Color.yellow);
            var victoryTextRt = victoryTextGo.GetComponent<RectTransform>();
            victoryTextRt.anchorMin        = new Vector2(0f, 0.5f);
            victoryTextRt.anchorMax        = new Vector2(1f, 1f);
            victoryTextRt.pivot            = new Vector2(0.5f, 1f);
            victoryTextRt.offsetMin        = new Vector2(20f, 0f);
            victoryTextRt.offsetMax        = new Vector2(-20f, 0f);
            victoryTextRt.sizeDelta        = new Vector2(0f, 0f);
            victoryTextRt.anchoredPosition = Vector2.zero;
            var victoryTmp = victoryTextGo.GetComponent<TextMeshProUGUI>();
            victoryTmp.alignment = TextAlignmentOptions.Center;

            // 다음 스테이지 버튼
            var btnGo  = new GameObject("NextStageButton");
            btnGo.transform.SetParent(victoryPanel.transform, false);
            var btnImg      = btnGo.AddComponent<Image>();
            btnImg.sprite   = sq;
            btnImg.color    = BTN_COLOR;
            var btn         = btnGo.AddComponent<Button>();
            var btnColors   = btn.colors;
            btnColors.highlightedColor = new Color(1f, 0.9f, 0.3f);
            btnColors.pressedColor     = new Color(0.7f, 0.6f, 0.05f);
            btn.colors = btnColors;
            var btnRt       = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin        = new Vector2(0.1f, 0f);
            btnRt.anchorMax        = new Vector2(0.9f, 0f);
            btnRt.pivot            = new Vector2(0.5f, 0f);
            btnRt.sizeDelta        = new Vector2(0f, 120f);
            btnRt.anchoredPosition = new Vector2(0f, 40f);

            var btnTextGo = CreateTMPText(btnGo.transform, "ButtonText", "다음 스테이지",
                                          40, FontStyles.Bold, BTN_TEXT_COLOR);
            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = btnTextRt.offsetMax = Vector2.zero;
            btnTextGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            // ── 전체 클리어 패널 ──────────────────────────────────
            var allClearPanel   = CreatePanel(cvGo.transform, "AllClearPanel", PANEL_BG_COLOR, sq);
            var allClearRt      = allClearPanel.GetComponent<RectTransform>();
            allClearRt.anchorMin        = Vector2.zero;
            allClearRt.anchorMax        = Vector2.one;
            allClearRt.offsetMin        = allClearRt.offsetMax = Vector2.zero;
            allClearPanel.SetActive(false);

            var allClearTextGo = CreateTMPText(allClearPanel.transform, "AllClearText",
                                                "ALL CLEAR!", 80, FontStyles.Bold, Color.yellow);
            var allClearTextRt = allClearTextGo.GetComponent<RectTransform>();
            allClearTextRt.anchorMin        = new Vector2(0f, 0.4f);
            allClearTextRt.anchorMax        = new Vector2(1f, 0.6f);
            allClearTextRt.offsetMin        = allClearTextRt.offsetMax = Vector2.zero;
            allClearTextGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            // ── BossRushHudView 부착 및 연결 ──────────────────────
            var hudView = cvGo.AddComponent<BossRushHudView>();
            var t       = typeof(BossRushHudView);
            var flags   = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            t.GetField("_bossHpFill",    flags)?.SetValue(hudView, hpFillImg);
            t.GetField("_bossNameText",  flags)?.SetValue(hudView, bossNameGo.GetComponent<TextMeshProUGUI>());
            t.GetField("_bossPhaseText", flags)?.SetValue(hudView, phaseGo.GetComponent<TextMeshProUGUI>());
            t.GetField("_stageText",     flags)?.SetValue(hudView, stageGo.GetComponent<TextMeshProUGUI>());
            t.GetField("_victoryPanel",  flags)?.SetValue(hudView, victoryPanel);
            t.GetField("_victoryText",   flags)?.SetValue(hudView, victoryTmp);
            t.GetField("_nextStageButton", flags)?.SetValue(hudView, btn);
            t.GetField("_allClearPanel", flags)?.SetValue(hudView, allClearPanel);

            Selection.activeGameObject = cvGo;
            Debug.Log("[BossRushHudSetup] BossRushHudCanvas 생성 완료!");
        }

        // ── 헬퍼 ──────────────────────────────────────────────────
        private static GameObject CreatePanel(Transform parent, string name, Color color, Sprite sprite)
        {
            var go    = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img   = go.AddComponent<Image>();
            img.color  = color;
            img.sprite = sprite;
            return go;
        }

        private static GameObject CreateTMPText(Transform parent, string name, string text,
                                                  int fontSize, FontStyles style, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp        = go.AddComponent<TextMeshProUGUI>();
            tmp.text       = text;
            tmp.fontSize   = fontSize;
            tmp.fontStyle  = style;
            tmp.color      = color;
            return go;
        }
    }
}
#endif
