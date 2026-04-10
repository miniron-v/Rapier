#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// 프리팹에서 MissingScript 컴포넌트를 제거하고
    /// 지정 컴포넌트를 추가하는 일회성 유틸리티.
    /// </summary>
    public static class PrefabMissingScriptCleaner
    {
        [MenuItem("Rapier/Setup/Fix Missing Scripts in Boss Prefabs")]
        public static void FixBossPrefabs()
        {
            string[] paths = new[]
            {
                "Assets/_Project/Prefabs/Enemies/Enemy_Template.prefab",
                "Assets/_Project/Prefabs/Boss/Titan_Boss.prefab",
                "Assets/_Project/Prefabs/Boss/Specter_Boss.prefab",
            };

            foreach (var path in paths)
                CleanAndSetup(path);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PrefabMissingScriptCleaner] 완료!");
        }

        private static void CleanAndSetup(string path)
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var root = scope.prefabContentsRoot;

            // Missing Script 제거
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            Debug.Log($"[PrefabCleaner] {path} — MissingScript {removed}개 제거");

            // 경로별 컴포넌트 추가
            if (path.Contains("Enemy_Template"))
            {
                if (root.GetComponent<Game.Enemies.NormalEnemyPresenter>() == null)
                    root.AddComponent<Game.Enemies.NormalEnemyPresenter>();
            }
            else if (path.Contains("Titan_Boss"))
            {
                if (root.GetComponent<Game.Enemies.TitanBossPresenter>() == null)
                    root.AddComponent<Game.Enemies.TitanBossPresenter>();
            }
            else if (path.Contains("Specter_Boss"))
            {
                if (root.GetComponent<Game.Enemies.SpecterBossPresenter>() == null)
                    root.AddComponent<Game.Enemies.SpecterBossPresenter>();
            }
        }
    }
}
#endif
