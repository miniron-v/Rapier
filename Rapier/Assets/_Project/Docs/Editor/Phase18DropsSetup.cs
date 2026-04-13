#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Game.Core;
using Game.Core.Stage;
using Game.Data.Equipment;
using Game.UI.Intermission;
using Game.UI.Stage;

namespace Game.Editor
{
    /// <summary>
    /// Phase 18 보스 드롭 시스템 씬 자동 배선 도우미.
    ///
    /// [생성/배선 목록]
    ///   BossDeathSequencer   — ProgressionManager와 동일 GO에 AddComponent
    ///   DroppedItemView 프리팹 — Assets/_Project/Prefabs/Stage/DroppedItemView.prefab
    ///   RunDropListView 패널  — StageClearView 내부 패널 자식으로 추가
    ///
    /// [SerializedField 자동 연결]
    ///   ProgressionManager._bossDeathSequencer → 위 Sequencer
    ///   BossDeathSequencer._droppedItemPrefab  → 위 프리팹
    ///   StageClearView._runDropListView        → 위 패널
    ///   RunDropListView._panel / _listParent / _emptyText / _font 내부 참조
    ///
    /// [실행]
    ///   Rapier/Phase 18 Drops/Setup Drop System
    ///   Rapier/Phase 18 Drops/Rebuild Drop System
    /// </summary>
    public static class Phase18DropsSetup
    {
        // ── 경로 상수 ─────────────────────────────────────────────
        private const string FONT_PATH =
            "Assets/_Project/ScriptableObjects/Fonts/NEXONLv1Gothic Regular SDF.asset";
        private const string SPRITE_BASE =
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/";
        private const string PREFAB_DIR  = "Assets/_Project/Prefabs/Stage";
        private const string PREFAB_PATH = "Assets/_Project/Prefabs/Stage/DroppedItemView.prefab";

        // ── 메뉴 항목 ─────────────────────────────────────────────
        [MenuItem("Rapier/Phase 18 Drops/Setup Drop System")]
        public static void Setup()   => Build(forceRebuild: false);

        [MenuItem("Rapier/Phase 18 Drops/Rebuild Drop System")]
        public static void Rebuild() => Build(forceRebuild: true);

        // ── 진입점 ────────────────────────────────────────────────
        private static void Build(bool forceRebuild)
        {
            // ① ProgressionManager 확인
            var pm = Object.FindObjectOfType<ProgressionManager>();
            if (pm == null)
            {
                Debug.LogError("[Phase18DropsSetup] ProgressionManager를 찾을 수 없습니다. " +
                               "스테이지 씬(StageDemo / BossRushDemo)에서 실행하세요.");
                return;
            }

            // ② BossDeathSequencer — ProgressionManager GO에 AddComponent
            var seq = pm.GetComponent<BossDeathSequencer>();
            if (seq == null)
            {
                seq = Undo.AddComponent<BossDeathSequencer>(pm.gameObject);
                Debug.Log("[Phase18DropsSetup] BossDeathSequencer 추가 완료.");
            }
            else if (forceRebuild)
            {
                Debug.Log("[Phase18DropsSetup] BossDeathSequencer 기존 컴포넌트 유지 (값 재연결).");
            }

            // ③ DroppedItemView 프리팹 생성 (없거나 Rebuild 시)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null || forceRebuild)
            {
                if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
                    AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Stage");

                var tmpGo = new GameObject("DroppedItemView");
                tmpGo.AddComponent<DroppedItemView>();
                PrefabUtility.SaveAsPrefabAsset(tmpGo, PREFAB_PATH);
                Object.DestroyImmediate(tmpGo);
                AssetDatabase.Refresh();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
                Debug.Log($"[Phase18DropsSetup] DroppedItemView 프리팹 생성: {PREFAB_PATH}");
            }

            // ④ BossDeathSequencer 필드 연결
            var seqSo = new SerializedObject(seq);
            seqSo.FindProperty("_droppedItemPrefab").objectReferenceValue =
                prefab.GetComponent<DroppedItemView>();
            seqSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(seq);

            // ⑤ ProgressionManager 필드 연결
            var pmSo = new SerializedObject(pm);
            pmSo.FindProperty("_bossDeathSequencer").objectReferenceValue = seq;
            pmSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(pm);

            // ⑥ StageClearView + RunDropListView 연결
            var scv = Object.FindObjectOfType<StageClearView>();
            if (scv == null)
            {
                Debug.LogWarning("[Phase18DropsSetup] StageClearView 없음 → RunDropListView 연결 생략.");
            }
            else
            {
                // 기존 RunDropListView 처리
                var existingView = Object.FindObjectOfType<RunDropListView>();
                if (existingView != null)
                {
                    if (forceRebuild)
                    {
                        // 버튼을 ButtonRow에서 panel 직속으로 먼저 이동 (ButtonRow 파괴 전)
                        var cleanSo    = new SerializedObject(scv);
                        var cleanPanel = cleanSo.FindProperty("_panel")?.objectReferenceValue as GameObject;
                        if (cleanPanel != null)
                        {
                            var nb = cleanSo.FindProperty("_nextStageButton")?.objectReferenceValue as UnityEngine.UI.Button;
                            var lb = cleanSo.FindProperty("_returnToLobbyButton")?.objectReferenceValue as UnityEngine.UI.Button;
                            if (nb != null) nb.transform.SetParent(cleanPanel.transform, false);
                            if (lb != null) lb.transform.SetParent(cleanPanel.transform, false);
                            foreach (var n in new[] { "ButtonRow", "TopSpacer" })
                            {
                                var child = cleanPanel.transform.Find(n);
                                if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
                            }
                        }

                        Undo.DestroyObjectImmediate(existingView.gameObject);
                        existingView = null;
                    }
                    else
                    {
                        Debug.Log("[Phase18DropsSetup] RunDropListView 이미 존재. Rebuild를 사용하세요.");
                    }
                }

                var view = existingView ?? CreateRunDropListViewInStageClear(scv);

                if (view != null)
                {
                    var scvSo = new SerializedObject(scv);
                    scvSo.FindProperty("_runDropListView").objectReferenceValue = view;
                    scvSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(scv);
                    Debug.Log("[Phase18DropsSetup] StageClearView._runDropListView 연결 완료.");
                }
            }

            // ⑦ 씬 저장
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Phase18DropsSetup] ✓ 드롭 시스템 배선 완료. 씬 저장됨.");
        }

