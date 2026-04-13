#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TMPro;
using Game.UI;
using Game.UI.Lobby;
using Game.UI.Lobby.Equipment;

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

        [MenuItem("Rapier/Lobby/Create")]
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

        [MenuItem("Rapier/Lobby/Rebuild")]
        public static void RebuildLobbyHud()
        {
            var existing = GameObject.Find(ROOT_NAME);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);
            Build();
        }

        private const string FONT_ASSET_PATH =
            "Assets/_Project/ScriptableObjects/Fonts/NEXONLv1Gothic Regular SDF.asset";

        private static TMP_FontAsset _font;

        private static TMP_FontAsset GetFont()
        {
            if (_font == null)
            {
                _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
                if (_font == null)
                    Debug.LogError($"[LobbyHudSetup] NEXON 폰트 로드 실패: {FONT_ASSET_PATH}");
            }
            return _font;
        }

        // ── 메인 빌드 메서드 ──────────────────────────────────────
        private static void Build()
        {
            _font = null; // 매 빌드마다 재로드
            EnsureEventSystem();

            // 기존 LobbyCanvas 비활성화 (충돌 방지)
            var oldCanvas = GameObject.Find("LobbyCanvas");
            if (oldCanvas != null)
                oldCanvas.SetActive(false);

            // 1. Canvas 루트
            var root      = new GameObject(ROOT_NAME);
            var canvas    = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler    = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<SafeAreaFitter>();

            // 2. 탭 패널 영역 (탭 바 위쪽, 전체 화면에서 하단 바 높이 제외)
            var contentArea = CreateRectChild(root, "ContentArea");
            SetAnchors(contentArea, Vector2.zero, Vector2.one);
            contentArea.offsetMin = new Vector2(0, 180); // 하단 탭 바 높이
            contentArea.offsetMax = Vector2.zero;

            // 3. 5개 탭 패널 생성
            var shopPanel       = CreateTabPanel(contentArea.gameObject, "ShopPanel",       new Color(0.15f, 0.15f, 0.18f));
            var charPanel       = CreateTabPanel(contentArea.gameObject, "CharacterPanel",  new Color(0.13f, 0.13f, 0.16f));
            var homePanel       = CreateTabPanel(contentArea.gameObject, "HomePanel",       new Color(0.10f, 0.10f, 0.13f));
            var missionPanel    = CreateTabPanel(contentArea.gameObject, "MissionPanel",    new Color(0.12f, 0.12f, 0.15f));
            var settingsPanel   = CreateTabPanel(contentArea.gameObject, "SettingsPanel",   new Color(0.11f, 0.11f, 0.14f));

            // 4. 각 패널 내부 내용 구성
            var shopView                           = SetupShopPanel(shopPanel);
            var (charView, equipPresenter)         = SetupCharacterPanel(charPanel);
            var homeView                           = SetupHomePanel(homePanel);
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
            charPresenter.InitEquipmentPanel(equipPresenter);   // B2: 장비 패널 Presenter 연결
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
            EditorUtility.SetDirty(homePresenter);
            EditorUtility.SetDirty(charPresenter);    // _equipmentPanel [SerializeField] 직렬화 보장
            EditorUtility.SetDirty(equipPresenter);   // _view [SerializeField] 직렬화 보장
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

        private static (CharacterTabView view, EquipmentPanelPresenter equipPresenter) SetupCharacterPanel(GameObject panel)
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

            var rapierSlot = CreateCharacterSlot(slotContainer.gameObject, "RapierSlot",    "기사",      true);
            var slot2      = CreateCharacterSlot(slotContainer.gameObject, "AssassinSlot",  "암살자",    true);
            var slot3      = CreateCharacterSlot(slotContainer.gameObject, "CharSlot3",     "전사",      false);
            var slot4      = CreateCharacterSlot(slotContainer.gameObject, "CharSlot4",     "사냥꾼",    false);

            // B2: EquipmentPanelRoot — 장비 슬롯 8개 + 인벤토리 ScrollRect 실장
            var equipRoot = CreateRectChild(panel, "EquipmentPanelRoot");
            SetAnchors(equipRoot, new Vector2(0f, 0.3f), new Vector2(1f, 0.6f));
            equipRoot.offsetMin = equipRoot.offsetMax = Vector2.zero;

            // ── (a) 8슬롯 그리드 컨테이너 ─────────────────────────────────────
            var slotGrid = CreateRectChild(equipRoot, "SlotGrid");
            SetAnchors(slotGrid, new Vector2(0f, 0.55f), new Vector2(1f, 1f));
            slotGrid.offsetMin = slotGrid.offsetMax = Vector2.zero;
            var gridLayout           = slotGrid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize      = new Vector2(90f, 90f);
            gridLayout.spacing       = new Vector2(8f, 8f);
            gridLayout.padding       = new RectOffset(10, 10, 10, 10);
            gridLayout.constraint    = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 8;
            gridLayout.startAxis     = GridLayoutGroup.Axis.Horizontal;

            var slotViews = new List<EquipmentSlotView>();
            for (int i = 0; i < 8; i++)
            {
                var slotGo  = CreateRectChild(slotGrid, $"EquipmentSlot_{i}").gameObject;

                // 슬롯 배경 Image
                var slotBg  = slotGo.AddComponent<Image>();
                slotBg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

                // GradeBorder Image
                var borderGo  = new GameObject("GradeBorder");
                borderGo.transform.SetParent(slotGo.transform, false);
                var borderImg = borderGo.AddComponent<Image>();
                borderImg.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                var borderRect = borderGo.GetComponent<RectTransform>();
                SetAnchors(borderRect, Vector2.zero, Vector2.one);
                borderRect.offsetMin = borderRect.offsetMax = Vector2.zero;

                // EmptyIcon Image
                var emptyGo   = new GameObject("EmptyIcon");
                emptyGo.transform.SetParent(slotGo.transform, false);
                var emptyImg  = emptyGo.AddComponent<Image>();
                emptyImg.color = new Color(0.4f, 0.4f, 0.45f, 0.6f);
                var emptyRect = emptyGo.GetComponent<RectTransform>();
                SetAnchors(emptyRect, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
                emptyRect.offsetMin = emptyRect.offsetMax = Vector2.zero;

                // ItemIcon Image (기본 비활성)
                var iconGo  = new GameObject("ItemIcon");
                iconGo.transform.SetParent(slotGo.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.color = Color.white;
                var iconRect = iconGo.GetComponent<RectTransform>();
                SetAnchors(iconRect, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                iconGo.SetActive(false);

                // RuneSocket 아이콘 3개 (하단 행)
                var runeIcons = new List<Image>();
                for (int r = 0; r < 3; r++)
                {
                    var runeGo   = new GameObject($"RuneSocket_{r}");
                    runeGo.transform.SetParent(slotGo.transform, false);
                    var runeImg  = runeGo.AddComponent<Image>();
                    runeImg.color = Color.gray;
                    var runeRect = runeGo.GetComponent<RectTransform>();
                    float xMin = 0.05f + r * 0.32f;
                    SetAnchors(runeRect, new Vector2(xMin, 0.02f), new Vector2(xMin + 0.28f, 0.2f));
                    runeRect.offsetMin = runeRect.offsetMax = Vector2.zero;
                    runeGo.SetActive(false);
                    runeIcons.Add(runeImg);
                }

                // SlotButton
                var slotBtn = slotGo.AddComponent<Button>();

                // EquipmentSlotView 컴포넌트 추가 및 참조 주입
                var slotView = slotGo.AddComponent<EquipmentSlotView>();
                slotView.InitReferences(iconImg, borderImg, emptyImg, runeIcons, slotBtn);
                slotViews.Add(slotView);
            }

            // ── (b) 인벤토리 ScrollRect 영역 ───────────────────────────────────
            var scrollGo = CreateRectChild(equipRoot, "InventoryScroll");
            SetAnchors(scrollGo, new Vector2(0f, 0f), new Vector2(1f, 0.52f));
            scrollGo.offsetMin = scrollGo.offsetMax = Vector2.zero;

            // Scroll 배경
            var scrollBg = scrollGo.gameObject.AddComponent<Image>();
            scrollBg.color = new Color(0.15f, 0.15f, 0.18f, 0.8f);

            // Viewport — RectMask2D 로 사각형 클리핑 (Mask + clear Image 조합은
            // Image.alpha=0 으로 인해 전체 영역이 마스킹 아웃되는 버그가 있었음)
            var viewportGo = CreateRectChild(scrollGo, "Viewport");
            SetAnchors(viewportGo, Vector2.zero, Vector2.one);
            viewportGo.offsetMin = viewportGo.offsetMax = Vector2.zero;
            viewportGo.gameObject.AddComponent<RectMask2D>();

            // Content
            var contentGo = CreateRectChild(viewportGo, "Content");
            contentGo.anchorMin = new Vector2(0f, 1f);
            contentGo.anchorMax = new Vector2(1f, 1f);
            contentGo.pivot     = new Vector2(0.5f, 1f);
            contentGo.offsetMin = contentGo.offsetMax = Vector2.zero;
            var contentFitter = contentGo.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var contentLayout          = contentGo.gameObject.AddComponent<GridLayoutGroup>();
            contentLayout.cellSize     = new Vector2(140f, 140f);
            contentLayout.spacing      = new Vector2(18f, 18f);
            contentLayout.padding      = new RectOffset(6, 6, 6, 6);
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.constraint   = GridLayoutGroup.Constraint.FixedColumnCount;
            contentLayout.constraintCount = 6;

            // ScrollRect 설정
            var scrollRect        = scrollGo.gameObject.AddComponent<ScrollRect>();
            scrollRect.content    = contentGo;
            scrollRect.viewport   = viewportGo;
            scrollRect.horizontal = false;
            scrollRect.vertical   = true;

            // ── (c) InventoryItemTemplate (비활성 템플릿) ──────────────────────
            var templateGo  = CreateRectChild(contentGo, "InventoryItemTemplate").gameObject;
            var templateBg  = templateGo.AddComponent<Image>();
            templateBg.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);

            // GradeBackground
            var gradeBgGo  = new GameObject("GradeBackground");
            gradeBgGo.transform.SetParent(templateGo.transform, false);
            var gradeBgImg = gradeBgGo.AddComponent<Image>();
            gradeBgImg.color = new Color(0.3f, 0.3f, 0.35f);
            var gradeBgRect = gradeBgGo.GetComponent<RectTransform>();
            SetAnchors(gradeBgRect, Vector2.zero, Vector2.one);
            gradeBgRect.offsetMin = gradeBgRect.offsetMax = Vector2.zero;

            // ItemIcon (슬롯 전체를 채움)
            var tIconGo  = new GameObject("ItemIcon");
            tIconGo.transform.SetParent(templateGo.transform, false);
            var tIconImg = tIconGo.AddComponent<Image>();
            tIconImg.color = Color.white;
            var tIconRect = tIconGo.GetComponent<RectTransform>();
            SetAnchors(tIconRect, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
            tIconRect.offsetMin = tIconRect.offsetMax = Vector2.zero;

            // ItemButton
            var tBtn = templateGo.AddComponent<Button>();

            // InventoryItemView 컴포넌트 추가 및 참조 주입
            var itemViewTemplate = templateGo.AddComponent<InventoryItemView>();
            itemViewTemplate.InitReferences(tIconImg, gradeBgImg, tBtn);
            templateGo.SetActive(false);  // 템플릿은 비활성 유지

            // ── (d) 인벤토리 탭 버튼 3개 (Phase 24) ───────────────────────────
            var tabBarGo = new GameObject("InventoryTabBar");
            tabBarGo.transform.SetParent(equipRoot, false);
            var tabBarRect = tabBarGo.AddComponent<RectTransform>();
            SetAnchors(tabBarRect, new Vector2(0f, 0.0f), new Vector2(1f, 0.2f));
            tabBarRect.offsetMin = tabBarRect.offsetMax = Vector2.zero;
            var tabHLayout           = tabBarGo.AddComponent<HorizontalLayoutGroup>();
            tabHLayout.spacing       = 0f;
            tabHLayout.padding       = new RectOffset(0, 0, 0, 0);
            tabHLayout.childAlignment           = TextAnchor.MiddleCenter;
            tabHLayout.childControlWidth        = true;
            tabHLayout.childControlHeight       = true;
            tabHLayout.childForceExpandWidth    = true;
            tabHLayout.childForceExpandHeight   = true;

            (Button weaponTabBtn,    TextMeshProUGUI weaponTabTxt)    = CreateTabButtonPair(tabBarGo, "무기");
            (Button armorTabBtn,     TextMeshProUGUI armorTabTxt)     = CreateTabButtonPair(tabBarGo, "방어구");
            (Button accessoryTabBtn, TextMeshProUGUI accessoryTabTxt) = CreateTabButtonPair(tabBarGo, "장신구");

            // ScrollRect 위치를 탭 바 위로 (탭 바가 하단 0~0.2 차지)
            scrollGo.anchorMin = new Vector2(0f, 0.22f);
            scrollGo.anchorMax = new Vector2(1f, 1.0f);

            // ── EquipmentPanelView + Presenter 조립 ───────────────────────────
            var equipView = equipRoot.gameObject.AddComponent<EquipmentPanelView>();
            equipView.InitReferences(slotViews, contentGo.gameObject.transform, itemViewTemplate);
            equipView.InitTabReferences(
                weaponTabBtn, weaponTabTxt,
                armorTabBtn,  armorTabTxt,
                accessoryTabBtn, accessoryTabTxt);

            // ── (f) 룬 인벤토리 팝업 (Phase 24) — 먼저 생성 (itemDetail 에서 참조)
            var (runeInventoryView, runeInventoryPresenter, runeDetailPresenter) =
                CreateRuneInventoryPopup(panel, GetFont());

            // ── (e) 아이템 상세 팝업 (Phase 24) ────────────────────────────────
            var (itemDetailView, itemDetailPresenter) = CreateItemDetailPopup(panel, GetFont());
            // 룬 소켓 클릭 → 룬 인벤토리 팝업 연결
            itemDetailPresenter.InitReferences(itemDetailView, runeInventoryPresenter);

            var equipPresenter = equipRoot.gameObject.AddComponent<EquipmentPanelPresenter>();
            equipPresenter.InitReferences(equipView, itemDetailPresenter, runeInventoryPresenter);

            // 초기 상태: 패널 비활성 (CharacterTabPresenter.OnTabShown 에서 Show 호출)
            equipRoot.gameObject.SetActive(false);

            // B3 hook: LevelUpPanelRoot
            var levelRoot = CreateRectChild(panel, "LevelUpPanelRoot");
            SetAnchors(levelRoot, new Vector2(0f, 0f), new Vector2(1f, 0.3f));
            levelRoot.offsetMin = levelRoot.offsetMax = Vector2.zero;
            CreateLabel(levelRoot.gameObject, "[B3] 레벨업 패널 영역", 32, TextAlignmentOptions.Center,
                        new Color(0.5f, 0.6f, 0.9f, 0.6f));

            view.Init(rapierSlot, slot2, slot3, slot4, equipRoot.gameObject, levelRoot.gameObject);
            return (view, equipPresenter);
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
            CreateLabel(mailboxGo, "우편", 32, TextAlignmentOptions.Center);

            // 미리보기 영역 컨테이너 (stageText 아래, enterBtn 위)
            var previewArea = new GameObject("StagePreviewArea");
            previewArea.transform.SetParent(panel.transform, false);
            var previewAreaRect = previewArea.AddComponent<RectTransform>();
            SetAnchors(previewAreaRect, new Vector2(0f, 0.24f), new Vector2(1f, 0.63f));
            previewAreaRect.offsetMin = previewAreaRect.offsetMax = Vector2.zero;

            // 미리보기 패널 (중앙, 좌우 화살표 공간 확보)
            var previewPanel = new GameObject("StagePreviewPanel");
            previewPanel.transform.SetParent(previewArea.transform, false);
            var previewImg = previewPanel.AddComponent<Image>();
            previewImg.color = new Color(0.15f, 0.15f, 0.20f, 0.7f);
            var previewRect = previewPanel.GetComponent<RectTransform>();
            SetAnchors(previewRect, new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.95f));
            previewRect.offsetMin = previewRect.offsetMax = Vector2.zero;
            // placeholder 텍스트
            CreateLabel(previewPanel, "스테이지 미리보기", 32, TextAlignmentOptions.Center,
                        new Color(0.5f, 0.5f, 0.5f, 0.5f));

            // 왼쪽 화살표 (◀)
            var leftArrowGo = new GameObject("LeftArrowButton");
            leftArrowGo.transform.SetParent(previewArea.transform, false);
            var leftArrowImg = leftArrowGo.AddComponent<Image>();
            leftArrowImg.color = new Color(0.3f, 0.3f, 0.4f);
            var leftArrowRect = leftArrowGo.GetComponent<RectTransform>();
            SetAnchors(leftArrowRect, new Vector2(0.02f, 0.3f), new Vector2(0.13f, 0.7f));
            leftArrowRect.offsetMin = leftArrowRect.offsetMax = Vector2.zero;
            var leftBtn = leftArrowGo.AddComponent<Button>();
            CreateLabel(leftArrowGo, "◀", 48, TextAlignmentOptions.Center);

            // 오른쪽 화살표 (▶)
            var rightArrowGo = new GameObject("RightArrowButton");
            rightArrowGo.transform.SetParent(previewArea.transform, false);
            var rightArrowImg = rightArrowGo.AddComponent<Image>();
            rightArrowImg.color = new Color(0.3f, 0.3f, 0.4f);
            var rightArrowRect = rightArrowGo.GetComponent<RectTransform>();
            SetAnchors(rightArrowRect, new Vector2(0.87f, 0.3f), new Vector2(0.98f, 0.7f));
            rightArrowRect.offsetMin = rightArrowRect.offsetMax = Vector2.zero;
            var rightBtn = rightArrowGo.AddComponent<Button>();
            CreateLabel(rightArrowGo, "▶", 48, TextAlignmentOptions.Center);

            view.Init(stageText.GetComponent<TMP_Text>(), enterBtn.GetComponent<Button>(), mailboxGo,
                      leftBtn, rightBtn, previewPanel);

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

            view.Init(bgmSlider, sfxSlider, vibToggle, brightSlider);
            return view;
        }

        // ── UI 헬퍼 메서드 ────────────────────────────────────────

        private static TabBarData CreateTabBar(GameObject root)
        {
            var barGo  = new GameObject("TabBar");
            barGo.transform.SetParent(root.transform, false);
            var barImg = barGo.AddComponent<Image>();
            barImg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            var barRect = barGo.GetComponent<RectTransform>();
            SetAnchors(barRect, Vector2.zero, new Vector2(1f, 0f));
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = new Vector2(0f, 180f);

            var hLayout = barGo.AddComponent<HorizontalLayoutGroup>();
            hLayout.childControlWidth      = true;
            hLayout.childControlHeight     = true;
            hLayout.childForceExpandWidth  = true;
            hLayout.childForceExpandHeight = true;
            hLayout.padding = new RectOffset(0, 0, 0, 0);
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
                tmp.fontSize  = 40;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color     = new Color(0.7f, 0.7f, 0.7f);
                var tabFont = GetFont();
                if (tabFont != null) tmp.font = tabFont;
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
            var labelFont = GetFont();
            if (labelFont != null) tmp.font = labelFont;
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
            var btnFont = GetFont();
            if (btnFont != null) tmp.font = btnFont;
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
            nameTmp.fontSize  = 32;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color     = Color.white;
            var charFont = GetFont();
            if (charFont != null) nameTmp.font = charFont;
            var nameRect = nameGo.GetComponent<RectTransform>();
            SetAnchors(nameRect, new Vector2(0f, 0.6f), Vector2.one);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;

            // Coming Soon 레이블
            var csGo  = new GameObject("ComingSoonLabel");
            csGo.transform.SetParent(slotGo.transform, false);
            var csTmp = csGo.AddComponent<TextMeshProUGUI>();
            csTmp.text      = "Coming\nSoon";
            csTmp.fontSize  = 32;
            csTmp.alignment = TextAlignmentOptions.Center;
            csTmp.color     = new Color(1f, 0.8f, 0.3f);
            var csFont = GetFont();
            if (csFont != null) csTmp.font = csFont;
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
            var sliderFont = GetFont();
            if (sliderFont != null) tmp.font = sliderFont;
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
            var toggleFont = GetFont();
            if (toggleFont != null) tmp.font = toggleFont;
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

        // ── Phase 24 헬퍼: 탭 버튼 쌍 생성 ──────────────────────────

        private static (Button btn, TextMeshProUGUI txt) CreateTabButtonPair(GameObject parent, string label)
        {
            var btnGo = new GameObject($"TabBtn_{label}");
            btnGo.transform.SetParent(parent.transform, false);
            var bg    = btnGo.AddComponent<Image>();
            bg.color  = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            var btn   = btnGo.AddComponent<Button>();
            btnGo.AddComponent<LayoutElement>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var tmp     = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 32f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.5f, 0.5f, 0.5f);
            var f = GetFont();
            if (f != null) tmp.font = f;
            var labelRect = labelGo.GetComponent<RectTransform>();
            SetAnchors(labelRect, Vector2.zero, Vector2.one);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            return (btn, tmp);
        }

        // ── Phase 24 헬퍼: 아이템 상세 팝업 생성 ────────────────────

        private static (ItemDetailPopupView view, ItemDetailPopupPresenter presenter)
            CreateItemDetailPopup(GameObject panelParent, TMP_FontAsset font)
        {
            var popupGo = new GameObject("ItemDetailPopup", typeof(RectTransform));
            popupGo.transform.SetParent(panelParent.transform, false);

            // 불투명 배경 (다크)
            var bg   = popupGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, 1f);
            var rt   = popupGo.GetComponent<RectTransform>();
            SetAnchors(rt, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.95f));
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Block input below (GraphicRaycaster already on root canvas)

            // 1. 아이템 이름 (상단)
            var nameGo  = CreateTmpLabel(popupGo, "ItemName", "아이템 이름", 36f, font);
            SetAnchors(nameGo.GetComponent<RectTransform>(), new Vector2(0f, 0.88f), new Vector2(1f, 0.98f));
            nameGo.GetComponent<RectTransform>().offsetMin = nameGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 2. 아이콘 (좌)
            var iconGo  = new GameObject("ItemIcon");
            iconGo.transform.SetParent(popupGo.transform, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.white;
            SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.62f), new Vector2(0.28f, 0.88f));
            iconGo.GetComponent<RectTransform>().offsetMin = iconGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 3. 스탯창 (우 — 고정 크기 영역)
            var statPanel = new GameObject("StatPanel", typeof(RectTransform));
            statPanel.transform.SetParent(popupGo.transform, false);
            SetAnchors(statPanel.GetComponent<RectTransform>(), new Vector2(0.30f, 0.62f), new Vector2(0.98f, 0.88f));
            statPanel.GetComponent<RectTransform>().offsetMin = statPanel.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var mainStatGo = CreateTmpLabel(statPanel, "MainStat", "메인 스탯", 32f, font);
            SetAnchors(mainStatGo.GetComponent<RectTransform>(), new Vector2(0f, 0.75f), new Vector2(1f, 1f));
            mainStatGo.GetComponent<RectTransform>().offsetMin = mainStatGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            mainStatGo.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.9f, 0.5f);

            var subStatTexts = new List<TextMeshProUGUI>();
            for (int i = 0; i < 3; i++)
            {
                float yMax = 0.72f - i * 0.25f;
                float yMin = yMax - 0.23f;
                var subGo  = CreateTmpLabel(statPanel, $"SubStat_{i}", string.Empty, 32f, font);
                SetAnchors(subGo.GetComponent<RectTransform>(), new Vector2(0f, yMin), new Vector2(1f, yMax));
                subGo.GetComponent<RectTransform>().offsetMin = subGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                subGo.GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.8f, 0.8f);
                subStatTexts.Add(subGo.GetComponent<TextMeshProUGUI>());
            }

            // 4. 룬 소켓 행
            var runeRowGo = new GameObject("RuneRow", typeof(RectTransform));
            runeRowGo.transform.SetParent(popupGo.transform, false);
            SetAnchors(runeRowGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.50f), new Vector2(0.98f, 0.61f));
            runeRowGo.GetComponent<RectTransform>().offsetMin = runeRowGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            var runeHLayout           = runeRowGo.AddComponent<HorizontalLayoutGroup>();
            runeHLayout.spacing       = 6f;
            runeHLayout.childAlignment             = TextAnchor.MiddleLeft;
            runeHLayout.childForceExpandHeight     = true;
            runeHLayout.padding = new RectOffset(4, 4, 2, 2);

            var runeSocketBtns  = new List<Button>();
            var runeSocketImgs  = new List<Image>();
            for (int i = 0; i < 3; i++)
            {
                var sockGo  = new GameObject($"RuneSocket_{i}");
                sockGo.transform.SetParent(runeRowGo.transform, false);
                var sockBg  = sockGo.AddComponent<Image>();
                sockBg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
                var sockBtn = sockGo.AddComponent<Button>();
                var sockLe  = sockGo.AddComponent<LayoutElement>();
                sockLe.preferredWidth  = 60f;
                sockLe.preferredHeight = 60f;
                sockLe.flexibleWidth   = 0f;

                var innerGo  = new GameObject("Icon");
                innerGo.transform.SetParent(sockGo.transform, false);
                var innerImg = innerGo.AddComponent<Image>();
                innerImg.color = Color.gray;
                var innerRect = innerGo.GetComponent<RectTransform>();
                SetAnchors(innerRect, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
                innerRect.offsetMin = innerRect.offsetMax = Vector2.zero;
                sockGo.SetActive(false);  // 등급에 따라 Presenter 가 활성화

                runeSocketBtns.Add(sockBtn);
                runeSocketImgs.Add(innerImg);
            }

            // 5. 설명
            var descGo = CreateTmpLabel(popupGo, "Description", "설명 텍스트", 32f, font);
            var descTmp = descGo.GetComponent<TextMeshProUGUI>();
            descTmp.color = new Color(0.7f, 0.7f, 0.7f);
            descTmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            SetAnchors(descGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.24f), new Vector2(0.98f, 0.49f));
            descGo.GetComponent<RectTransform>().offsetMin = descGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 6. 장착/닫기 버튼 행
            var equipBtn = CreateSimpleButton(popupGo, "EquipButton", "장착",
                new Vector2(0.05f, 0.02f), new Vector2(0.55f, 0.14f), new Color(0.2f, 0.7f, 0.3f), font);
            var closeBtn = CreateSimpleButton(popupGo, "CloseButton", "닫기",
                new Vector2(0.60f, 0.02f), new Vector2(0.95f, 0.14f), new Color(0.5f, 0.2f, 0.2f), font);

            var view = popupGo.AddComponent<ItemDetailPopupView>();
            view.InitReferences(
                nameGo.GetComponent<TextMeshProUGUI>(),
                iconImg,
                mainStatGo.GetComponent<TextMeshProUGUI>(),
                subStatTexts,
                runeSocketBtns,
                runeSocketImgs,
                descTmp,
                equipBtn.GetComponent<Button>(),
                equipBtn.GetComponentInChildren<TextMeshProUGUI>(),
                closeBtn.GetComponent<Button>());

            var presenter = popupGo.AddComponent<ItemDetailPopupPresenter>();
            presenter.InitReferences(view);

            popupGo.SetActive(false);
            return (view, presenter);
        }

        // ── Phase 24 헬퍼: 룬 인벤토리 팝업 생성 ───────────────────

        private static (RuneInventoryPopupView view, RuneInventoryPopupPresenter presenter,
                         RuneDetailPopupPresenter runeDetailPresenter)
            CreateRuneInventoryPopup(GameObject panelParent, TMP_FontAsset font)
        {
            // 룬 인벤토리 팝업
            var popupGo = new GameObject("RuneInventoryPopup", typeof(RectTransform));
            popupGo.transform.SetParent(panelParent.transform, false);
            var popBg   = popupGo.AddComponent<Image>();
            popBg.color = new Color(0.08f, 0.08f, 0.10f, 1f);
            var popRt   = popupGo.GetComponent<RectTransform>();
            SetAnchors(popRt, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.97f));
            popRt.offsetMin = popRt.offsetMax = Vector2.zero;

            // 탭 버튼 행
            var tabRowGo = new GameObject("TabRow", typeof(RectTransform));
            tabRowGo.transform.SetParent(popupGo.transform, false);
            SetAnchors(tabRowGo.GetComponent<RectTransform>(), new Vector2(0f, 0.88f), new Vector2(1f, 0.98f));
            tabRowGo.GetComponent<RectTransform>().offsetMin = tabRowGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            var tabHLay           = tabRowGo.AddComponent<HorizontalLayoutGroup>();
            tabHLay.spacing       = 4f;
            tabHLay.childForceExpandWidth  = true;
            tabHLay.childForceExpandHeight = true;
            tabHLay.padding = new RectOffset(4, 4, 2, 2);

            (Button rapierTab,   TextMeshProUGUI rapierTabTxt)   = CreateTabButtonPair(tabRowGo, "Rapier");
            (Button assassinTab, TextMeshProUGUI assassinTabTxt) = CreateTabButtonPair(tabRowGo, "Assassin");

            // 룬 목록 ScrollRect
            var scrollGo = new GameObject("RuneScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(popupGo.transform, false);
            SetAnchors(scrollGo.GetComponent<RectTransform>(), new Vector2(0f, 0.15f), new Vector2(1f, 0.87f));
            scrollGo.GetComponent<RectTransform>().offsetMin = scrollGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            scrollGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.8f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            SetAnchors(viewportGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            viewportGo.GetComponent<RectTransform>().offsetMin = viewportGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing   = 4f;
            vLayout.padding   = new RectOffset(4, 4, 4, 4);
            vLayout.childForceExpandWidth  = true;
            vLayout.childForceExpandHeight = false;

            var scrollRect        = scrollGo.AddComponent<ScrollRect>();
            scrollRect.content    = contentRt;
            scrollRect.viewport   = viewportGo.GetComponent<RectTransform>();
            scrollRect.horizontal = false;
            scrollRect.vertical   = true;

            // 룬 행 템플릿
            var rowTemplate = CreateRuneRowTemplate(contentGo, font);
            rowTemplate.gameObject.SetActive(false);

            // 해제/닫기 버튼
            var unequipBtn = CreateSimpleButton(popupGo, "UnequipButton", "해제",
                new Vector2(0.05f, 0.01f), new Vector2(0.50f, 0.12f), new Color(0.7f, 0.3f, 0.3f), font);
            var closeBtn   = CreateSimpleButton(popupGo, "CloseButton", "닫기",
                new Vector2(0.55f, 0.01f), new Vector2(0.95f, 0.12f), new Color(0.3f, 0.3f, 0.5f), font);

            var view = popupGo.AddComponent<RuneInventoryPopupView>();
            view.InitReferences(
                rapierTab, rapierTabTxt,
                assassinTab, assassinTabTxt,
                contentRt,
                rowTemplate,
                unequipBtn.GetComponent<Button>(),
                closeBtn.GetComponent<Button>());

            // 룬 상세 팝업
            var (runeDetailView, runeDetailPresenter) = CreateRuneDetailPopup(panelParent, font);

            var presenter = popupGo.AddComponent<RuneInventoryPopupPresenter>();
            presenter.InitReferences(view, runeDetailPresenter);

            popupGo.SetActive(false);
            return (view, presenter, runeDetailPresenter);
        }

        private static RuneItemRowView CreateRuneRowTemplate(GameObject parent, TMP_FontAsset font)
        {
            var rowGo  = new GameObject("RuneRowTemplate", typeof(RectTransform));
            rowGo.transform.SetParent(parent.transform, false);
            var rowBg  = rowGo.AddComponent<Image>();
            rowBg.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
            var rowLe  = rowGo.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 70f;
            rowLe.flexibleWidth   = 1f;
            rowGo.AddComponent<Button>();

            var iconGo  = new GameObject("RuneIcon");
            iconGo.transform.SetParent(rowGo.transform, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.cyan;
            SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.01f, 0.1f), new Vector2(0.15f, 0.9f));
            iconGo.GetComponent<RectTransform>().offsetMin = iconGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var nameGo  = CreateTmpLabel(rowGo, "RuneName", "룬 이름", 32f, font);
            SetAnchors(nameGo.GetComponent<RectTransform>(), new Vector2(0.17f, 0.5f), new Vector2(1f, 0.95f));
            nameGo.GetComponent<RectTransform>().offsetMin = nameGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var effectGo = CreateTmpLabel(rowGo, "EffectText", "효과", 32f, font);
            effectGo.GetComponent<TextMeshProUGUI>().color = new Color(0.7f, 0.7f, 0.7f);
            SetAnchors(effectGo.GetComponent<RectTransform>(), new Vector2(0.17f, 0.05f), new Vector2(1f, 0.5f));
            effectGo.GetComponent<RectTransform>().offsetMin = effectGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var row = rowGo.AddComponent<RuneItemRowView>();
            row.InitReferences(iconImg, nameGo.GetComponent<TextMeshProUGUI>(), effectGo.GetComponent<TextMeshProUGUI>(), rowGo.GetComponent<Button>());
            return row;
        }

        // ── Phase 24 헬퍼: 룬 상세 팝업 생성 ───────────────────────

        private static (RuneDetailPopupView view, RuneDetailPopupPresenter presenter)
            CreateRuneDetailPopup(GameObject panelParent, TMP_FontAsset font)
        {
            var popupGo = new GameObject("RuneDetailPopup", typeof(RectTransform));
            popupGo.transform.SetParent(panelParent.transform, false);
            popupGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);
            var rt = popupGo.GetComponent<RectTransform>();
            SetAnchors(rt, new Vector2(0.08f, 0.15f), new Vector2(0.92f, 0.88f));
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 룬 이름
            var nameGo = CreateTmpLabel(popupGo, "RuneName", "룬 이름", 34f, font);
            SetAnchors(nameGo.GetComponent<RectTransform>(), new Vector2(0f, 0.84f), new Vector2(1f, 0.98f));
            nameGo.GetComponent<RectTransform>().offsetMin = nameGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 룬 아이콘
            var iconGo  = new GameObject("RuneIcon");
            iconGo.transform.SetParent(popupGo.transform, false);
            iconGo.AddComponent<Image>().color = Color.cyan;
            SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.30f, 0.55f), new Vector2(0.70f, 0.84f));
            iconGo.GetComponent<RectTransform>().offsetMin = iconGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 효과 설명
            var effectGo  = CreateTmpLabel(popupGo, "EffectText", "효과 설명", 32f, font);
            var effectTmp = effectGo.GetComponent<TextMeshProUGUI>();
            effectTmp.color = new Color(0.8f, 0.8f, 0.8f);
            effectTmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            SetAnchors(effectGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.22f), new Vector2(0.98f, 0.54f));
            effectGo.GetComponent<RectTransform>().offsetMin = effectGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 버튼
            var equipBtn = CreateSimpleButton(popupGo, "EquipButton", "장착",
                new Vector2(0.05f, 0.03f), new Vector2(0.55f, 0.17f), new Color(0.2f, 0.7f, 0.3f), font);
            var closeBtn = CreateSimpleButton(popupGo, "CloseButton", "닫기",
                new Vector2(0.60f, 0.03f), new Vector2(0.95f, 0.17f), new Color(0.5f, 0.2f, 0.2f), font);

            var view = popupGo.AddComponent<RuneDetailPopupView>();
            view.InitReferences(
                nameGo.GetComponent<TextMeshProUGUI>(),
                iconGo.GetComponent<Image>(),
                effectTmp,
                equipBtn.GetComponent<Button>(),
                equipBtn.GetComponentInChildren<TextMeshProUGUI>(),
                closeBtn.GetComponent<Button>());

            var presenter = popupGo.AddComponent<RuneDetailPopupPresenter>();
            presenter.InitReferences(view);

            popupGo.SetActive(false);
            return (view, presenter);
        }

        // ── Phase 24 공통 UI 헬퍼 ──────────────────────────────────

        private static GameObject CreateTmpLabel(
            GameObject parent, string name, string text, float fontSize, TMP_FontAsset font)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            if (font != null) tmp.font = font;
            else Debug.LogWarning($"[LobbyHudSetup] TMP '{name}' — font null (feedback_tmp_font_unset)");
            go.AddComponent<RectTransform>(); // 이미 있으면 noop
            return go;
        }

        private static GameObject CreateSimpleButton(
            GameObject parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax,
            Color bgColor, TMP_FontAsset font)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent.transform, false);
            btnGo.AddComponent<Image>().color = bgColor;
            btnGo.AddComponent<Button>();
            var rt = btnGo.GetComponent<RectTransform>();
            SetAnchors(rt, anchorMin, anchorMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var lGo  = new GameObject("Label");
            lGo.transform.SetParent(btnGo.transform, false);
            var tmp  = lGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 32f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            if (font != null) tmp.font = font;
            var lr = lGo.GetComponent<RectTransform>();
            SetAnchors(lr, Vector2.zero, Vector2.one);
            lr.offsetMin = lr.offsetMax = Vector2.zero;

            return btnGo;
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
