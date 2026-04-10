#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TMPro;
using Game.UI;
using Game.UI.Lobby;

namespace Game.DevTools
{
    /// <summary>
    /// 로비 5탭 HUD를 씬에 자동 생성하는 에디터 툴.
    ///
    /// 메뉴:
    ///   Rapier/Lobby/Create Lobby HUD  — 신규 생성
    ///   Rapier/Lobby/Rebuild Lobby HUD — 기존 삭제 후 재생성
    ///
    /// [Setup 체크리스트 (UI.md §3)]
    ///   1. [v] 모든 [SerializeField] → Init()으로 주입
    ///   2. [v] 씬 내 LobbyManager가 LobbyPresenter를 참조
    ///   3. [v] EventSystem(InputSystemUIInputModule) 생성
    ///   4. [v] SetDirty → MarkSceneDirty → SaveScene 순서 준수
    ///
    /// [CanvasScaler]
    ///   ScaleWithScreenSize, referenceResolution (1080, 1920)
    /// </summary>
    public static class LobbyHudSetup
    {
        private const string ROOT_NAME = "LobbyHUD";

        [MenuItem("Rapier/Lobby/Create Lobby HUD")]
        public static void CreateLobbyHud()
        {
            var existing = GameObject.Find(ROOT_NAME);
            if (existing != null)
            {
                Debug.LogWarning("[LobbyHudSetup] LobbyHUD가 이미 씬에 존재합니다. Rebuild를 사용하세요.");
                return;
            }
            Build();
        }

        [MenuItem("Rapier/Lobby/Rebuild Lobby HUD")]
        public static void RebuildLobbyHud()
        {
            var existing = GameObject.Find(ROOT_NAME);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);
            Build();
        }

        // ── 메인 빌드 메서드 ──────────────────────────────────────
        private static void Build()
        {
            EnsureEventSystem();

            // 1. Canvas 루트
            var root      = new GameObject(ROOT_NAME);
            var canvas    = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler    = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // 2. 탭 패널 영역 (탭 바 위쪽, 전체 화면에서 하단 바 높이 제외)
            var contentArea = CreateRectChild(root, "ContentArea");
            SetAnchors(contentArea, Vector2.zero, Vector2.one);
            contentArea.offsetMin = new Vector2(0, 120); // 하단 탭 바 높이
            contentArea.offsetMax = Vector2.zero;

            // 3. 5개 탭 패널 생성
            var shopPanel       = CreateTabPanel(contentArea.gameObject, "ShopPanel",       new Color(0.15f, 0.15f, 0.18f));
            var charPanel       = CreateTabPanel(contentArea.gameObject, "CharacterPanel",  new Color(0.13f, 0.13f, 0.16f));
            var homePanel       = CreateTabPanel(contentArea.gameObject, "HomePanel",       new Color(0.10f, 0.10f, 0.13f));
            var missionPanel    = CreateTabPanel(contentArea.gameObject, "MissionPanel",    new Color(0.12f, 0.12f, 0.15f));
            var settingsPanel   = CreateTabPanel(contentArea.gameObject, "SettingsPanel",   new Color(0.11f, 0.11f, 0.14f));

            // 4. 각 패널 내부 내용 구성
            var shopView     = SetupShopPanel(shopPanel);
            var charView     = SetupCharacterPanel(charPanel);
            var homeView     = SetupHomePanel(homePanel);
            var missionView  = SetupMissionPanel(missionPanel);
            var settingsView = SetupSettingsPanel(settingsPanel);

            // 5. 하단 탭 바
            var tabBar = CreateTabBar(root);

            // 6. LobbyTabView 컴포넌트 연결
            var tabViewGo    = new GameObject("LobbyTabView");
            tabViewGo.transform.SetParent(root.transform, false);
            var tabView      = tabViewGo.AddComponent<LobbyTabView>();
            tabView.Init(
                tabBar.buttons,
                new GameObject[] { shopPanel, charPanel, homePanel, missionPanel, settingsPanel }
            );

            // 7. Presenter 생성 및 Init
            var homePresenter = tabViewGo.AddComponent<HomeTabPresenter>();
            var charPresenter = tabViewGo.AddComponent<CharacterTabPresenter>();
            var settPresenter = tabViewGo.AddComponent<SettingsTabPresenter>();

            var lobbyPresenterGo = new GameObject("LobbyPresenter");
            lobbyPresenterGo.transform.SetParent(root.transform, false);
            var lobbyPresenter = lobbyPresenterGo.AddComponent<LobbyPresenter>();

            // 8. LobbyManager 연결
            var managerGo = new GameObject("LobbyManager");
            managerGo.transform.SetParent(root.transform, false);
            var lobbyManager = managerGo.AddComponent<LobbyManager>();
            lobbyManager.Init(
                lobbyPresenter,
                tabView,
                homeView,
                charView,
                shopView,
                missionView,
                settingsView,
                homePresenter,
                charPresenter,
                settPresenter
            );

            // 9. Dirty 처리
            EditorUtility.SetDirty(lobbyManager);
            EditorUtility.SetDirty(lobbyPresenter);
            EditorUtility.SetDirty(tabView);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Undo.RegisterCreatedObjectUndo(root, "Create Lobby HUD");
            Debug.Log("[LobbyHudSetup] LobbyHUD 생성 완료.");
        }