        // ── StageClearView 내부에 RunDropListView 생성 ────────────
        private static RunDropListView CreateRunDropListViewInStageClear(StageClearView scv)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            var sq   = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Square.png");

            // StageClearView._panel 접근
            var scvSo     = new SerializedObject(scv);
            var panelProp = scvSo.FindProperty("_panel");
            var panel     = panelProp?.objectReferenceValue as GameObject;
            if (panel == null)
            {
                Debug.LogWarning("[Phase18DropsSetup] StageClearView._panel을 찾을 수 없음.");
                return null;
            }

            // panel에 Canvas override 추가 (BossHUD보다 위에 렌더링)
            var panelCanvas = panel.GetComponent<Canvas>();
            if (panelCanvas == null)
                panelCanvas = Undo.AddComponent<Canvas>(panel);
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder    = 100;
            EditorUtility.SetDirty(panel);

            // 버튼 클릭을 위한 GraphicRaycaster
            if (panel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                Undo.AddComponent<UnityEngine.UI.GraphicRaycaster>(panel);

            // panel에 VerticalLayoutGroup 추가 (없는 경우)
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
                vlg = Undo.AddComponent<VerticalLayoutGroup>(panel);
            vlg.spacing               = 120f;
            vlg.padding               = new RectOffset(20, 20, 540, 320);
            vlg.childAlignment        = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth     = true;
            vlg.childControlHeight    = true;

            // ── 기존 TitleText 레이아웃 설정 ──────────────────────
            var titleProp = scvSo.FindProperty("_titleText");
            var titleText = titleProp?.objectReferenceValue as TMPro.TextMeshProUGUI;
            if (titleText != null)
            {
                var le = titleText.gameObject.GetComponent<LayoutElement>()
                         ?? Undo.AddComponent<LayoutElement>(titleText.gameObject);
                le.minHeight       = 80f;
                le.preferredHeight = 80f;
                le.flexibleHeight  = 0f;
            }

            // ── ScrollView (드롭 목록) ─────────────────────────────
            var scrollGo = new GameObject("DropListScrollView");
            Undo.RegisterCreatedObjectUndo(scrollGo, "Create DropListScrollView");
            scrollGo.transform.SetParent(panel.transform, false);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0.08f, 0.08f, 0.08f, 0.60f);
            if (sq != null) scrollImg.sprite = sq;

            var scrollLe = scrollGo.AddComponent<LayoutElement>();
            scrollLe.minHeight       = 200f;
            scrollLe.preferredHeight = 400f;
            scrollLe.flexibleHeight  = 1f;  // 남은 공간도 차지 (해상도 대응)

