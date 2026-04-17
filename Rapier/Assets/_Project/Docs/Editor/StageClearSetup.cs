#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Core.Stage;
using Game.UI.Intermission;

namespace Game.Editor
{
    /// <summary>
    /// StageClearManager 씬 배치 도우미.
    ///
    /// [동작]
    ///   씬에 [StageClearManager] GameObject 를 생성하고 StageClearManager 컴포넌트를 부착한다.
    ///   이미 존재하면 Rebuild 메뉴로 재생성. StageClearView 를 FindObjectOfType 으로 자동 탐색하여 연결.
    ///
    /// [실행]
    ///   Rapier/Stage/Rebuild StageClearManager
    ///
    /// [주의]
    ///   에디터 전용 스크립트. Editor/ 폴더에 위치하므로 빌드에 포함되지 않음 (CLAUDE.md L-12 참조).
    ///   FindObjectOfType 은 에디터 툴에서만 허용 (런타임 로직 금지 — CLAUDE.md §9).
    /// </summary>
    public static class StageClearSetup
    {
        private const string GO_NAME = "[StageClearManager]";

        [MenuItem("Rapier/Stage/Rebuild StageClearManager")]
        public static void RebuildStageClearManager()
        {
            // 기존 오브젝트 제거
            var existing = GameObject.Find(GO_NAME);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log("[StageClearSetup] 기존 [StageClearManager] 제거.");
            }

            // 신규 GameObject 생성
            var go = new GameObject(GO_NAME);
            Undo.RegisterCreatedObjectUndo(go, "Create StageClearManager");

            var mgr = go.AddComponent<StageClearManager>();
            EditorUtility.SetDirty(mgr);

            // StageClearView 자동 탐색 및 연결
            var view = Object.FindObjectOfType<StageClearView>();
            if (view != null)
            {
                // SerializedObject 를 통해 _stageClearView 필드에 참조 주입
                var so    = new SerializedObject(mgr);
                var prop  = so.FindProperty("_stageClearView");
                if (prop != null)
                {
                    prop.objectReferenceValue = view;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[StageClearSetup] StageClearView 자동 연결 완료: {view.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning("[StageClearSetup] _stageClearView 직렬화 필드를 찾을 수 없음. 수동 연결 필요.");
                }
            }
            else
            {
                Debug.LogWarning("[StageClearSetup] StageClearView 를 씬에서 찾을 수 없음. Inspector 에서 수동 연결 필요.");
            }

            // 씬 저장
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = go;
            Debug.Log("[StageClearSetup] [StageClearManager] 생성 및 씬 저장 완료!");
        }
    }
}
#endif