        // ── 탭 패널 내부 구성 ─────────────────────────────────────

        private static ShopTabView SetupShopPanel(GameObject panel)
        {
            var view = panel.AddComponent<ShopTabView>();
            CreateLabel(panel, "상점 준비 중", 48, TextAlignmentOptions.Center);
            return view;
        }

        private static CharacterTabView SetupCharacterPanel(GameObject panel)
        {
            var view = panel.AddComponent<CharacterTabView>();

            // 캐릭터 슬롯 4칸
            var slotContainer = CreateRectChild(panel, "CharacterSlots");
            SetAnchors(slotContainer, new Vector2(0f, 0.6f), new Vector2(1f, 1f));
            slotContainer.offsetMin = slotContainer.offsetMax = Vector2.zero;
            var hLayout = slotContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 20;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childForceExpandWidth  = true;
            hLayout.childForceExpandHeight = true;
            hLayout.padding = new RectOffset(20, 20, 20, 20);

            var rapierSlot = CreateCharacterSlot(slotContainer.gameObject, "RapierSlot",    "Rapier",      true);
            var slot2      = CreateCharacterSlot(slotContainer.gameObject, "CharSlot2",     "Warrior",     false);
            var slot3      = CreateCharacterSlot(slotContainer.gameObject, "CharSlot3",     "Assassin",    false);
            var slot4      = CreateCharacterSlot(slotContainer.gameObject, "CharSlot4",     "Ranger",      false);

            // B2 hook: EquipmentPanelRoot
            var equipRoot = CreateRectChild(panel, "EquipmentPanelRoot");
            SetAnchors(equipRoot, new Vector2(0f, 0.3f), new Vector2(1f, 0.6f));
            equipRoot.offsetMin = equipRoot.offsetMax = Vector2.zero;
            CreateLabel(equipRoot.gameObject, "[B2] 장비 패널 영역", 32, TextAlignmentOptions.Center,
                        new Color(0.5f, 0.8f, 0.5f, 0.6f));

            // B3 hook: LevelUpPanelRoot
            var levelRoot = CreateRectChild(panel, "LevelUpPanelRoot");
            SetAnchors(levelRoot, new Vector2(0f, 0f), new Vector2(1f, 0.3f));
            levelRoot.offsetMin = levelRoot.offsetMax = Vector2.zero;
            CreateLabel(levelRoot.gameObject, "[B3] 레벨업 패널 영역", 32, TextAlignmentOptions.Center,
                        new Color(0.5f, 0.6f, 0.9f, 0.6f));

            view.Init(rapierSlot, slot2, slot3, slot4, equipRoot.gameObject, levelRoot.gameObject);
            return view;
        }

        private static HomeTabView SetupHomePanel(GameObject panel)
        {
            var view = panel.AddComponent<HomeTabView>();

            // 스테이지 번호 텍스트 (중앙 상단)
            var stageText = CreateLabel(panel, "Stage 1", 64, TextAlignmentOptions.Center);
            var stageRect = stageText.GetComponent<RectTransform>();
            SetAnchors(stageRect, new Vector2(0f, 0.65f), new Vector2(1f, 0.85f));
            stageRect.offsetMin = stageRect.offsetMax = Vector2.zero;

            // 스테이지 진입 버튼 (하단 중앙)
            var enterBtn = CreateButton(panel, "EnterStageButton", "스테이지 진입",
                                        new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.22f));

