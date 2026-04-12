#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Rapier_Player.prefab 을 복제하여 Assassin_Player.prefab 을 생성하는 일회성 도구.
    ///
    /// [동작 순서]
    ///   1. Rapier_Player.prefab 을 Assassin_Player.prefab 으로 복사 (AssetDatabase.CopyAsset)
    ///   2. PrefabUtility.LoadPrefabContents 로 프리팹 열기
    ///   3. 루트의 RapierPresenter 컴포넌트 제거
    ///   4. AssassinPresenter 컴포넌트 추가
    ///   5. AssassinPresenter 직렬화 필드 배선:
    ///      - _statData      : AssassinStatData.asset
    ///      - _phantomPrefab : PhantomPresenter.prefab
    ///   6. holdCurve / exitCurve / dodgeDashCurve / holdDuration 은
    ///      Rapier_Player 프리팹의 RapierPresenter 에서 복사 (SerializedObject 경유)
    ///   7. PrefabUtility.SaveAsPrefabAsset 로 저장
    ///
    /// [메뉴]
    ///   Rapier/Character/Create Assassin Prefab
    /// </summary>
    public static class AssassinPrefabSetup
    {
        // ── 에셋 경로 ─────────────────────────────────────────────
        private const string RAPIER_PREFAB_PATH =
            "Assets/_Project/Prefabs/Player/Rapier_Player.prefab";

        private const string ASSASSIN_PREFAB_PATH =
            "Assets/_Project/Prefabs/Player/Assassin_Player.prefab";

        private const string ASSASSIN_STAT_PATH =
            "Assets/_Project/ScriptableObjects/Characters/AssassinStatData.asset";

        private const string PHANTOM_PREFAB_PATH =
            "Assets/_Project/Prefabs/Player/PhantomPresenter.prefab";

        [MenuItem("Rapier/Character/Create Assassin Prefab")]
        public static void CreateAssassinPrefab()
        {
            // ── 소스 프리팹 확인 ──────────────────────────────────
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(RAPIER_PREFAB_PATH))
            {
                Debug.LogError($"[AssassinPrefabSetup] Rapier_Player.prefab 없음: {RAPIER_PREFAB_PATH}");
                return;
            }

            // ── 복제 ─────────────────────────────────────────────
            if (!AssetDatabase.CopyAsset(RAPIER_PREFAB_PATH, ASSASSIN_PREFAB_PATH))
            {
                Debug.LogError($"[AssassinPrefabSetup] 복제 실패. 대상 경로: {ASSASSIN_PREFAB_PATH}");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[AssassinPrefabSetup] 복제 완료: {ASSASSIN_PREFAB_PATH}");

            // ── 복제본 프리팹 편집 ────────────────────────────────
            var prefabRoot = PrefabUtility.LoadPrefabContents(ASSASSIN_PREFAB_PATH);
            if (prefabRoot == null)
            {
                Debug.LogError("[AssassinPrefabSetup] LoadPrefabContents 실패.");
                return;
            }

            try
            {
                // ── holdCurve 등 값 미리 읽어두기 (원본 Rapier SO 참조 경유가 아닌
                //    프리팹에 직렬화된 값을 사용하므로, 복제된 루트에서 SerializedObject 를 읽는다) ──
                var assassinSo   = new SerializedObject(prefabRoot.GetComponent<MonoBehaviour>() /* 임시 참조, 이후 덮어씀 */);

                // RapierPresenter 를 먼저 SerializedObject 로 읽어 커브 값 캐싱
                var rapierComp = prefabRoot.GetComponent("RapierPresenter") as MonoBehaviour;
                if (rapierComp == null)
                {
                    // GetComponent(string) 이 null 이면 타입으로 재시도
                    var comps = prefabRoot.GetComponents<MonoBehaviour>();
                    foreach (var c in comps)
                    {
                        if (c.GetType().Name == "RapierPresenter")
                        {
                            rapierComp = c;
                            break;
                        }
                    }
                }

                AnimationCurve holdCurve      = null;
                AnimationCurve exitCurve      = null;
                AnimationCurve dodgeDashCurve = null;
                float          holdDuration   = 2.4f;

                if (rapierComp != null)
                {
                    var rSo = new SerializedObject(rapierComp);
                    var holdCurveProp      = rSo.FindProperty("holdCurve");
                    var exitCurveProp      = rSo.FindProperty("exitCurve");
                    var dodgeDashCurveProp = rSo.FindProperty("dodgeDashCurve");
                    var holdDurationProp   = rSo.FindProperty("holdDuration");

                    if (holdCurveProp      != null) holdCurve      = holdCurveProp.animationCurveValue;
                    if (exitCurveProp      != null) exitCurve      = exitCurveProp.animationCurveValue;
                    if (dodgeDashCurveProp != null) dodgeDashCurve = dodgeDashCurveProp.animationCurveValue;
                    if (holdDurationProp   != null) holdDuration   = holdDurationProp.floatValue;

                    Debug.Log($"[AssassinPrefabSetup] holdDuration={holdDuration} 읽기 완료.");
                }
                else
                {
                    Debug.LogWarning("[AssassinPrefabSetup] RapierPresenter 컴포넌트를 찾지 못해 커브 기본값 사용.");
                }

                // ── RapierPresenter 제거 ──────────────────────────
                var rapierToRemove = prefabRoot.GetComponent("RapierPresenter") as MonoBehaviour;
                if (rapierToRemove == null)
                {
                    var comps = prefabRoot.GetComponents<MonoBehaviour>();
                    foreach (var c in comps)
                    {
                        if (c.GetType().Name == "RapierPresenter")
                        {
                            rapierToRemove = c;
                            break;
                        }
                    }
                }

                if (rapierToRemove != null)
                {
                    Object.DestroyImmediate(rapierToRemove);
                    Debug.Log("[AssassinPrefabSetup] RapierPresenter 제거 완료.");
                }
                else
                {
                    Debug.LogWarning("[AssassinPrefabSetup] RapierPresenter 를 찾지 못해 제거 생략.");
                }

                // ── AssassinPresenter 추가 ────────────────────────
                // 타입을 이름으로 조회 (Assembly-CSharp)
                var assassinType = System.Type.GetType("Game.Characters.Assassin.AssassinPresenter, Assembly-CSharp");
                if (assassinType == null)
                {
                    // 폴백: UnityEditor API 경유
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        assassinType = asm.GetType("Game.Characters.Assassin.AssassinPresenter");
                        if (assassinType != null) break;
                    }
                }

                if (assassinType == null)
                {
                    Debug.LogError("[AssassinPrefabSetup] AssassinPresenter 타입을 찾을 수 없습니다.");
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    return;
                }

                var assassinComp = prefabRoot.AddComponent(assassinType) as MonoBehaviour;
                if (assassinComp == null)
                {
                    Debug.LogError("[AssassinPrefabSetup] AssassinPresenter AddComponent 실패.");
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    return;
                }

                Debug.Log("[AssassinPrefabSetup] AssassinPresenter 추가 완료.");

                // ── 직렬화 필드 배선 ──────────────────────────────
                var aSo = new SerializedObject(assassinComp);

                // _statData
                var statData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ASSASSIN_STAT_PATH);
                if (statData == null)
                    Debug.LogWarning($"[AssassinPrefabSetup] AssassinStatData 없음: {ASSASSIN_STAT_PATH}");
                else
                {
                    var statProp = aSo.FindProperty("_statData");
                    if (statProp != null) statProp.objectReferenceValue = statData;
                }

                // _phantomPrefab
                var phantomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PHANTOM_PREFAB_PATH);
                if (phantomPrefab == null)
                    Debug.LogWarning($"[AssassinPrefabSetup] PhantomPresenter.prefab 없음: {PHANTOM_PREFAB_PATH}");
                else
                {
                    var phantomProp = aSo.FindProperty("_phantomPrefab");
                    if (phantomProp != null) phantomProp.objectReferenceValue = phantomPrefab;
                }

                // holdCurve / exitCurve / dodgeDashCurve / holdDuration (Base 직렬화 필드)
                if (holdCurve != null)
                {
                    var p = aSo.FindProperty("holdCurve");
                    if (p != null) p.animationCurveValue = holdCurve;
                }
                if (exitCurve != null)
                {
                    var p = aSo.FindProperty("exitCurve");
                    if (p != null) p.animationCurveValue = exitCurve;
                }
                if (dodgeDashCurve != null)
                {
                    var p = aSo.FindProperty("dodgeDashCurve");
                    if (p != null) p.animationCurveValue = dodgeDashCurve;
                }

                var holdDurProp = aSo.FindProperty("holdDuration");
                if (holdDurProp != null) holdDurProp.floatValue = holdDuration;

                aSo.ApplyModifiedProperties();

                // ── 프리팹 저장 ───────────────────────────────────
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ASSASSIN_PREFAB_PATH);
                Debug.Log($"[AssassinPrefabSetup] Assassin_Player.prefab 저장 완료: {ASSASSIN_PREFAB_PATH}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
        }
    }
}
#endif
