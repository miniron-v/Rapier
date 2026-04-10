#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace Game.DevTools
{
    /// <summary>
    /// NEXON Lv1 Gothic 한글 폰트를 TMP 폰트 에셋으로 생성하고 프로젝트 전체에 적용합니다.
    /// Window → TextMeshPro → Font Asset Creator 대신 스크립트로 자동화.
    /// </summary>
    public static class TmpKoreanFontSetup
    {
        private const string TTF_REGULAR = "Assets/Rapier-Private/Fonts/NEXON Lv1 Gothic_OTF_TTF/TTF/NEXONLv1GothicRegular.ttf";
        private const string TTF_BOLD    = "Assets/Rapier-Private/Fonts/NEXON Lv1 Gothic_OTF_TTF/TTF/NEXONLv1GothicBold.ttf";
        private const string TTF_LIGHT   = "Assets/Rapier-Private/Fonts/NEXON Lv1 Gothic_OTF_TTF/TTF/NEXONLv1GothicLight.ttf";

        private const string SAVE_DIR    = "Assets/_Project/30_ScriptableObjects/Fonts";
        private const string TMP_SETTINGS_PATH = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string LIBERATION_SANS_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("Game/Setup Korean TMP Fonts")]
        public static void Run()
        {
            // 1. 폰트 에셋 생성
            TMP_FontAsset regular = CreateAndSave(TTF_REGULAR, "NEXONLv1Gothic Regular SDF");
            TMP_FontAsset bold    = CreateAndSave(TTF_BOLD,    "NEXONLv1Gothic Bold SDF");
            TMP_FontAsset light   = CreateAndSave(TTF_LIGHT,   "NEXONLv1Gothic Light SDF");

            if (regular == null) { Debug.LogError("[TmpSetup] 폰트 에셋 생성 실패. 경로를 확인하세요."); return; }

            // 2. TMP Settings 기본 폰트 교체
            SetTmpDefault(regular);

            // 3. LiberationSans SDF 에 Korean 폰트를 Fallback 으로 추가 (기존 컴포넌트 호환)
            AddFallback(regular, bold, light);

            // 4. _Project 하위 모든 Prefab 의 TMP_Text 컴포넌트에 직접 적용
            ApplyToPrefabs(regular, bold, light);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TmpSetup] 완료. Prefab 에 적용된 수: " + _appliedCount);
        }

        private static int _appliedCount;

        // -----------------------------------------------------------------

        private static TMP_FontAsset CreateAndSave(string ttfPath, string assetName)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[TmpSetup] 폰트 파일 없음: {ttfPath}");
                return null;
            }

            string savePath = $"{SAVE_DIR}/{assetName}.asset";

            // 이미 존재하면 재사용
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(savePath);
            if (existing != null)
            {
                Debug.Log($"[TmpSetup] 기존 에셋 재사용: {assetName}");
                return existing;
            }

            // Dynamic 모드로 생성 — 한글 글리프는 런타임에 자동 추가됨
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic
            );

            asset.name = assetName;

            if (!Directory.Exists(SAVE_DIR))
                Directory.CreateDirectory(SAVE_DIR);

            AssetDatabase.CreateAsset(asset, savePath);
            Debug.Log($"[TmpSetup] 폰트 에셋 생성: {savePath}");
            return asset;
        }

        private static void SetTmpDefault(TMP_FontAsset regular)
        {
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TMP_SETTINGS_PATH);
            if (settings == null) { Debug.LogWarning("[TmpSetup] TMP Settings 없음."); return; }

            SerializedObject so = new SerializedObject(settings);
            SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
            prop.objectReferenceValue = regular;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            Debug.Log("[TmpSetup] TMP Settings 기본 폰트 → NEXONLv1Gothic Regular");
        }

        private static void AddFallback(TMP_FontAsset regular, TMP_FontAsset bold, TMP_FontAsset light)
        {
            TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LIBERATION_SANS_PATH);
            if (liberation == null) return;

            liberation.fallbackFontAssetTable ??= new List<TMP_FontAsset>();

            bool dirty = false;
            foreach (TMP_FontAsset fa in new[] { regular, bold, light })
            {
                if (fa != null && !liberation.fallbackFontAssetTable.Contains(fa))
                {
                    liberation.fallbackFontAssetTable.Add(fa);
                    dirty = true;
                }
            }

            if (dirty) EditorUtility.SetDirty(liberation);
        }

        private static void ApplyToPrefabs(TMP_FontAsset regular, TMP_FontAsset bold, TMP_FontAsset light)
        {
            _appliedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // prefab 당 한 번만 EditPrefabContentsScope 열기
                using var scope = new PrefabUtility.EditPrefabContentsScope(path);
                TMP_Text[] texts = scope.prefabContentsRoot.GetComponentsInChildren<TMP_Text>(true);

                foreach (TMP_Text tmp in texts)
                {
                    TMP_FontAsset target = ChooseFont(tmp, regular, bold, light);
                    if (target == null || tmp.font == target) continue;

                    tmp.font = target;
                    _appliedCount++;
                }
                // scope Dispose 시 변경 사항이 있으면 자동 저장
            }
        }

        /// <summary>폰트 이름 또는 스타일 힌트로 Regular/Bold/Light 중 선택.</summary>
        private static TMP_FontAsset ChooseFont(TMP_Text tmp, TMP_FontAsset regular, TMP_FontAsset bold, TMP_FontAsset light)
        {
            if (tmp.fontStyle.HasFlag(FontStyles.Bold))
                return bold ?? regular;
            if (tmp.font != null && tmp.font.name.ToLower().Contains("light"))
                return light ?? regular;
            return regular;
        }
    }
}
#endif
