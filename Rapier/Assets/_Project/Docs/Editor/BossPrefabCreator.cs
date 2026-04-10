#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Game.Enemies;

namespace Game.Editor
{
    /// <summary>
    /// 신규 5종 보스 프리팹 생성 유틸리티.
    ///
    /// [동작]
    ///   Titan_Boss.prefab 을 템플릿으로 임시 인스턴스 로드 →
    ///   TitanBossPresenter 제거 후 대상 *BossPresenter 추가 →
    ///   각 이름에 맞는 프리팹 경로로 SaveAsPrefabAsset →
    ///   임시 인스턴스 정리.
    ///
    /// [AttackAction 주의]
    ///   EnemyAttackAction 은 [Serializable] 순수 C# 클래스이며,
    ///   BossStatData SO 의 attackSequence / phase2Sequence 에 [SerializeReference]로
    ///   저장됩니다. 프리팹에 MonoBehaviour 로 추가하는 것이 아닙니다.
    ///   StatData 는 BossRushManager.Spawn() 시점에 주입됩니다.
    ///
    /// [GravekeeperBossPresenter 주의]
    ///   _minionPrefab / _minionData 는 이 유틸리티에서 null 로 남깁니다.
    ///   Unity Inspector 에서 수동으로 연결하거나 별도 Setup 메뉴를 사용하세요.
    ///
    /// [TwinPhantomsBossPresenter 주의]
    ///   TwinPhantoms 는 씬에 2개 인스턴스를 배치하고 partner 필드를 서로 연결해야 합니다.
    ///   이 유틸리티는 단일 프리팹을 생성합니다. 씬 배치 후 수동 연결이 필요합니다.
    ///
    /// [메뉴]
    ///   Rapier/BossRush/Create New Boss Prefabs
    /// </summary>
    public static class BossPrefabCreator
    {
        private const string TEMPLATE_PATH         = "Assets/_Project/Prefabs/Boss/Titan_Boss.prefab";
        private const string ENEMY_TEMPLATE_PATH   = "Assets/_Project/Prefabs/Enemies/Enemy_Template.prefab";
        private const string BOSS_DIR              = "Assets/_Project/Prefabs/Boss/";
        private const string ENEMY_DIR             = "Assets/_Project/Prefabs/Enemies/";

        [MenuItem("Rapier/BossRush/Create New Boss Prefabs")]
        public static void CreateNewBossPrefabs()
        {
            CreateBossPrefab<PyromancerBossPresenter>("Pyromancer_Boss");
            CreateBossPrefab<BerserkerBossPresenter>("Berserker_Boss");
            CreateBossPrefab<StormcallerBossPresenter>("Stormcaller_Boss");
            CreateBossPrefab<GravekeeperBossPresenter>("Gravekeeper_Boss");
            CreateBossPrefab<TwinPhantomsBossPresenter>("TwinPhantoms_Boss");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BossPrefabCreator] 신규 5종 보스 프리팹 생성 완료.");
            Debug.Log("  ※ GravekeeperBossPresenter._minionPrefab / _minionData 는 Inspector에서 수동 연결 필요.");
            Debug.Log("  ※ TwinPhantomsBossPresenter.partner 는 씬 배치 후 서로 수동 연결 필요.");
        }

        // ── Gravekeeper 전용 미니언 프리팹 생성 ───────────────────
        [MenuItem("Rapier/BossRush/Create Gravekeeper Minion Prefab")]
        public static void CreateGravekeeperMinionPrefab()
        {
            const string destPath = ENEMY_DIR + "GravekeeperMinion.prefab";

            // 이미 존재하면 스킵
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
            {
                Debug.LogWarning("[BossPrefabCreator] GravekeeperMinion.prefab 이미 존재 — 스킵.");
                return;
            }

            // Enemy_Template 확인
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(ENEMY_TEMPLATE_PATH);
            if (template == null)
            {
                Debug.LogError($"[BossPrefabCreator] 템플릿 {ENEMY_TEMPLATE_PATH} 없음 — GravekeeperMinion 생성 중단.");
                return;
            }

            var tempGo = PrefabUtility.LoadPrefabContents(ENEMY_TEMPLATE_PATH);

            try
            {
                tempGo.name = "GravekeeperMinion";

                PrefabUtility.SaveAsPrefabAsset(tempGo, destPath);

                Debug.Log($"[BossPrefabCreator] GravekeeperMinion.prefab 생성 완료 → {destPath}");
                Debug.Log("  ※ GravekeeperBossPresenter._minionPrefab / _minionData 는 Inspector에서 수동 연결 필요.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(tempGo);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ── 프리팹 생성 ────────────────────────────────────────────
        private static void CreateBossPrefab<TPresenter>(string prefabName)
            where TPresenter : BossPresenterBase
        {
            string destPath = BOSS_DIR + prefabName + ".prefab";

            // 이미 존재하면 스킵
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
            {
                Debug.LogWarning($"[BossPrefabCreator] {prefabName}.prefab 이미 존재 — 스킵.");
                return;
            }

            // 템플릿 확인
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TEMPLATE_PATH);
            if (template == null)
            {
                Debug.LogError($"[BossPrefabCreator] 템플릿 {TEMPLATE_PATH} 없음 — {prefabName} 생성 중단.");
                return;
            }

            // ── 임시 인스턴스 로드 ─────────────────────────────────
            // PrefabUtility.LoadPrefabContents: 씬에 인스턴스화하지 않고 prefab 내용을 편집 가능한 임시 오브젝트로 로드.
            var tempGo = PrefabUtility.LoadPrefabContents(TEMPLATE_PATH);

            try
            {
                // ── 이름 변경 ──────────────────────────────────────
                tempGo.name = prefabName;

                // ── TitanBossPresenter 제거 ────────────────────────
                var titan = tempGo.GetComponent<TitanBossPresenter>();
                if (titan != null)
                    Object.DestroyImmediate(titan);

                // 혹시 다른 BossPresenterBase 파생이 남아있으면 제거
                var existing = tempGo.GetComponent<BossPresenterBase>();
                if (existing != null)
                    Object.DestroyImmediate(existing);

                // ── 대상 Presenter 추가 ────────────────────────────
                tempGo.AddComponent<TPresenter>();

                // ── 프리팹으로 저장 ────────────────────────────────
                PrefabUtility.SaveAsPrefabAsset(tempGo, destPath);

                Debug.Log($"[BossPrefabCreator] {prefabName}.prefab 생성 완료 → {destPath}");
            }
            finally
            {
                // ── 임시 인스턴스 정리 ─────────────────────────────
                PrefabUtility.UnloadPrefabContents(tempGo);
            }
        }
    }
}
#endif