            // 우편함 아이콘 플레이스홀더 (우측 상단)
            var mailboxGo = new GameObject("MailboxIconPlaceholder");
            mailboxGo.transform.SetParent(panel.transform, false);
            var mailImg = mailboxGo.AddComponent<Image>();
            mailImg.color = new Color(0.9f, 0.85f, 0.3f, 0.6f);
            var mailRect = mailboxGo.GetComponent<RectTransform>();
            SetAnchors(mailRect, new Vector2(0.78f, 0.88f), new Vector2(0.95f, 0.98f));
            mailRect.offsetMin = mailRect.offsetMax = Vector2.zero;
            CreateLabel(mailboxGo, "우편", 24, TextAlignmentOptions.Center);

            view.Init(stageText.GetComponent<TMP_Text>(), enterBtn.GetComponent<Button>(), mailboxGo);
            return view;
        }

        private static MissionTabView SetupMissionPanel(GameObject panel)
        {
            var view = panel.AddComponent<MissionTabView>();

            // B3 hook: MissionPanelRoot
            var missionRoot = CreateRectChild(panel, "MissionPanelRoot");
            SetAnchors(missionRoot, Vector2.zero, Vector2.one);
            missionRoot.offsetMin = missionRoot.offsetMax = Vector2.zero;
            CreateLabel(missionRoot.gameObject, "미션 준비 중\n[B3] MissionPanelRoot", 40,
                        TextAlignmentOptions.Center, new Color(0.9f, 0.7f, 0.3f, 0.8f));

            view.Init(missionRoot.gameObject);
            return view;
        }

        private static SettingsTabView SetupSettingsPanel(GameObject panel)
        {
            var view = panel.AddComponent<SettingsTabView>();

            // BGM 슬라이더
            var (bgmLabel, bgmSlider) = CreateLabeledSlider(panel, "BGM 볼륨", 0.78f, 0.86f);
            // SFX 슬라이더
            var (sfxLabel, sfxSlider) = CreateLabeledSlider(panel, "SFX 볼륨", 0.66f, 0.74f);
            // 진동 토글
            var (vibLabel, vibToggle) = CreateLabeledToggle(panel, "진동",     0.54f, 0.62f);
            // 밝기 슬라이더
            var (brightLabel, brightSlider) = CreateLabeledSlider(panel, "밝기", 0.42f, 0.50f);

            // B3 SaveManagerHook 안내 레이블
            CreateLabel(panel, "※ B3 완료 후 SaveManager를 연결하세요\n(_saveManagerHook 슬롯)",
                        24, TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.4f, 0.7f),
                        new Vector2(0f, 0.05f), new Vector2(1f, 0.18f));

            view.Init(bgmSlider, sfxSlider, vibToggle, brightSlider);
            return view;
        }

        // ── UI 헬퍼 메서드 ────────────────────────────────────────

        private static (TabBarData buttons) CreateTabBar(GameObject root)
        {
            var barGo  = new GameObject("TabBar");
            barGo.transform.SetParent(root.transform, false);
            var barImg = barGo.AddComponent<Image>();
            barImg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            var barRect = barGo.GetComponent<RectTransform>();
            SetAnchors(barRect, Vector2.zero, new Vector2(1f, 0f));
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = new Vector2(0f, 120f);

            var hLayout = barGo.AddComponent<HorizontalLayoutGroup>();
            hLayout.childForceExpandWidth  = true;
            hLayout.childForceExpandHeight = true;
            hLayout.spacing = 0;

            string[] labels = { "상점", "캐릭터", "홈", "미션", "설정" };
            var buttons = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var btnGo = new GameObject($"TabButton_{i + 1}_{labels[i]}");
                btnGo.transform.SetParent(barGo.transform, false);
                var btnImg = btnGo.AddComponent<Image>();
                btnImg.color = new Color(0.15f, 0.15f, 0.18f);
                var btn = btnGo.AddComponent<Button>();

                // 버튼 색상 트랜지션
                var colors       = btn.colors;
                colors.normalColor    = new Color(0.15f, 0.15f, 0.18f);
                colors.highlightedColor = new Color(0.25f, 0.25f, 0.3f);
                colors.pressedColor   = new Color(0.3f, 0.3f, 0.35f);
                btn.colors       = colors;

                // 레이블
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(btnGo.transform, false);
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.text      = labels[i];
                tmp.fontSize  = 28;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color     = new Color(0.7f, 0.7f, 0.7f);
                var labelRect = labelGo.GetComponent<RectTransform>();
                SetAnchors(labelRect, Vector2.zero, Vector2.one);
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

                // 레이아웃 요소
                btnGo.AddComponent<LayoutElement>();
                buttons[i] = btn;
            }

            return new TabBarData { buttons = buttons };
        }

        private struct TabBarData
        {
            public Button[] buttons;
        }

        private static RectTransform CreateRectChild(GameObject parent, string name)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go.AddComponent<RectTransform>();
        }

        private static RectTransform CreateRectChild(RectTransform parent, string name)
        {
            return CreateRectChild(parent.gameObject, name);
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot     = (min + max) * 0.5f;
        }

        private static GameObject CreateTabPanel(GameObject parent, string name, Color bgColor)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img  = go.AddComponent<Image>();
            img.color = bgColor;
            var rect = go.GetComponent<RectTransform>();
            SetAnchors(rect, Vector2.zero, Vector2.one);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject CreateLabel(
            GameObject parent,
            string text,
            float fontSize,
            TextAlignmentOptions alignment,
            Color? color = null,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null)
        {
            var go  = new GameObject("Label_" + text.Replace("\n", ""));
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = alignment;
            tmp.color     = color ?? Color.white;
            var rect = go.GetComponent<RectTransform>();
            SetAnchors(rect,
                anchorMin ?? new Vector2(0.05f, 0.3f),
                anchorMax ?? new Vector2(0.95f, 0.7f));
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject CreateButton(
            GameObject parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent.transform, false);
            var img  = btnGo.AddComponent<Image>();
            img.color = new Color(0.9f, 0.5f, 0.1f);
            var btn  = btnGo.AddComponent<Button>();
            var rect = btnGo.GetComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // 레이블
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var tmp  = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 40;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            var labelRect = labelGo.GetComponent<RectTransform>();
            SetAnchors(labelRect, Vector2.zero, Vector2.one);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            return btnGo;
        }

        private static GameObject CreateCharacterSlot(
            GameObject parent,
            string name,
            string charName,
            bool isActive)
        {
            var slotGo = new GameObject(name);
            slotGo.transform.SetParent(parent.transform, false);
            var img   = slotGo.AddComponent<Image>();
            img.color = isActive
                ? new Color(0.2f, 0.6f, 0.9f, 0.9f)
                : new Color(0.3f, 0.3f, 0.35f, 0.9f);

            var btn   = slotGo.AddComponent<Button>();
            btn.interactable = isActive;

            // 캐릭터 이름 레이블
            var nameGo = new GameObject("CharName");
            nameGo.transform.SetParent(slotGo.transform, false);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text      = charName;
            nameTmp.fontSize  = 28;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color     = Color.white;
            var nameRect = nameGo.GetComponent<RectTransform>();
            SetAnchors(nameRect, new Vector2(0f, 0.6f), Vector2.one);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;

            // Coming Soon 레이블
            var csGo  = new GameObject("ComingSoonLabel");
            csGo.transform.SetParent(slotGo.transform, false);
            var csTmp = csGo.AddComponent<TextMeshProUGUI>();
            csTmp.text      = "Coming\nSoon";
            csTmp.fontSize  = 22;
            csTmp.alignment = TextAlignmentOptions.Center;
            csTmp.color     = new Color(1f, 0.8f, 0.3f);
            var csRect = csGo.GetComponent<RectTransform>();
            SetAnchors(csRect, Vector2.zero, new Vector2(1f, 0.6f));
            csRect.offsetMin = csRect.offsetMax = Vector2.zero;
            csGo.SetActive(!isActive); // Rapier는 숨김, 나머지는 표시

            slotGo.AddComponent<LayoutElement>();
            return slotGo;
        }

        private static (GameObject label, Slider slider) CreateLabeledSlider(
            GameObject parent,
            string labelText,
            float anchorYMin,
            float anchorYMax)
        {
            var container = new GameObject($"Setting_{labelText}");
            container.transform.SetParent(parent.transform, false);
            var rect = container.AddComponent<RectTransform>();
            SetAnchors(rect, new Vector2(0.05f, anchorYMin), new Vector2(0.95f, anchorYMax));
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // 레이블
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(container.transform, false);
            var tmp   = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = labelText;
            tmp.fontSize  = 32;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color     = Color.white;
            var labelRect = labelGo.GetComponent<RectTransform>();
            SetAnchors(labelRect, Vector2.zero, new Vector2(0.3f, 1f));
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            // 슬라이더
            var sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(container.transform, false);
            var sliderRect = sliderGo.AddComponent<RectTransform>();
            SetAnchors(sliderRect, new Vector2(0.32f, 0.1f), new Vector2(1f, 0.9f));
            sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;

            // Background
            var bgGo   = new GameObject("Background");
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgImg  = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.3f, 0.3f, 0.35f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            SetAnchors(bgRect, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
            SetAnchors(fillAreaRect, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-15, 0);

            var fillGo   = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillImg  = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.9f, 0.6f, 0.1f);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

            // Handle Slide Area
            var handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var handleAreaRect = handleAreaGo.AddComponent<RectTransform>();
            SetAnchors(handleAreaRect, Vector2.zero, Vector2.one);
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handleGo   = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleImg  = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(20, 0);

            var slider          = sliderGo.AddComponent<Slider>();
            slider.fillRect     = fillRect;
            slider.handleRect   = handleRect;
            slider.targetGraphic = handleImg;
            slider.minValue     = 0f;
            slider.maxValue     = 1f;
            slider.value        = 1f;
            slider.direction    = Slider.Direction.LeftToRight;

            return (labelGo, slider);
        }

        private static (GameObject label, Toggle toggle) CreateLabeledToggle(
            GameObject parent,
            string labelText,
            float anchorYMin,
            float anchorYMax)
        {
            var container = new GameObject($"Setting_{labelText}");
            container.transform.SetParent(parent.transform, false);
            var rect = container.AddComponent<RectTransform>();
            SetAnchors(rect, new Vector2(0.05f, anchorYMin), new Vector2(0.95f, anchorYMax));
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // 레이블
            var labelGo  = new GameObject("Label");
            labelGo.transform.SetParent(container.transform, false);
            var tmp      = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = labelText;
            tmp.fontSize  = 32;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color     = Color.white;
            var labelRect = labelGo.GetComponent<RectTransform>();
            SetAnchors(labelRect, Vector2.zero, new Vector2(0.3f, 1f));
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            // 토글
            var toggleGo   = new GameObject("Toggle");
            toggleGo.transform.SetParent(container.transform, false);
            var toggleRect = toggleGo.AddComponent<RectTransform>();
            SetAnchors(toggleRect, new Vector2(0.32f, 0.1f), new Vector2(0.55f, 0.9f));
            toggleRect.offsetMin = toggleRect.offsetMax = Vector2.zero;

            var bgGo   = new GameObject("Background");
            bgGo.transform.SetParent(toggleGo.transform, false);
            var bgImg  = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.3f, 0.3f, 0.35f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            SetAnchors(bgRect, Vector2.zero, Vector2.one);
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            var checkGo   = new GameObject("Checkmark");
            checkGo.transform.SetParent(bgGo.transform, false);
            var checkImg  = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0.9f, 0.6f, 0.1f);
            var checkRect = checkGo.GetComponent<RectTransform>();
            SetAnchors(checkRect, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
            checkRect.offsetMin = checkRect.offsetMax = Vector2.zero;

            var toggle          = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic       = checkImg;
            toggle.isOn          = true;

            return (labelGo, toggle);
        }

        // ── EventSystem 생성 ──────────────────────────────────────
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            Debug.Log("[LobbyHudSetup] EventSystem(InputSystemUIInputModule) 생성.");
        }
    }
}
#endif
