#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TMPro;
using Game.Characters;
using Game.Core;
using Game.Core.Services;
using Game.Data.Equipment;
using Game.Data.Gacha;
using Game.UI;
using Game.UI.Lobby;
using Game.UI.Lobby.Equipment;
using Game.UI.Lobby.Shop;

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

            // 2. 탭 패널 영역 (탭 바 위쪽, 전체 화면에서 하단 바 높이 제외)
            // 배경 패널은 full-screen 유지 — SafeAreaFitter 는 각 패널 내부 콘텐츠에만 적용
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
            var (shopView, shopPresenter)          = SetupShopPanel(shopPanel);
            var (charView, equipPresenter)         = SetupCharacterPanel(charPanel);
            var homeView                           = SetupHomePanel(homePanel);
            var missionView  = SetupMissionPanel(missionPanel);
            var settingsView = SetupSettingsPanel(settingsPanel);

            // 5. 하단 탭 바 (root 하위 — 배경과 동일 레벨, safe area 미적용)
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
            // Phase 23c: CharacterInfoPanelPresenter 는 SetupCharacterPanel 내부에서 panel 에 붙어 있으므로
            // GetComponentInChildren 으로 회수해 주입한다.
            var infoPanelPresenter = charPanel.GetComponentInChildren<CharacterInfoPanelPresenter>(true);
            charPresenter.InitInfoPanel(infoPanelPresenter);
            charPresenter.InitEquipmentPanel(equipPresenter);   // B2: 하위 호환 유지
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
                settPresenter,
                shopPresenter
            );

            // 9. Dirty 처리
            if (shopPresenter != null) EditorUtility.SetDirty(shopPresenter);
            EditorUtility.SetDirty(lobbyManager);
            EditorUtility.SetDirty(lobbyPresenter);
            EditorUtility.SetDirty(tabView);
            EditorUtility.SetDirty(homePresenter);
            EditorUtility.SetDirty(charPresenter);          // [SerializeField] 직렬화 보장
            EditorUtility.SetDirty(equipPresenter);         // _view [SerializeField] 직렬화 보장
            if (infoPanelPresenter != null) EditorUtility.SetDirty(infoPanelPresenter);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Undo.RegisterCreatedObjectUndo(root, "Create Lobby HUD");
            Debug.Log("[LobbyHudSetup] LobbyHUD 생성 완료.");
        }

        // ── 탭 패널 내부 구성 ─────────────────────────────────────

        private static (ShopTabView view, ShopTabPresenter shopPresenter) SetupShopPanel(GameObject panel)
        {
            var font = GetFont();
            var view = panel.AddComponent<ShopTabView>();

            // ── SafeAreaInset ──────────────────────────────────────────────────
            var safeInset = new GameObject("SafeAreaInset", typeof(RectTransform));
            safeInset.transform.SetParent(panel.transform, false);
            var safeRect = safeInset.GetComponent<RectTransform>();
            SetAnchors(safeRect, Vector2.zero, Vector2.one);
            safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
            safeInset.AddComponent<SafeAreaFitter>();

            // ── CurrencyHeader (상단 80px) ─────────────────────────────────────
            var headerGo = new GameObject("CurrencyHeader", typeof(RectTransform));
            headerGo.transform.SetParent(safeInset.transform, false);
            var headerRect = headerGo.GetComponent<RectTransform>();
            SetAnchors(headerRect, new Vector2(0, 1), Vector2.one);
            headerRect.pivot     = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0, 80);
            var headerLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childAlignment      = TextAnchor.MiddleRight;
            headerLayout.spacing             = 30;
            headerLayout.padding             = new RectOffset(20, 20, 0, 0);
            headerLayout.childForceExpandWidth  = false;
            headerLayout.childForceExpandHeight = false;

            // 티켓 라벨
            var ticketLabel = CreateTmpLabel(headerGo, "TicketLabel", "🎫 x0", 28, font).GetComponent<TextMeshProUGUI>();
            var ticketLE = ticketLabel.gameObject.AddComponent<LayoutElement>();
            ticketLE.preferredWidth  = 150;
            ticketLE.preferredHeight = 60;

            // Crystal 라벨
            var crystalLabel = CreateTmpLabel(headerGo, "CrystalLabel", "💎 x0", 28, font).GetComponent<TextMeshProUGUI>();
            var crystalLE = crystalLabel.gameObject.AddComponent<LayoutElement>();
            crystalLE.preferredWidth  = 200;
            crystalLE.preferredHeight = 60;

            // ── BannerScrollView ───────────────────────────────────────────────
            var scrollGo = new GameObject("BannerScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(safeInset.transform, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            SetAnchors(scrollRect, Vector2.zero, Vector2.one);
            scrollRect.offsetMin = new Vector2(0, 0);
            scrollRect.offsetMax = new Vector2(0, -80); // 헤더 아래부터
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical   = true;

            // Viewport — RectMask2D 로 사각형 클리핑
            // (Mask + clear Image 조합은 alpha=0 Image 가 전체 영역을 마스킹 아웃시키는 버그)
            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            SetAnchors(viewportRect, Vector2.zero, Vector2.one);
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();
            scroll.viewport = viewportRect;

            // BannerContainer (VerticalLayoutGroup)
            var containerGo = new GameObject("BannerContainer", typeof(RectTransform));
            containerGo.transform.SetParent(viewportGo.transform, false);
            var containerRect = containerGo.GetComponent<RectTransform>();
            SetAnchors(containerRect, new Vector2(0, 1), new Vector2(1, 1));
            containerRect.pivot     = new Vector2(0.5f, 1f);
            containerRect.sizeDelta = new Vector2(0, 0);
            var vlg = containerGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing             = 20;
            vlg.padding             = new RectOffset(20, 20, 20, 20);
            vlg.childAlignment      = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            var fitter = containerGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = containerRect;

            // ── Toast (비활성 시작) ────────────────────────────────────────────
            var toastTextGo = CreateTmpLabel(panel, "Toast", "", 32, font);
            var toastText = toastTextGo.GetComponent<TextMeshProUGUI>();
            toastText.alignment = TextAlignmentOptions.Center;
            toastText.color     = Color.yellow;
            var toastTransform = toastTextGo.GetComponent<RectTransform>();
            SetAnchors(toastTransform, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.55f));
            toastTransform.offsetMin = toastTransform.offsetMax = Vector2.zero;
            toastTextGo.SetActive(false);

            // ── GachaResultModal (비활성 시작) ─────────────────────────────────
            var (resultModalView, resultModalPresenter) = CreateGachaResultModal(panel, font);

            // ── GachaShopData 로드 → 배너 카드 생성 ───────────────────────────
            var shopData = AssetDatabase.LoadAssetAtPath<GachaShopData>(
                "Assets/_Project/Resources/GachaShopData.asset");

            if (shopData != null && shopData.Banners != null)
            {
                foreach (var banner in shopData.Banners)
                {
                    if (banner == null) continue;
                    var card = CreateBannerCard(containerGo, banner, font);
                    view.RegisterBannerCard(card);
                }
            }
            else
            {
                Debug.LogWarning("[LobbyHudSetup] GachaShopData not found — 배너 없이 Shop 패널 생성");
            }

            // ── View InitReferences ────────────────────────────────────────────
            view.InitReferences(ticketLabel, crystalLabel, scroll, containerGo.transform, toastText);

            // ── ShopTabPresenter ───────────────────────────────────────────────
            var presenterGo = new GameObject("ShopTabPresenter", typeof(RectTransform));
            presenterGo.transform.SetParent(panel.transform, false);
            var shopPresenter = presenterGo.AddComponent<ShopTabPresenter>();

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(shopPresenter);

            return (view, shopPresenter);
        }

        /// <summary>
        /// 배너 카드 1장 생성. 부모 VLG 가 가로를 stretch.
        /// 세로는 LayoutElement.preferredHeight 로 지정 (1080 기준 약 2:1).
        /// 내부 자식은 anchor 비율 배분.
        /// </summary>
        private static BannerCardView CreateBannerCard(GameObject container, GachaBannerData bannerData, TMP_FontAsset font)
        {
            // ── 카드 루트 ─────────────────────────────────────────────────────
            var cardGo = new GameObject($"BannerCard_{bannerData.BannerId}", typeof(RectTransform));
            cardGo.transform.SetParent(container.transform, false);
            // VLG(childForceExpandWidth) 가 가로를 부모 폭으로 확장.
            // 세로만 LayoutElement 로 지정. 1080 기준 패딩 제외 ~1040 가로, 500 세로 ≈ 2:1.
            var cardLE = cardGo.AddComponent<LayoutElement>();
            cardLE.preferredHeight = 500;

            // 배경
            var cardBg = cardGo.AddComponent<Image>();
            cardBg.color = new Color(0.16f, 0.16f, 0.20f, 0.95f);

            // ── 자식 배치 (anchor 비율 배분) ──────────────────────────────────
            // 카드 내부 영역을 anchor 비율로 분할. 패딩은 offset 으로 처리.
            // 상→하 배분 (정규화 0~1):
            //   BannerArt :  0.30 ~ 1.00  (상단 70%)
            //   BannerName:  0.22 ~ 0.30  (8%)
            //   Description: 0.12 ~ 0.22  (10%)
            //   Divider:     0.115~ 0.12  (0.5%)
            //   ButtonRow:   0.00 ~ 0.115 (11.5%)
            const float pad = 20f; // 좌우/상하 여백 (offset)

            // ── BannerArt (상단 70%) ──────────────────────────────────────────
            var artGo = new GameObject("BannerArt", typeof(RectTransform));
            artGo.transform.SetParent(cardGo.transform, false);
            var artRect = artGo.GetComponent<RectTransform>();
            SetAnchors(artRect, new Vector2(0, 0.30f), Vector2.one);
            artRect.offsetMin = new Vector2(pad, 5);
            artRect.offsetMax = new Vector2(-pad, -pad);
            var artImg = artGo.AddComponent<Image>();

            if (bannerData.BannerArt != null)
            {
                artImg.sprite = bannerData.BannerArt;
                artImg.preserveAspect = true;
                artImg.color = Color.white;
            }
            else
            {
                // placeholder — 등급 확률 안내
                artImg.color = new Color(0.12f, 0.12f, 0.16f);

                var rateGo = new GameObject("RateInfo", typeof(RectTransform));
                rateGo.transform.SetParent(artGo.transform, false);
                var rateRect = rateGo.GetComponent<RectTransform>();
                SetAnchors(rateRect, Vector2.zero, Vector2.one);
                rateRect.offsetMin = new Vector2(20, 20);
                rateRect.offsetMax = new Vector2(-20, -20);

                var rateTmp = rateGo.AddComponent<TextMeshProUGUI>();
                rateTmp.font      = font;
                rateTmp.fontSize  = 28;
                rateTmp.alignment = TextAlignmentOptions.Center;
                rateTmp.color     = new Color(0.85f, 0.85f, 0.85f);

                float totalWeight = 0f;
                foreach (var entry in bannerData.GradeEntries)
                    totalWeight += entry.Weight;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<size=36><b>장비 가챠</b></size>\n");
                foreach (var entry in bannerData.GradeEntries)
                {
                    float pct = totalWeight > 0 ? entry.Weight / totalWeight * 100f : 0f;
                    string gradeColor = entry.Grade switch
                    {
                        EquipmentGrade.Normal => "#CCCCCC",
                        EquipmentGrade.Rare   => "#4CA6FF",
                        EquipmentGrade.Epic   => "#C864FF",
                        EquipmentGrade.Unique => "#FFB830",
                        _                     => "#FFFFFF"
                    };
                    sb.AppendLine($"<color={gradeColor}>★ {entry.Grade}  —  {pct:F1}%</color>");
                }
                rateTmp.text = sb.ToString();
            }

            // ── BannerName (8%) ───────────────────────────────────────────────
            var nameGo = new GameObject("BannerName", typeof(RectTransform));
            nameGo.transform.SetParent(cardGo.transform, false);
            var nameRect = nameGo.GetComponent<RectTransform>();
            SetAnchors(nameRect, new Vector2(0, 0.22f), new Vector2(1, 0.30f));
            nameRect.offsetMin = new Vector2(pad, 0);
            nameRect.offsetMax = new Vector2(-pad, 0);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.font      = font;
            nameText.fontSize  = 40;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.color     = Color.white;
            nameText.text      = bannerData.BannerName;

            // ── Description (10%) ─────────────────────────────────────────────
            var descGo = new GameObject("Description", typeof(RectTransform));
            descGo.transform.SetParent(cardGo.transform, false);
            var descRect = descGo.GetComponent<RectTransform>();
            SetAnchors(descRect, new Vector2(0, 0.12f), new Vector2(1, 0.22f));
            descRect.offsetMin = new Vector2(pad, 0);
            descRect.offsetMax = new Vector2(-pad, 0);
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.font      = font;
            descText.fontSize  = 26;
            descText.alignment = TextAlignmentOptions.TopLeft;
            descText.color     = new Color(0.75f, 0.75f, 0.75f);
            descText.text      = bannerData.Description;

            // ── Divider (0.5%) ────────────────────────────────────────────────
            var divGo = new GameObject("Divider", typeof(RectTransform));
            divGo.transform.SetParent(cardGo.transform, false);
            var divRect = divGo.GetComponent<RectTransform>();
            SetAnchors(divRect, new Vector2(0, 0.115f), new Vector2(1, 0.12f));
            divRect.offsetMin = new Vector2(pad, 0);
            divRect.offsetMax = new Vector2(-pad, 0);
            var divImg = divGo.AddComponent<Image>();
            divImg.color = new Color(0.4f, 0.4f, 0.45f, 0.5f);

            // ── ButtonRow (하단 11.5%) ────────────────────────────────────────
            var btnRowGo = new GameObject("ButtonRow", typeof(RectTransform));
            btnRowGo.transform.SetParent(cardGo.transform, false);
            var btnRowRect = btnRowGo.GetComponent<RectTransform>();
            SetAnchors(btnRowRect, Vector2.zero, new Vector2(1, 0.115f));
            btnRowRect.offsetMin = new Vector2(pad, pad * 0.5f);
            btnRowRect.offsetMax = new Vector2(-pad, 0);

            // 버튼 2개를 좌우 반반 배치 (anchor 비율)
            var (singleBtn, singleCostText) = CreatePullButton(btnRowGo, "SinglePullBtn", "1회 뽑기",
                new Vector2(0f, 0f), new Vector2(0.48f, 1f), font);
            var (tenBtn, tenCostText)       = CreatePullButton(btnRowGo, "TenPullBtn",    "10회 뽑기",
                new Vector2(0.52f, 0f), new Vector2(1f, 1f), font);

            // ── BannerCardView ────────────────────────────────────────────────
            var cardView = cardGo.AddComponent<BannerCardView>();
            cardView.InitReferences(artImg, nameText, descText, singleBtn, tenBtn, singleCostText, tenCostText);
            cardView.Refresh(bannerData);

            return cardView;
        }

        /// <summary>
        /// 뽑기 버튼 1개 생성. 부모 내에서 anchor 로 위치·크기 결정.
        /// </summary>
        private static (Button btn, TextMeshProUGUI costText) CreatePullButton(
            GameObject parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font)
        {
            var btnGo = new GameObject(name, typeof(RectTransform));
            btnGo.transform.SetParent(parent.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            SetAnchors(btnRect, anchorMin, anchorMax);
            btnRect.offsetMin = btnRect.offsetMax = Vector2.zero;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.25f, 0.45f, 0.7f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            // 라벨 (상단 55%)
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            SetAnchors(labelRect, new Vector2(0, 0.4f), Vector2.one);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.font      = font;
            labelTmp.fontSize  = 30;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color     = Color.white;
            labelTmp.text      = label;

            // 비용 (하단 40%)
            var costGo = new GameObject("Cost", typeof(RectTransform));
            costGo.transform.SetParent(btnGo.transform, false);
            var costRect = costGo.GetComponent<RectTransform>();
            SetAnchors(costRect, Vector2.zero, new Vector2(1, 0.4f));
            costRect.offsetMin = costRect.offsetMax = Vector2.zero;
            var costTmp = costGo.AddComponent<TextMeshProUGUI>();
            costTmp.font      = font;
            costTmp.fontSize  = 26;
            costTmp.alignment = TextAlignmentOptions.Center;
            costTmp.color     = new Color(1f, 0.9f, 0.3f);

            return (btn, costTmp);
        }

        private static (GachaResultModalView view, GachaResultModalPresenter presenter)
            CreateGachaResultModal(GameObject parent, TMP_FontAsset font)
        {
            // 모달 루트 (비활성 시작)
            var modalGo = new GameObject("GachaResultModal", typeof(RectTransform));
            modalGo.transform.SetParent(parent.transform, false);
            var modalRect = modalGo.GetComponent<RectTransform>();
            SetAnchors(modalRect, Vector2.zero, Vector2.one);
            modalRect.offsetMin = modalRect.offsetMax = Vector2.zero;

            // 별도 Canvas (sortingOrder 500)
            var modalCanvas = modalGo.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder    = 500;
            modalGo.AddComponent<GraphicRaycaster>();

            // Dimmer (반투명 검정)
            var dimmer = new GameObject("Dimmer", typeof(RectTransform));
            dimmer.transform.SetParent(modalGo.transform, false);
            var dimmerRect = dimmer.GetComponent<RectTransform>();
            SetAnchors(dimmerRect, Vector2.zero, Vector2.one);
            dimmerRect.offsetMin = dimmerRect.offsetMax = Vector2.zero;
            var dimmerImg = dimmer.AddComponent<Image>();
            dimmerImg.color = new Color(0, 0, 0, 0.7f);

            // Flash Image (연출용)
            var flashGo = new GameObject("FlashImage", typeof(RectTransform));
            flashGo.transform.SetParent(modalGo.transform, false);
            var flashRect = flashGo.GetComponent<RectTransform>();
            SetAnchors(flashRect, Vector2.zero, Vector2.one);
            flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.color         = new Color(1, 1, 1, 0);
            flashImg.raycastTarget = false;
            flashGo.SetActive(false);

            // ResultPanel (중앙)
            var resultPanel = new GameObject("ResultPanel", typeof(RectTransform));
            resultPanel.transform.SetParent(modalGo.transform, false);
            var resultRect = resultPanel.GetComponent<RectTransform>();
            SetAnchors(resultRect, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f));
            resultRect.offsetMin = resultRect.offsetMax = Vector2.zero;
            var resultBg = resultPanel.AddComponent<Image>();
            resultBg.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);

            // ItemGrid
            var gridGo = new GameObject("ItemGrid", typeof(RectTransform));
            gridGo.transform.SetParent(resultPanel.transform, false);
            var gridRect = gridGo.GetComponent<RectTransform>();
            SetAnchors(gridRect, new Vector2(0, 0.15f), Vector2.one);
            gridRect.offsetMin = new Vector2(10, 0);
            gridRect.offsetMax = new Vector2(-10, -10);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize        = new Vector2(180, 220);
            grid.spacing         = new Vector2(15, 15);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment  = TextAnchor.UpperCenter;
            grid.padding         = new RectOffset(10, 10, 10, 10);

            // CloseButton
            var closeBtnGo = new GameObject("CloseButton", typeof(RectTransform));
            closeBtnGo.transform.SetParent(resultPanel.transform, false);
            var closeBtnRect = closeBtnGo.GetComponent<RectTransform>();
            SetAnchors(closeBtnRect, new Vector2(0.2f, 0.02f), new Vector2(0.8f, 0.12f));
            closeBtnRect.offsetMin = closeBtnRect.offsetMax = Vector2.zero;
            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.4f, 0.2f, 0.2f);
            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            CreateTmpLabel(closeBtnGo, "Label", "닫기", 28, font);

            // GachaResultModalView
            var modalView = modalGo.AddComponent<GachaResultModalView>();
            modalView.InitReferences(gridGo.transform, closeBtn, flashImg, font);

            // GachaResultModalPresenter
            var presenterGo = new GameObject("GachaResultPresenter");
            presenterGo.transform.SetParent(modalGo.transform, false);
            var presenter = presenterGo.AddComponent<GachaResultModalPresenter>();
            presenter.InitReferences(modalView);

            modalGo.SetActive(false); // 비활성 시작

            EditorUtility.SetDirty(modalView);
            EditorUtility.SetDirty(presenter);

            return (modalView, presenter);
        }

        private static (CharacterTabView view, EquipmentPanelPresenter equipPresenter) SetupCharacterPanel(GameObject panel)
        {
            var view = panel.AddComponent<CharacterTabView>();

            // SafeAreaInset — 배경(panel)은 full-screen, 위젯은 Safe Area 인셋 안에만 배치
            var safeInset = new GameObject("SafeAreaInset", typeof(RectTransform));
            safeInset.transform.SetParent(panel.transform, false);
            var safeInsetRect = safeInset.GetComponent<RectTransform>();
            SetAnchors(safeInsetRect, Vector2.zero, Vector2.one);
            safeInsetRect.offsetMin = safeInsetRect.offsetMax = Vector2.zero;
            safeInset.AddComponent<SafeAreaFitter>();

            // ── 캐릭터 정보 패널 (Phase 23c) ───────────────────────────────────
            // 패널 레이아웃 (2:2:1): 상단 40% = 캐릭터 정보, 중간 40% = 인벤토리+탭바, 하단 20% = B3
            var infoPanel = new GameObject("CharacterInfoPanel", typeof(RectTransform));
            infoPanel.transform.SetParent(safeInset.transform, false);
            var infoBg   = infoPanel.AddComponent<Image>();
            infoBg.color = new Color(0.10f, 0.10f, 0.13f, 1.0f);
            var infoRect = infoPanel.GetComponent<RectTransform>();
            // 비율 재조정: CharInfo 47% / EquipPanel 33% / LevelUp 20%
            // (3행 × 120px + spacing + tabbar 딱 맞게 역산 → equipPanel 33% 필요)
            SetAnchors(infoRect, new Vector2(0f, 0.53f), new Vector2(1f, 1f));
            infoRect.offsetMin = infoRect.offsetMax = Vector2.zero;

            // 일러스트 (Raycast Target off)
            var illustGo   = new GameObject("CharacterIllustration", typeof(RectTransform));
            illustGo.transform.SetParent(infoPanel.transform, false);
            var illustImg  = illustGo.AddComponent<Image>();
            illustImg.color         = new Color(0f, 0f, 0f, 0f); // sprite 없으므로 투명
            illustImg.raycastTarget = false;
            var illustRect = illustGo.GetComponent<RectTransform>();
            SetAnchors(illustRect, Vector2.zero, Vector2.one);
            illustRect.offsetMin = illustRect.offsetMax = Vector2.zero;

            // 좌측 슬롯 컨테이너 (Weapon / Necklace / Ring — 세로 3칸)
            var leftColumnGo = new GameObject("LeftSlotColumn", typeof(RectTransform));
            leftColumnGo.transform.SetParent(infoPanel.transform, false);
            var leftRect = leftColumnGo.GetComponent<RectTransform>();
            SetAnchors(leftRect, new Vector2(0.00f, 0.00f), new Vector2(0.18f, 1.00f));
            leftRect.offsetMin = leftRect.offsetMax = Vector2.zero;
            var leftVLayout           = leftColumnGo.AddComponent<VerticalLayoutGroup>();
            leftVLayout.childAlignment        = TextAnchor.UpperCenter;
            leftVLayout.childForceExpandWidth  = false;
            leftVLayout.childForceExpandHeight = false;
            leftVLayout.childControlWidth      = true;  // preferredHeight 적용 (false면 RectTransform 기본 100 사용)
            leftVLayout.childControlHeight     = true;  // preferredHeight 적용 (false면 RectTransform 기본 100 사용)
            leftVLayout.spacing               = 8f;
            // padding.top = SLOT_SIZE / 2 (반칸 상단 여백, SLOT_SIZE=120 → 60)
            leftVLayout.padding               = new RectOffset(4, 4, 60, 4);

            // 우측 슬롯 컨테이너 (Hat / Top / Bottom / Gloves / Shoes — 세로 5칸)
            var rightColumnGo = new GameObject("RightSlotColumn", typeof(RectTransform));
            rightColumnGo.transform.SetParent(infoPanel.transform, false);
            var rightRect = rightColumnGo.GetComponent<RectTransform>();
            SetAnchors(rightRect, new Vector2(0.82f, 0.00f), new Vector2(1.00f, 1.00f));
            rightRect.offsetMin = rightRect.offsetMax = Vector2.zero;
            var rightVLayout           = rightColumnGo.AddComponent<VerticalLayoutGroup>();
            rightVLayout.childAlignment        = TextAnchor.UpperCenter;
            rightVLayout.childForceExpandWidth  = false;
            rightVLayout.childForceExpandHeight = false;
            rightVLayout.childControlWidth      = true;  // preferredHeight 적용 (false면 RectTransform 기본 100 사용)
            rightVLayout.childControlHeight     = true;  // preferredHeight 적용 (false면 RectTransform 기본 100 사용)
            rightVLayout.spacing               = 8f;
            // padding.top = SLOT_SIZE / 2 (반칸 상단 여백, SLOT_SIZE=120 → 60)
            rightVLayout.padding               = new RectOffset(4, 4, 60, 4);

            // 슬롯 크기 (장착 슬롯 + 인벤토리 동일 120)
            const float SLOT_SIZE = 120f;

            // 좌측 3슬롯: Weapon, Necklace, Ring
            var leftSlotViews = new EquipmentSlotView[3];
            string[] leftSlotNames  = { "Weapon", "Necklace", "Ring" };
            for (int i = 0; i < 3; i++)
                leftSlotViews[i] = CreateEquipmentSlotGo(leftColumnGo, leftSlotNames[i], SLOT_SIZE);

            // 우측 5슬롯: Hat, Top, Bottom, Gloves, Shoes
            var rightSlotViews = new EquipmentSlotView[5];
            string[] rightSlotNames = { "Hat", "Top", "Bottom", "Gloves", "Shoes" };
            for (int i = 0; i < 5; i++)
                rightSlotViews[i] = CreateEquipmentSlotGo(rightColumnGo, rightSlotNames[i], SLOT_SIZE);

            // 캐릭터 변경 버튼 (중하단, 일러스트를 약간 가림)
            var changeBtnGo = new GameObject("ChangeCharacterButton", typeof(RectTransform));
            changeBtnGo.transform.SetParent(infoPanel.transform, false);
            var changeBtnImg  = changeBtnGo.AddComponent<Image>();
            changeBtnImg.color = new Color(0.15f, 0.45f, 0.80f, 0.9f);
            var changeBtnBtn  = changeBtnGo.AddComponent<Button>();
            var changeBtnRect = changeBtnGo.GetComponent<RectTransform>();
            SetAnchors(changeBtnRect, new Vector2(0.25f, 0.02f), new Vector2(0.75f, 0.15f));
            changeBtnRect.offsetMin = changeBtnRect.offsetMax = Vector2.zero;
            var changeLabelGo = CreateTmpLabel(changeBtnGo, "Label_캐릭터변경", "캐릭터 변경", 36f, GetFont());
            var changeLabelRect = changeLabelGo.GetComponent<RectTransform>();
            SetAnchors(changeLabelRect, Vector2.zero, Vector2.one);
            changeLabelRect.offsetMin = changeLabelRect.offsetMax = Vector2.zero;

            // CharacterInfoPanelView 컴포넌트 부착 및 참조 주입
            var infoPanelView = infoPanel.AddComponent<CharacterInfoPanelView>();
            infoPanelView.InitReferences(
                illustImg, changeBtnBtn,
                leftSlotViews[0],  // Weapon
                leftSlotViews[1],  // Necklace
                leftSlotViews[2],  // Ring
                rightSlotViews[0], // Hat
                rightSlotViews[1], // Top
                rightSlotViews[2], // Bottom
                rightSlotViews[3], // Gloves
                rightSlotViews[4]  // Shoes
            );

            // ── 캐릭터 변경 모달 ───────────────────────────────────────────────
            var modal = BuildCharacterSelectModal(safeInset);

            // CharacterSelectModalPresenter
            var modalPresenter = panel.AddComponent<CharacterSelectModalPresenter>();

            // CharacterStatData 로드
            const string RAPIER_DATA_PATH   = "Assets/_Project/ScriptableObjects/Characters/RapierStatData.asset";
            const string ASSASSIN_DATA_PATH = "Assets/_Project/ScriptableObjects/Characters/AssassinStatData.asset";
            const string WARRIOR_DATA_PATH  = "Assets/_Project/ScriptableObjects/Characters/WarriorStatData.asset";
            const string RANGER_DATA_PATH   = "Assets/_Project/ScriptableObjects/Characters/RangerStatData.asset";
            var rapierData   = AssetDatabase.LoadAssetAtPath<CharacterStatData>(RAPIER_DATA_PATH);
            var assassinData = AssetDatabase.LoadAssetAtPath<CharacterStatData>(ASSASSIN_DATA_PATH);
            var warriorData  = AssetDatabase.LoadAssetAtPath<CharacterStatData>(WARRIOR_DATA_PATH);
            var rangerData   = AssetDatabase.LoadAssetAtPath<CharacterStatData>(RANGER_DATA_PATH);
            if (rapierData   == null) Debug.LogWarning($"[LobbyHudSetup] RapierStatData 로드 실패: {RAPIER_DATA_PATH}");
            if (assassinData == null) Debug.LogWarning($"[LobbyHudSetup] AssassinStatData 로드 실패: {ASSASSIN_DATA_PATH}");
            if (warriorData  == null) Debug.LogWarning($"[LobbyHudSetup] WarriorStatData 로드 실패: {WARRIOR_DATA_PATH}");
            if (rangerData   == null) Debug.LogWarning($"[LobbyHudSetup] RangerStatData 로드 실패: {RANGER_DATA_PATH}");

            modalPresenter.InitReferences(modal, rapierData, assassinData, warriorData, rangerData);

            // B2: EquipmentPanelRoot — 장비 슬롯 8개 + 인벤토리 ScrollRect 실장
            var equipRoot = CreateRectChild(safeInset, "EquipmentPanelRoot");
            SetAnchors(equipRoot, new Vector2(0f, 0.20f), new Vector2(1f, 0.53f));
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
            // 장착 슬롯과 동일 크기(120), 7열 → 3행이 scroll viewport에 딱 맞게
            contentLayout.cellSize     = new Vector2(120f, 120f);
            contentLayout.spacing      = new Vector2(18f, 18f);
            contentLayout.padding      = new RectOffset(6, 6, 6, 6);
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.constraint   = GridLayoutGroup.Constraint.FixedColumnCount;
            contentLayout.constraintCount = 7;
            contentGo.gameObject.AddComponent<GridLayoutFiller>();

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
            // Phase 23c: 기존 8칸 그리드(slotGrid)는 숨기고, 좌3/우5 슬롯을 사용한다.
            // leftSlotViews(Weapon/Necklace/Ring) + rightSlotViews(Hat/Top/Bottom/Gloves/Shoes)
            // → EquipmentSlotType enum 순서: Weapon, Hat, Top, Bottom, Shoes, Gloves, Necklace, Ring
            slotGrid.gameObject.SetActive(false); // 구식 그리드 폐기 (비활성)
            var charInfoSlotViews = new List<EquipmentSlotView>
            {
                leftSlotViews[0],  // Weapon
                rightSlotViews[0], // Hat
                rightSlotViews[1], // Top
                rightSlotViews[2], // Bottom
                rightSlotViews[4], // Shoes
                rightSlotViews[3], // Gloves
                leftSlotViews[1],  // Necklace
                leftSlotViews[2],  // Ring
            };
            var equipView = equipRoot.gameObject.AddComponent<EquipmentPanelView>();
            equipView.InitReferences(charInfoSlotViews, contentGo.gameObject.transform, itemViewTemplate);
            equipView.InitTabReferences(
                weaponTabBtn, weaponTabTxt,
                armorTabBtn,  armorTabTxt,
                accessoryTabBtn, accessoryTabTxt);

            // ── (f) 룬 인벤토리 팝업 (Phase 24) — 먼저 생성 (itemDetail 에서 참조)
            var (runeInventoryView, runeInventoryPresenter, runeDetailPresenter) =
                CreateRuneInventoryPopup(panel, GetFont());

            // ── (e) 아이템 상세 팝업 (Phase 24/25-C) ──────────────────────────────
            var (itemDetailView, itemDetailPresenter) = CreateItemDetailPopup(panel, GetFont());

            // ── (e-2) 강화 모달 (Phase 25-C) ───────────────────────────────────
            var (enhanceModalView, enhanceModalPresenter) = CreateEnhanceModal(panel, GetFont());

            // 룬 소켓 클릭 → 룬 인벤토리 팝업 연결 + 강화 모달 연결
            itemDetailPresenter.InitReferences(itemDetailView, runeInventoryPresenter, enhanceModalPresenter);

            // EnhanceModal → ItemDetailPresenter 역참조 (서브스탯 펄스 힌트 전달용)
            enhanceModalPresenter.InitReferences(enhanceModalView, itemDetailPresenter);

            // ── (g) Phase 25-B: EquipmentActionBar ──────────────────────────────
            // EquipmentPanelRoot 의 탭바(0~0.22)와 스크롤(0.22~1.0) 사이에 ActionBar 삽입.
            // 탭바 위치를 하단 0~0.14 로 내리고, ActionBar 가 0.14~0.22 차지, 스크롤 0.22~1.0 유지.
            // (기존 tabBarRect 는 0~0.2 이므로 0~0.14 로 재조정)
            var tabBarRect2 = tabBarGo.GetComponent<RectTransform>();
            SetAnchors(tabBarRect2, new Vector2(0f, 0.0f), new Vector2(1f, 0.14f));

            var (actionBarView, actionBarPresenter, resultModalPresenter) =
                CreateActionBarAndModal(equipRoot.gameObject, panel, GetFont());

            // ActionBar 는 탭바(0~0.14) 바로 위, 스크롤(0.22~1.0) 바로 아래 = 0.14~0.22
            var actionBarRect = actionBarView.GetComponent<RectTransform>();
            SetAnchors(actionBarRect, new Vector2(0f, 0.14f), new Vector2(1f, 0.22f));
            actionBarRect.offsetMin = actionBarRect.offsetMax = Vector2.zero;

            // 스크롤 위치는 이미 tabBarRect 재조정으로 맞춰져 있으므로 유지.

            var equipPresenter = equipRoot.gameObject.AddComponent<EquipmentPanelPresenter>();
            equipPresenter.InitReferences(equipView, itemDetailPresenter, runeInventoryPresenter);
            equipPresenter.InitActionBar(actionBarPresenter);
            EditorUtility.SetDirty(actionBarPresenter);
            EditorUtility.SetDirty(resultModalPresenter);

            EditorUtility.SetDirty(enhanceModalPresenter);

            // 초기 상태: 패널 비활성 (CharacterInfoPanelPresenter.Show 에서 Show 호출)
            equipRoot.gameObject.SetActive(false);

            // B3 hook: LevelUpPanelRoot (2:2:1 하단 20%)
            var levelRoot = CreateRectChild(safeInset, "LevelUpPanelRoot");
            SetAnchors(levelRoot, new Vector2(0f, 0f), new Vector2(1f, 0.20f));
            levelRoot.offsetMin = levelRoot.offsetMax = Vector2.zero;
            CreateLabel(levelRoot.gameObject, "[B3] 레벨업 패널 영역", 32, TextAlignmentOptions.Center,
                        new Color(0.5f, 0.6f, 0.9f, 0.6f));

            // CharacterInfoPanelPresenter 조립
            var infoPanelPresenter = panel.AddComponent<CharacterInfoPanelPresenter>();
            infoPanelPresenter.InitReferences(infoPanelView, modalPresenter, equipPresenter, rapierData, assassinData, warriorData, rangerData);
            EditorUtility.SetDirty(infoPanelPresenter);
            EditorUtility.SetDirty(modalPresenter);

            // EquipmentPanelView の 8슬롯을 CharacterInfoPanelView 슬롯으로 재초기화
            // (좌3/우5 슬롯이 EquipmentPanelView 와는 별개로 직접 EquipmentSlotView 를 가짐)
            // EquipmentPanelView 의 _slotViews 는 기존 8슬롯 그리드 전용으로 유지.

            view.Init(equipRoot.gameObject, levelRoot.gameObject);
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

        // ── Phase 23c 헬퍼: EquipmentSlotGo (VerticalLayout 자식용) ────────

        /// <summary>VerticalLayoutGroup 자식으로 넣을 단일 장비 슬롯 GO + EquipmentSlotView 를 생성한다.</summary>
        private static EquipmentSlotView CreateEquipmentSlotGo(GameObject parent, string slotName, float size)
        {
            var slotGo  = new GameObject($"Slot_{slotName}", typeof(RectTransform));
            slotGo.transform.SetParent(parent.transform, false);

            // LayoutElement — preferredWidth/Height 고정 (VerticalLayout expand 차단)
            var le            = slotGo.AddComponent<LayoutElement>();
            le.preferredWidth  = size;
            le.preferredHeight = size;
            le.flexibleWidth   = 0f;
            le.flexibleHeight  = 0f;

            // 슬롯 배경 Image
            var slotBg  = slotGo.AddComponent<Image>();
            slotBg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

            // GradeBorder
            var borderGo  = new GameObject("GradeBorder", typeof(RectTransform));
            borderGo.transform.SetParent(slotGo.transform, false);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            var borderRect = borderGo.GetComponent<RectTransform>();
            SetAnchors(borderRect, Vector2.zero, Vector2.one);
            borderRect.offsetMin = borderRect.offsetMax = Vector2.zero;

            // EmptyIcon
            var emptyGo  = new GameObject("EmptyIcon", typeof(RectTransform));
            emptyGo.transform.SetParent(slotGo.transform, false);
            var emptyImg = emptyGo.AddComponent<Image>();
            emptyImg.color = new Color(0.4f, 0.4f, 0.45f, 0.6f);
            var emptyRect = emptyGo.GetComponent<RectTransform>();
            SetAnchors(emptyRect, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            emptyRect.offsetMin = emptyRect.offsetMax = Vector2.zero;

            // ItemIcon
            var iconGo  = new GameObject("ItemIcon", typeof(RectTransform));
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.white;
            var iconRect = iconGo.GetComponent<RectTransform>();
            SetAnchors(iconRect, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            iconGo.SetActive(false);

            // RuneSocket 아이콘 3개
            var runeIcons = new System.Collections.Generic.List<Image>();
            for (int r = 0; r < 3; r++)
            {
                var runeGo   = new GameObject($"RuneSocket_{r}", typeof(RectTransform));
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

            // EquipmentSlotView
            var slotView = slotGo.AddComponent<EquipmentSlotView>();
            slotView.InitReferences(iconImg, borderImg, emptyImg, runeIcons, slotBtn);
            return slotView;
        }

        // ── Phase 23c 헬퍼: 캐릭터 변경 모달 생성 ──────────────────────────

        /// <summary>CharacterPanel 하위에 캐릭터 변경 모달 오버레이를 생성한다.</summary>
        private static CharacterSelectModalView BuildCharacterSelectModal(GameObject panelParent)
        {
            var font = GetFont();

            // 모달 오버레이 루트 (ContentArea 전체를 가림)
            var modalGo = new GameObject("CharacterSelectModal", typeof(RectTransform));
            modalGo.transform.SetParent(panelParent.transform, false);
            var modalBg   = modalGo.AddComponent<Image>();
            modalBg.color = new Color(0.0f, 0.0f, 0.0f, 0.88f);
            // raycastTarget=true (기본값) 이므로 모달 뒤 클릭은 Image 가 차단한다.
            var modalRect = modalGo.GetComponent<RectTransform>();
            SetAnchors(modalRect, Vector2.zero, Vector2.one);
            modalRect.offsetMin = modalRect.offsetMax = Vector2.zero;

            // 일러스트 영역 (모달 전체를 채움)
            var illustGo   = new GameObject("ModalIllustration", typeof(RectTransform));
            illustGo.transform.SetParent(modalGo.transform, false);
            var illustImg  = illustGo.AddComponent<Image>();
            illustImg.color         = new Color(0.15f, 0.15f, 0.20f, 0.5f); // placeholder
            illustImg.raycastTarget = false;
            var illustRect = illustGo.GetComponent<RectTransform>();
            SetAnchors(illustRect, Vector2.zero, Vector2.one);
            illustRect.offsetMin = illustRect.offsetMax = Vector2.zero;

            // 좌 화살표
            var leftArrowGo = new GameObject("LeftArrowButton", typeof(RectTransform));
            leftArrowGo.transform.SetParent(modalGo.transform, false);
            leftArrowGo.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 0.8f);
            var leftArrowBtn  = leftArrowGo.AddComponent<Button>();
            var leftArrowRect = leftArrowGo.GetComponent<RectTransform>();
            SetAnchors(leftArrowRect, new Vector2(0.01f, 0.35f), new Vector2(0.12f, 0.65f));
            leftArrowRect.offsetMin = leftArrowRect.offsetMax = Vector2.zero;
            var leftLabel = CreateTmpLabel(leftArrowGo, "Label_◀", "◀", 52f, font);
            SetAnchors(leftLabel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            leftLabel.GetComponent<RectTransform>().offsetMin = leftLabel.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 우 화살표
            var rightArrowGo = new GameObject("RightArrowButton", typeof(RectTransform));
            rightArrowGo.transform.SetParent(modalGo.transform, false);
            rightArrowGo.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 0.8f);
            var rightArrowBtn  = rightArrowGo.AddComponent<Button>();
            var rightArrowRect = rightArrowGo.GetComponent<RectTransform>();
            SetAnchors(rightArrowRect, new Vector2(0.88f, 0.35f), new Vector2(0.99f, 0.65f));
            rightArrowRect.offsetMin = rightArrowRect.offsetMax = Vector2.zero;
            var rightLabel = CreateTmpLabel(rightArrowGo, "Label_▶", "▶", 52f, font);
            SetAnchors(rightLabel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            rightLabel.GetComponent<RectTransform>().offsetMin = rightLabel.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 하단 설명 패널 (반투명 검정 배경)
            var descPanelGo = new GameObject("DescriptionPanel", typeof(RectTransform));
            descPanelGo.transform.SetParent(modalGo.transform, false);
            descPanelGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            var descPanelRect = descPanelGo.GetComponent<RectTransform>();
            SetAnchors(descPanelRect, new Vector2(0f, 0.0f), new Vector2(1f, 0.35f));
            descPanelRect.offsetMin = descPanelRect.offsetMax = Vector2.zero;

            // 캐릭터 이름 텍스트
            var charNameGo  = CreateTmpLabel(descPanelGo, "CharacterName", "캐릭터 이름", 44f, font);
            charNameGo.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            var charNameRect = charNameGo.GetComponent<RectTransform>();
            SetAnchors(charNameRect, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.95f));
            charNameRect.offsetMin = charNameRect.offsetMax = Vector2.zero;

            // 설명 텍스트
            var descGo  = CreateTmpLabel(descPanelGo, "DescriptionText", "설명 텍스트", 32f, font);
            var descTmp = descGo.GetComponent<TextMeshProUGUI>();
            descTmp.color           = new Color(0.85f, 0.85f, 0.85f);
            descTmp.alignment       = TextAlignmentOptions.TopLeft;
            descTmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            var descRect = descGo.GetComponent<RectTransform>();
            SetAnchors(descRect, new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.70f));
            descRect.offsetMin = descRect.offsetMax = Vector2.zero;

            // 선택하기 버튼
            var selectBtnGo = CreateSimpleButton(descPanelGo, "SelectButton", "선택하기",
                new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.32f),
                new Color(0.2f, 0.65f, 0.3f), font);

            // 잠금 오버레이 (미구현 캐릭터)
            // [Fix] 좌우 화살표(각 12% 폭)를 제외한 중앙 영역만 덮는다.
            // 화살표: left 0~0.12, right 0.88~1.0 → LockOverlay xMin=0.12, xMax=0.88
            var lockOverlayGo = new GameObject("LockOverlay", typeof(RectTransform));
            lockOverlayGo.transform.SetParent(modalGo.transform, false);
            lockOverlayGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var lockOverlayRect = lockOverlayGo.GetComponent<RectTransform>();
            SetAnchors(lockOverlayRect, new Vector2(0.12f, 0f), new Vector2(0.88f, 1f));
            lockOverlayRect.offsetMin = lockOverlayRect.offsetMax = Vector2.zero;
            var lockLabelGo = CreateTmpLabel(lockOverlayGo, "LockText", "Coming Soon", 56f, font);
            lockLabelGo.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.8f, 0.3f);
            var lockLabelRect = lockLabelGo.GetComponent<RectTransform>();
            SetAnchors(lockLabelRect, new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.58f));
            lockLabelRect.offsetMin = lockLabelRect.offsetMax = Vector2.zero;
            lockOverlayGo.SetActive(false);

            // CharacterSelectModalView 조립
            var modalView = modalGo.AddComponent<CharacterSelectModalView>();
            modalView.InitReferences(
                illustImg,
                charNameGo.GetComponent<TextMeshProUGUI>(),
                descTmp,
                selectBtnGo.GetComponent<Button>(),
                leftArrowBtn,
                rightArrowBtn,
                lockOverlayGo,
                lockLabelGo.GetComponent<TextMeshProUGUI>());

            modalGo.SetActive(false); // 기본 닫힘
            return modalView;
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

            // 6. 장착/강화/닫기 버튼 행 (Phase 25-C: 3버튼 가로 균등 분할)
            var equipBtn   = CreateSimpleButton(popupGo, "EquipButton", "장착",
                new Vector2(0.02f, 0.02f), new Vector2(0.34f, 0.14f), new Color(0.2f, 0.7f, 0.3f), font);
            var enhanceBtn = CreateSimpleButton(popupGo, "EnhanceButton", "강화",
                new Vector2(0.36f, 0.02f), new Vector2(0.64f, 0.14f), new Color(0.8f, 0.55f, 0.1f), font);
            var closeBtn   = CreateSimpleButton(popupGo, "CloseButton", "닫기",
                new Vector2(0.66f, 0.02f), new Vector2(0.98f, 0.14f), new Color(0.5f, 0.2f, 0.2f), font);

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
                enhanceBtn.GetComponent<Button>(),
                enhanceBtn.GetComponentInChildren<TextMeshProUGUI>(),
                closeBtn.GetComponent<Button>());

            var presenter = popupGo.AddComponent<ItemDetailPopupPresenter>();
            presenter.InitReferences(view);

            popupGo.SetActive(false);
            return (view, presenter);
        }

        // ── Phase 25-C 헬퍼: 강화 모달 생성 ─────────────────────────

        private static (EnhanceModalView view, EnhanceModalPresenter presenter)
            CreateEnhanceModal(GameObject panelParent, TMP_FontAsset font)
        {
            // 루트 — 전체화면 darken 배경. 독립 Canvas(sortingOrder=300)로 ItemDetailPopup(부모 Canvas 10) 위에 렌더링
            var modalGo = new GameObject("EnhanceModal", typeof(RectTransform));
            modalGo.transform.SetParent(panelParent.transform, false);

            var modalCanvas             = modalGo.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder    = 300;
            modalGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var darken   = modalGo.AddComponent<Image>();
            darken.color = new Color(0f, 0f, 0f, 0.85f);
            var darkenRt = modalGo.GetComponent<RectTransform>();
            SetAnchors(darkenRt, Vector2.zero, Vector2.one);
            darkenRt.offsetMin = darkenRt.offsetMax = Vector2.zero;

            // 모달 내용 패널 (ItemDetailPopup 기반 크기)
            var panelGo = new GameObject("ModalPanel", typeof(RectTransform));
            panelGo.transform.SetParent(modalGo.transform, false);
            panelGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 1f);
            var panelRt = panelGo.GetComponent<RectTransform>();
            SetAnchors(panelRt, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.95f));
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

            // 1. 아이콘
            var iconGo  = new GameObject("ItemIcon", typeof(RectTransform));
            iconGo.transform.SetParent(panelGo.transform, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.white;
            SetAnchors(iconGo.GetComponent<RectTransform>(), new Vector2(0.02f, 0.82f), new Vector2(0.28f, 0.98f));
            iconGo.GetComponent<RectTransform>().offsetMin = iconGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 2. 아이템 이름
            var nameGo = CreateTmpLabel(panelGo, "ItemName", "아이템 이름", 34f, font);
            SetAnchors(nameGo.GetComponent<RectTransform>(), new Vector2(0.30f, 0.90f), new Vector2(0.98f, 0.98f));
            nameGo.GetComponent<RectTransform>().offsetMin = nameGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 3. 강화 단계 (+N → +N+1)
            var levelGo = CreateTmpLabel(panelGo, "EnhanceLevel", "+0 → +1", 40f, font);
            levelGo.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.9f, 0.3f);
            levelGo.GetComponent<TextMeshProUGUI>().fontStyle = TMPro.FontStyles.Bold;
            SetAnchors(levelGo.GetComponent<RectTransform>(), new Vector2(0.30f, 0.80f), new Vector2(0.98f, 0.90f));
            levelGo.GetComponent<RectTransform>().offsetMin = levelGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 4. 메인 스탯 미리보기
            var mainPreviewGo = CreateTmpLabel(panelGo, "MainStatPreview", "메인 스탯 미리보기", 30f, font);
            mainPreviewGo.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.9f, 0.5f);
            mainPreviewGo.GetComponent<TextMeshProUGUI>().alignment = TMPro.TextAlignmentOptions.Left;
            SetAnchors(mainPreviewGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.68f), new Vector2(0.97f, 0.78f));
            mainPreviewGo.GetComponent<RectTransform>().offsetMin = mainPreviewGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 5. 서브스탯 강화 단계 강조
            var subBonusGo = CreateTmpLabel(panelGo, "SubStatBonus", "★ 서브스탯 강화 단계!", 32f, font);
            subBonusGo.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.9f, 0.2f);
            SetAnchors(subBonusGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.57f), new Vector2(0.97f, 0.67f));
            subBonusGo.GetComponent<RectTransform>().offsetMin = subBonusGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            subBonusGo.SetActive(false); // 기본 숨김

            // 6. 성공률
            var successGo = CreateTmpLabel(panelGo, "SuccessPercent", "성공 확률: --%", 30f, font);
            successGo.GetComponent<TextMeshProUGUI>().alignment = TMPro.TextAlignmentOptions.Left;
            SetAnchors(successGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.46f), new Vector2(0.97f, 0.56f));
            successGo.GetComponent<RectTransform>().offsetMin = successGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 7. 가루 비용
            var dustGo = CreateTmpLabel(panelGo, "DustCost", "필요 가루: -- / 보유 --", 30f, font);
            dustGo.GetComponent<TextMeshProUGUI>().alignment = TMPro.TextAlignmentOptions.Left;
            SetAnchors(dustGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.35f), new Vector2(0.97f, 0.45f));
            dustGo.GetComponent<RectTransform>().offsetMin = dustGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 8. 강화하기 / 닫기 버튼 (2등분)
            var enhBtn  = CreateSimpleButton(panelGo, "EnhanceButton", "강화하기",
                new Vector2(0.02f, 0.02f), new Vector2(0.49f, 0.13f), new Color(0.2f, 0.7f, 0.3f), font);
            var closeBtn = CreateSimpleButton(panelGo, "CloseButton", "닫기",
                new Vector2(0.51f, 0.02f), new Vector2(0.98f, 0.13f), new Color(0.5f, 0.2f, 0.2f), font);

            // 9. 플래시 Image (전체화면, 기본 비활성)
            var flashGo = new GameObject("FlashImage", typeof(RectTransform));
            flashGo.transform.SetParent(panelGo.transform, false);
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;
            SetAnchors(flashGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            flashGo.GetComponent<RectTransform>().offsetMin = flashGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            flashGo.SetActive(false);

            // 10. 파편 5개 (중앙에서 분산용)
            var shards = new List<Image>();
            for (int i = 0; i < 5; i++)
            {
                var shardGo = new GameObject($"Shard_{i}", typeof(RectTransform));
                shardGo.transform.SetParent(panelGo.transform, false);
                var shardImg = shardGo.AddComponent<Image>();
                shardImg.color = new Color(1f, 0.9f, 0.3f, 1f);
                shardImg.raycastTarget = false;
                var shardRt  = shardGo.GetComponent<RectTransform>();
                SetAnchors(shardRt, new Vector2(0.45f, 0.45f), new Vector2(0.55f, 0.55f));
                shardRt.offsetMin = shardRt.offsetMax = Vector2.zero;
                shardRt.sizeDelta = new Vector2(20f, 20f);
                shardGo.SetActive(false);
                shards.Add(shardImg);
            }

            // 11. 토스트 텍스트 (중앙)
            var toastGo = CreateTmpLabel(panelGo, "ToastText", string.Empty, 40f, font);
            toastGo.GetComponent<TextMeshProUGUI>().fontStyle = TMPro.FontStyles.Bold;
            SetAnchors(toastGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.34f));
            toastGo.GetComponent<RectTransform>().offsetMin = toastGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            toastGo.SetActive(false);

            // EnhanceModalView 조립
            var view = modalGo.AddComponent<EnhanceModalView>();
            view.InitReferences(
                iconImg,
                nameGo.GetComponent<TextMeshProUGUI>(),
                levelGo.GetComponent<TextMeshProUGUI>(),
                mainPreviewGo.GetComponent<TextMeshProUGUI>(),
                subBonusGo.GetComponent<TextMeshProUGUI>(),
                successGo.GetComponent<TextMeshProUGUI>(),
                dustGo.GetComponent<TextMeshProUGUI>(),
                enhBtn.GetComponent<Button>(),
                enhBtn.GetComponentInChildren<TextMeshProUGUI>(),
                closeBtn.GetComponent<Button>(),
                flashImg,
                shards,
                toastGo.GetComponent<TextMeshProUGUI>());

            // EnhanceModalPresenter 조립
            var presenter = modalGo.AddComponent<EnhanceModalPresenter>();
            presenter.InitReferences(view);

            // EnhanceTableData 주입
            const string ENHANCE_TABLE_PATH = "Assets/_Project/Resources/EnhanceTableData.asset";
            var enhanceTable = AssetDatabase.LoadAssetAtPath<Game.Data.Equipment.EnhanceTableData>(ENHANCE_TABLE_PATH);
            if (enhanceTable == null)
                Debug.LogWarning($"[LobbyHudSetup] EnhanceTableData 로드 실패: {ENHANCE_TABLE_PATH}");
            presenter.InitEnhanceTable(enhanceTable);

            modalGo.SetActive(false);
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

        // ── Phase 25-B 헬퍼: EquipmentActionBar + BulkSelectDropdown + DismantleResultModal ──

        private static (EquipmentActionBarView actionBarView,
                         EquipmentActionBarPresenter actionBarPresenter,
                         DismantleResultModalPresenter resultModalPresenter)
            CreateActionBarAndModal(GameObject equipRootGo, GameObject panelParent, TMP_FontAsset font)
        {
            // ── ActionBar GO ────────────────────────────────────────────────
            var barGo  = new GameObject("EquipmentActionBar", typeof(RectTransform));
            barGo.transform.SetParent(equipRootGo.transform, false);
            var barBg  = barGo.AddComponent<Image>();
            barBg.color = new Color(0.10f, 0.10f, 0.13f, 0.95f);

            // 가루 텍스트 (좌측)
            var dustGo  = new GameObject("DustText");
            dustGo.transform.SetParent(barGo.transform, false);
            var dustTmp = dustGo.AddComponent<TextMeshProUGUI>();
            dustTmp.text      = "강화의 가루 ×0";
            dustTmp.fontSize  = 24f;
            dustTmp.alignment = TextAlignmentOptions.MidlineLeft;
            dustTmp.color     = new Color(0.9f, 0.85f, 0.5f);
            if (font != null) dustTmp.font = font;
            else Debug.LogWarning("[LobbyHudSetup] ActionBar DustText — font null");
            var dustRect = dustGo.GetComponent<RectTransform>();
            SetAnchors(dustRect, new Vector2(0.02f, 0f), new Vector2(0.50f, 1f));
            dustRect.offsetMin = dustRect.offsetMax = Vector2.zero;

            // ── 기본 모드 루트 ──────────────────────────────────────────────
            var defaultRoot = new GameObject("DefaultModeRoot", typeof(RectTransform));
            defaultRoot.transform.SetParent(barGo.transform, false);
            var defaultRect = defaultRoot.GetComponent<RectTransform>();
            SetAnchors(defaultRect, new Vector2(0.55f, 0f), Vector2.one);
            defaultRect.offsetMin = defaultRect.offsetMax = Vector2.zero;
            var defaultHLayout = defaultRoot.AddComponent<HorizontalLayoutGroup>();
            defaultHLayout.childAlignment        = TextAnchor.MiddleRight;
            defaultHLayout.childControlWidth     = true;
            defaultHLayout.childControlHeight    = true;
            defaultHLayout.childForceExpandWidth = false;
            defaultHLayout.childForceExpandHeight= false;
            defaultHLayout.spacing               = 8f;
            defaultHLayout.padding               = new RectOffset(4, 8, 4, 4);

            // [분해] 버튼
            var dismantleEnterBtn = CreateSimpleButton(defaultRoot, "DismantleEnterButton", "분해",
                Vector2.zero, Vector2.zero,  // 앵커는 LayoutGroup 이 관리
                new Color(0.7f, 0.3f, 0.15f), font);
            var dismantleEnterLE = dismantleEnterBtn.GetComponent<LayoutElement>()
                                    ?? dismantleEnterBtn.AddComponent<LayoutElement>();
            dismantleEnterLE.preferredWidth  = 140f;
            dismantleEnterLE.preferredHeight = 60f;
            dismantleEnterLE.flexibleWidth   = 0f;

            // ── 분해 모드 루트 ──────────────────────────────────────────────
            var dismantleRoot = new GameObject("DismantleModeRoot", typeof(RectTransform));
            dismantleRoot.transform.SetParent(barGo.transform, false);
            var dismantleRect = dismantleRoot.GetComponent<RectTransform>();
            SetAnchors(dismantleRect, Vector2.zero, Vector2.one);
            dismantleRect.offsetMin = dismantleRect.offsetMax = Vector2.zero;
            dismantleRoot.SetActive(false);

            // 좌측: [돌아가기]
            var backBtn = CreateSimpleButton(dismantleRoot, "BackButton", "돌아가기",
                new Vector2(0.01f, 0.1f), new Vector2(0.28f, 0.9f),
                new Color(0.3f, 0.3f, 0.4f), font);

            // 우측: [일괄 선택 ▼]
            var bulkBtn = CreateSimpleButton(dismantleRoot, "BulkSelectButton", "일괄 선택 ▼",
                new Vector2(0.50f, 0.1f), new Vector2(0.74f, 0.9f),
                new Color(0.25f, 0.4f, 0.6f), font);

            // 우측: [분해하기]
            var dosDismantleBtn = CreateSimpleButton(dismantleRoot, "DosDismantleButton", "분해하기",
                new Vector2(0.76f, 0.1f), new Vector2(0.99f, 0.9f),
                new Color(0.7f, 0.3f, 0.15f), font);
            var dosDismantleLabel = dosDismantleBtn.GetComponentInChildren<TextMeshProUGUI>();

            // EquipmentActionBarView 조립
            var actionBarView = barGo.AddComponent<EquipmentActionBarView>();
            actionBarView.InitReferences(
                dustTmp,
                defaultRoot,
                dismantleEnterBtn.GetComponent<Button>(),
                dismantleRoot,
                backBtn.GetComponent<Button>(),
                bulkBtn.GetComponent<Button>(),
                dosDismantleBtn.GetComponent<Button>(),
                dosDismantleLabel);

            // ── BulkSelectDropdown ────────────────────────────────────────────
            var dropdownGo = new GameObject("BulkSelectDropdown", typeof(RectTransform));
            dropdownGo.transform.SetParent(equipRootGo.transform, false);
            dropdownGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.97f);
            var dropdownRect = dropdownGo.GetComponent<RectTransform>();
            // 액션바(0.14~0.22) 위에 펼침 — 0.22~0.52 영역 (등급 4종 × 80px)
            SetAnchors(dropdownRect, new Vector2(0.30f, 0.22f), new Vector2(1.00f, 0.52f));
            dropdownRect.offsetMin = dropdownRect.offsetMax = Vector2.zero;
            dropdownGo.SetActive(false);

            // 등급 4종 항목
            var gradeButtons   = new List<Button>();
            var gradeDotImages = new List<Image>();
            var gradeTexts     = new List<TextMeshProUGUI>();
            string[] gradeLabels = { "Normal", "Rare", "Epic", "Unique" };
            for (int i = 0; i < 4; i++)
            {
                float yMax = 1.0f - i * 0.25f;
                float yMin = yMax - 0.24f;
                var rowGo = new GameObject($"GradeRow_{gradeLabels[i]}", typeof(RectTransform));
                rowGo.transform.SetParent(dropdownGo.transform, false);
                var rowRect = rowGo.GetComponent<RectTransform>();
                SetAnchors(rowRect, new Vector2(0f, yMin), new Vector2(1f, yMax));
                rowRect.offsetMin = rowRect.offsetMax = Vector2.zero;
                rowGo.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.17f, 0.9f);
                var rowBtn = rowGo.AddComponent<Button>();

                // 등급 색 점 (●)
                var dotGo  = new GameObject("GradeDot", typeof(RectTransform));
                dotGo.transform.SetParent(rowGo.transform, false);
                var dotImg  = dotGo.AddComponent<Image>();
                dotImg.color = Color.gray;
                var dotRect = dotGo.GetComponent<RectTransform>();
                SetAnchors(dotRect, new Vector2(0.03f, 0.2f), new Vector2(0.15f, 0.8f));
                dotRect.offsetMin = dotRect.offsetMax = Vector2.zero;

                // 텍스트
                var labelGo = CreateTmpLabel(rowGo, $"Label_{gradeLabels[i]}", gradeLabels[i], 30f, font);
                var labelRect = labelGo.GetComponent<RectTransform>();
                SetAnchors(labelRect, new Vector2(0.18f, 0f), Vector2.one);
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                labelGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;

                gradeButtons.Add(rowBtn);
                gradeDotImages.Add(dotImg);
                gradeTexts.Add(labelGo.GetComponent<TextMeshProUGUI>());
            }

            var dropdownView = dropdownGo.AddComponent<BulkSelectDropdownView>();
            dropdownView.InitReferences(gradeButtons, gradeDotImages, gradeTexts);

            // ── DismantleResultModal ────────────────────────────────────────
            // panelParent 직속 자식, LobbyCanvas と同レベル (실제로는 Panel 하위이지만
            // Canvas sortingOrder 는 루트 Canvas 단위이므로 여기서는 Panel 계층 내 최상위 배치)
            // 전체화면 반투명 딤 레이어
            var modalGo = new GameObject("DismantleResultModal", typeof(RectTransform));
            modalGo.transform.SetParent(panelParent.transform, false);
            var modalBg   = modalGo.AddComponent<Image>();
            modalBg.color = new Color(0f, 0f, 0f, 0.55f);
            var modalRect = modalGo.GetComponent<RectTransform>();
            SetAnchors(modalRect, Vector2.zero, Vector2.one);
            modalRect.offsetMin = modalRect.offsetMax = Vector2.zero;
            modalGo.SetActive(false);

            // 내용 패널 (화면 중앙 부분만)
            var innerGo = new GameObject("InnerPanel", typeof(RectTransform));
            innerGo.transform.SetParent(modalGo.transform, false);
            innerGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 1f);
            var innerRt = innerGo.GetComponent<RectTransform>();
            SetAnchors(innerRt, new Vector2(0.10f, 0.35f), new Vector2(0.90f, 0.65f));
            innerRt.offsetMin = innerRt.offsetMax = Vector2.zero;

            // 타이틀
            var titleGo = CreateTmpLabel(innerGo, "Title", "분해 완료", 44f, font);
            titleGo.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            SetAnchors(titleGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.90f));
            titleGo.GetComponent<RectTransform>().offsetMin = titleGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 본문
            var bodyGo  = CreateTmpLabel(innerGo, "BodyText", "획득: 강화의 가루 ×0", 34f, font);
            bodyGo.GetComponent<TextMeshProUGUI>().color = new Color(0.9f, 0.85f, 0.5f);
            SetAnchors(bodyGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.58f));
            bodyGo.GetComponent<RectTransform>().offsetMin = bodyGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // 닫기 버튼
            var closeBtn = CreateSimpleButton(innerGo, "CloseButton", "닫기",
                new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.30f),
                new Color(0.3f, 0.3f, 0.5f), font);

            var modalView = modalGo.AddComponent<DismantleResultModalView>();
            modalView.InitReferences(bodyGo.GetComponent<TextMeshProUGUI>(), closeBtn.GetComponent<Button>());

            var modalPresenter = modalGo.AddComponent<DismantleResultModalPresenter>();
            modalPresenter.InitReferences(modalView);

            // EquipmentActionBarPresenter 조립
            var actionBarPresenter = barGo.AddComponent<EquipmentActionBarPresenter>();
            actionBarPresenter.InitReferences(actionBarView, dropdownView, modalPresenter);

            return (actionBarView, actionBarPresenter, modalPresenter);
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
