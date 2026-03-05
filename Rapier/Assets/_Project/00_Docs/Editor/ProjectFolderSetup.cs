#if UNITY_EDITOR
// 폴더 구조 자동 생성 스크립트. 실행 후 삭제해도 됩니다.
// Menu: Tools/Setup Project Folders
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Game.Core.Editor
{
    public static class ProjectFolderSetup
    {
        [MenuItem("Tools/Setup Project Folders")]
        public static void CreateFolders()
        {
            string[] folders = new string[]
            {
                // 00_Docs
                "Assets/_Project/00_Docs",
                "Assets/_Project/00_Docs/Editor",

                // 10_Scripts
                "Assets/_Project/10_Scripts",
                "Assets/_Project/10_Scripts/Core",
                "Assets/_Project/10_Scripts/Core/Interfaces",
                "Assets/_Project/10_Scripts/Core/Base",
                "Assets/_Project/10_Scripts/Core/Utils",
                "Assets/_Project/10_Scripts/Input",
                "Assets/_Project/10_Scripts/Combat",
                "Assets/_Project/10_Scripts/Combat/Model",
                "Assets/_Project/10_Scripts/Combat/View",
                "Assets/_Project/10_Scripts/Combat/Presenter",
                "Assets/_Project/10_Scripts/Characters",
                "Assets/_Project/10_Scripts/Characters/Base",
                "Assets/_Project/10_Scripts/Characters/Warrior",
                "Assets/_Project/10_Scripts/Characters/Assassin",
                "Assets/_Project/10_Scripts/Characters/Rapier",
                "Assets/_Project/10_Scripts/Characters/Ranger",
                "Assets/_Project/10_Scripts/Enemies",
                "Assets/_Project/10_Scripts/UI",
                "Assets/_Project/10_Scripts/UI/HUD",
                "Assets/_Project/10_Scripts/UI/Common",
                "Assets/_Project/10_Scripts/Data",
                "Assets/_Project/10_Scripts/Data/Characters",
                "Assets/_Project/10_Scripts/Data/Skills",

                // 20_Prefabs
                "Assets/_Project/20_Prefabs",
                "Assets/_Project/20_Prefabs/Characters",
                "Assets/_Project/20_Prefabs/Enemies",
                "Assets/_Project/20_Prefabs/Skills",
                "Assets/_Project/20_Prefabs/UI",

                // 30_ScriptableObjects
                "Assets/_Project/30_ScriptableObjects",
                "Assets/_Project/30_ScriptableObjects/Characters",
                "Assets/_Project/30_ScriptableObjects/Skills",

                // 40_Scenes
                "Assets/_Project/40_Scenes",
                "Assets/_Project/40_Scenes/_Test",
            };

            int created = 0;
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                    string name   = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                    created++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ProjectFolderSetup] 폴더 생성 완료. 신규 생성: {created}개 / 전체: {folders.Length}개");
        }
    }
}
#endif
