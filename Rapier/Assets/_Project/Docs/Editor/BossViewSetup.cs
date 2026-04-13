#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// 보스 프리팹의 EnemyView 컴포넌트를 BossView로 교체하는 에디터 도우미.
    /// [실행] Rapier/Phase 18 Bug Fix/Swap BossView on Boss Prefabs
    ///
    /// EnemyPresenterBase의 [RequireComponent(typeof(EnemyView))] 는 제거됨.
    /// BossView IS-A EnemyView이므로 GetComponent&lt;EnemyView&gt;()로 BossView를 가져올 수 있다.
    /// </summary>
    public static class BossViewSetup
    {
        [MenuItem("Rapier/Phase 18 Bug Fix/Swap BossView on Boss Prefabs")]
        public static void SwapBossViews()
        {
            // BossPresenterBase를 가진 프리팹 찾기
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                // BossPresenterBase 상속 타입을 가진 프리팹만 처리
                var presenter = go.GetComponent<Game.Enemies.BossPresenterBase>();
                if (presenter == null) continue;

                // EnemyView가 있고 BossView가 없는 경우만 처리
                var enemyView = go.GetComponent<Game.Enemies.EnemyView>();
                var bossView  = go.GetComponent<Game.Enemies.BossView>();
                if (enemyView == null || bossView != null) continue;

                // 프리팹 편집 컨텍스트에서 컴포넌트 교체
                var prefabGo = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var ev = prefabGo.GetComponent<Game.Enemies.EnemyView>();
                    if (ev != null)
                    {
                        // BossView가 아닌 순수 EnemyView만 제거 (BossView IS-A EnemyView)
                        if (ev.GetType() == typeof(Game.Enemies.EnemyView))
                            Undo.DestroyObjectImmediate(ev);
                    }
                    var addedBossView = Undo.AddComponent<Game.Enemies.BossView>(prefabGo);
                    if (addedBossView == null)
                    {
                        Debug.LogError($"[BossViewSetup] {path} → BossView 추가 실패. 프리팹을 수동으로 확인하세요.");
                        continue;
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefabGo, path);
                    count++;
                    Debug.Log($"[BossViewSetup] {path} → BossView 교체 완료");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabGo);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[BossViewSetup] 총 {count}개 보스 프리팹 교체 완료.");
        }
    }
}
#endif