            // Viewport
            var vpGo  = new GameObject("Viewport");
            vpGo.transform.SetParent(scrollGo.transform, false);
            var vpImg = vpGo.AddComponent<Image>();
            vpImg.color = Color.white;   // alpha=1 필수 — alpha=0이면 Mask 스텐실 미기록 → 자식 전부 클리핑됨
            var mask  = vpGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var vpRt  = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            scrollRect.viewport = vpRt;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(vpGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = Vector2.zero;

            var contentVlg = contentGo.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing               = 12f;
            contentVlg.padding               = new RectOffset(16, 16, 16, 16);
            contentVlg.childForceExpandWidth  = true;
            contentVlg.childForceExpandHeight = false;
            contentVlg.childControlWidth     = true;
            contentVlg.childControlHeight    = false;

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;

            // EmptyText
            var emptyGo  = new GameObject("EmptyText", typeof(RectTransform));
            emptyGo.transform.SetParent(scrollGo.transform, false);
            var emptyTmp = emptyGo.AddComponent<TMPro.TextMeshProUGUI>();
            if (font != null) emptyTmp.font = font;
            emptyTmp.text      = "획득한 장비 없음";
            emptyTmp.fontSize  = 28f;
            emptyTmp.color     = new Color(0.55f, 0.55f, 0.55f, 1f);
            emptyTmp.alignment = TMPro.TextAlignmentOptions.Center;
            var emptyRt = emptyGo.GetComponent<RectTransform>();
            emptyRt.anchorMin = Vector2.zero;
            emptyRt.anchorMax = Vector2.one;
            emptyRt.offsetMin = emptyRt.offsetMax = Vector2.zero;
            emptyGo.SetActive(false);

            // RunDropListView 컴포넌트 scrollGo에 부착
            var view   = scrollGo.AddComponent<RunDropListView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_panel").objectReferenceValue      = scrollGo;
            viewSo.FindProperty("_listParent").objectReferenceValue = contentGo.transform;
            viewSo.FindProperty("_emptyText").objectReferenceValue  = emptyGo;
            viewSo.FindProperty("_font").objectReferenceValue       = font;   // Bug 4 폰트 연결
            viewSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);

            scrollGo.SetActive(false);  // Show()/Hide()로 수명 관리

            // ── ButtonRow 생성 (하단, 가로 배치) ──────────────────
            var nextBtnProp  = scvSo.FindProperty("_nextStageButton");
            var lobbyBtnProp = scvSo.FindProperty("_returnToLobbyButton");
            var nextBtn      = nextBtnProp?.objectReferenceValue as UnityEngine.UI.Button;
            var lobbyBtn     = lobbyBtnProp?.objectReferenceValue as UnityEngine.UI.Button;

            var btnRowGo = new GameObject("ButtonRow", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(btnRowGo, "Create ButtonRow");
            btnRowGo.transform.SetParent(panel.transform, false);

            var btnRowHlg = btnRowGo.AddComponent<HorizontalLayoutGroup>();
            btnRowHlg.spacing               = 24f;
            btnRowHlg.padding               = new RectOffset(20, 20, 12, 12);
            btnRowHlg.childAlignment        = TextAnchor.MiddleCenter;
            btnRowHlg.childForceExpandWidth  = true;
            btnRowHlg.childForceExpandHeight = false;
            btnRowHlg.childControlWidth     = true;
            btnRowHlg.childControlHeight    = false;

            var btnRowLe = btnRowGo.AddComponent<LayoutElement>();
            btnRowLe.minHeight       = 80f;
            btnRowLe.preferredHeight = 80f;
            btnRowLe.flexibleHeight  = 0f;

            // 버튼 순서: 왼쪽=다음 스테이지, 오른쪽=로비
            if (nextBtn  != null) nextBtn.transform.SetParent(btnRowGo.transform, false);
            if (lobbyBtn != null) lobbyBtn.transform.SetParent(btnRowGo.transform, false);

            // hierarchy 순서: TitleText(0) → ScrollView(1) → ButtonRow(2)
            if (titleText != null) titleText.transform.SetAsFirstSibling();
            scrollGo.transform.SetSiblingIndex(1);
            btnRowGo.transform.SetAsLastSibling();

            return view;
        }

        // ── 유틸 ──────────────────────────────────────────────────
        /// <summary>Transform에서 가장 가까운 상위 Canvas를 찾는다.</summary>
        private static Transform FindRootCanvas(Transform t)
        {
            var cur = t;
            while (cur != null)
            {
                if (cur.GetComponent<Canvas>() != null) return cur;
                cur = cur.parent;
            }
            // Canvas를 못 찾으면 씬 루트에서 첫 번째 Canvas 탐색
            var fallback = Object.FindObjectOfType<Canvas>();
            return fallback != null ? fallback.transform : t;
        }
    }
}
#endif
