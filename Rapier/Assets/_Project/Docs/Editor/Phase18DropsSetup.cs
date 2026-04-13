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
    ///   RunDropListView 패널  — StageClearView 부모 Canvas에 추가
    ///
    /// [SerializedField 자동 연결]
    ///   ProgressionManager._bossDeathSequencer → 위 Sequencer
    ///   BossDeathSequencer._droppedItemPrefab  → 위 프리팹
    ///   IntermissionManager._runDropListView   → 위 패널
    ///   RunDropListView._panel / _listParent / _emptyText 내부 참조
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

            // ⑥ IntermissionManager + RunDropListView
            var im = Object.FindObjectOfType<IntermissionManager>();
            if (im == null)
            {
                Debug.LogWarning("[Phase18DropsSetup] IntermissionManager 없음 → RunDropListView 연결 생략.");
            }
            else
            {
                // StageClearView 부모 Canvas에 패널 추가
                var scv    = Object.FindObjectOfType<StageClearView>();
                var canvas = scv != null ? FindRootCanvas(scv.transform) : FindRootCanvas(im.transform);

                // 기존 RunDropListView 처리
                var existingView = Object.FindObjectOfType<RunDropListView>();
                if (existingView != null)
                {
                    if (forceRebuild)
                    {
                        Undo.DestroyObjectImmediate(existingView.gameObject);
                        existingView = null;
                    }
                    else
                    {
                        Debug.Log("[Phase18DropsSetup] RunDropListView 이미 존재. Rebuild를 사용하세요.");
                    }
                }

                var view = existingView ?? CreateRunDropListView(canvas);

                // IntermissionManager 필드 연결
                var imSo = new SerializedObject(im);
                imSo.FindProperty("_runDropListView").objectReferenceValue = view;
                imSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(im);

                Debug.Log("[Phase18DropsSetup] RunDropListView 연결 완료.");
            }

            // ⑦ 씬 저장
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Phase18DropsSetup] ✓ 드롭 시스템 배선 완료. 씬 저장됨.");
        }

        // ── RunDropListView 패널 생성 ─────────────────────────────
        private static RunDropListView CreateRunDropListView(Transform canvas)
        {
            var sq   = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Square.png");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);

            // ── 루트 패널 ─────────────────────────────────────────
            var panelGo = new GameObject("RunDropListPanel");
            Undo.RegisterCreatedObjectUndo(panelGo, "Create RunDropListPanel");
            panelGo.transform.SetParent(canvas, false);

            var panelImg   = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.90f);
            if (sq != null) panelImg.sprite = sq;

            var panelRt = panelGo.GetComponent<RectTransform>();
            // 화면 하단 60% 영역을 차지하는 드롭 목록 패널
            panelRt.anchorMin = new Vector2(0.05f, 0.05f);
            panelRt.anchorMax = new Vector2(0.95f, 0.60f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelGo.SetActive(false);   // Show()/Hide()로 수명 관리

            // ── 타이틀 텍스트 ─────────────────────────────────────
            var titleGo  = new GameObject("TitleText");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            if (font != null) titleTmp.font = font;
            titleTmp.text      = "획득한 장비";
            titleTmp.fontSize  = 36f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color     = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin        = new Vector2(0f, 1f);
            titleRt.anchorMax        = new Vector2(1f, 1f);
            titleRt.pivot            = new Vector2(0.5f, 1f);
            titleRt.sizeDelta        = new Vector2(0f, 56f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);

            // ── ScrollView ────────────────────────────────────────
            var scrollGo   = new GameObject("ScrollView");
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            var scrollImg  = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0.08f, 0.08f, 0.08f, 0.60f);
            if (sq != null) scrollImg.sprite = sq;
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(16f, 16f);
            scrollRt.offsetMax = new Vector2(-16f, -76f);

            // ── Viewport ──────────────────────────────────────────
            var vpGo  = new GameObject("Viewport");
            vpGo.transform.SetParent(scrollGo.transform, false);
            var vpImg = vpGo.AddComponent<Image>();
            vpImg.color = Color.clear;
            var mask  = vpGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var vpRt  = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            scrollRect.viewport = vpRt;

            // ── Content (슬롯 컨테이너) ────────────────────────────
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(vpGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = Vector2.zero;

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 6f;
            vlg.padding              = new RectOffset(12, 12, 12, 12);
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;

            // ── "획득한 장비 없음" 안내 텍스트 ─────────────────────
            var emptyGo  = new GameObject("EmptyText");
            emptyGo.transform.SetParent(panelGo.transform, false);
            var emptyTmp = emptyGo.AddComponent<TextMeshProUGUI>();
            if (font != null) emptyTmp.font = font;
            emptyTmp.text      = "획득한 장비 없음";
            emptyTmp.fontSize  = 28f;
            emptyTmp.color     = new Color(0.55f, 0.55f, 0.55f, 1f);
            emptyTmp.alignment = TextAlignmentOptions.Center;
            var emptyRt = emptyGo.GetComponent<RectTransform>();
            emptyRt.anchorMin = Vector2.zero;
            emptyRt.anchorMax = Vector2.one;
            emptyRt.offsetMin = emptyRt.offsetMax = Vector2.zero;
            emptyGo.SetActive(false);

            // ── RunDropListView 컴포넌트 부착 + 내부 참조 연결 ──────
            var view   = panelGo.AddComponent<RunDropListView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_panel").objectReferenceValue      = panelGo;
            viewSo.FindProperty("_listParent").objectReferenceValue = contentGo.transform;
            viewSo.FindProperty("_emptyText").objectReferenceValue  = emptyGo;
            viewSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);

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
