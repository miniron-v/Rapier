#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Game.Characters.Ranger;

namespace Game.Editor
{
    /// <summary>
    /// Ranger 투사체 프리팹 생성 도우미.
    ///
    /// [생성 항목]
    ///   Assets/_Project/Prefabs/Player/RangerArrow.prefab
    ///     — SpriteRenderer (흰 캡슐, +Y 방향)
    ///     — BoxCollider2D  (isTrigger, 크기는 Init() 에서 덮어씀)
    ///     — RangerArrow    컴포넌트
    ///
    ///   Assets/_Project/Prefabs/Player/RangerMine.prefab
    ///     — SpriteRenderer (주황 원)
    ///     — CircleCollider2D (isTrigger, 반경은 Init() 에서 덮어씀)
    ///     — RangerMine     컴포넌트
    ///
    /// [사용법]
    ///   Unity 메뉴 → Game/Setup/Create Ranger Prefabs
    ///   완료 후 RangerPlayer 프리팹 인스펙터에서 Arrow Prefab / Mine Prefab 슬롯에 연결.
    /// </summary>
    public static class RangerPrefabSetup
    {
        private const string PREFAB_DIR = "Assets/_Project/Prefabs/Player";

        public static void CreateRangerPrefabs()
        {
            CreateArrowPrefab();
            CreateMinePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RangerPrefabSetup] Arrow / Mine 프리팹 생성 완료.");
        }

        // ── Arrow ─────────────────────────────────────────────────────────

        private static void CreateArrowPrefab()
        {
            string path = $"{PREFAB_DIR}/RangerArrow.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log("[RangerPrefabSetup] RangerArrow.prefab 이미 존재 — 건너뜀.");
                return;
            }

            var go = new GameObject("RangerArrow");

            // SpriteRenderer — 내장 캡슐 스프라이트, +Y 방향 기준
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.color        = new Color(0.2f, 0.8f, 1f, 0.9f); // 하늘색
            sr.sortingOrder = 12;
            // 화살 비율: 폭 0.15, 높이 0.6 (Init 에서 width 로 scale.x 조정됨)
            go.transform.localScale = new Vector3(0.15f, 0.6f, 1f);

            // BoxCollider2D
            var box      = go.AddComponent<BoxCollider2D>();
            box.size      = new Vector2(0.15f, 0.3f); // Init() 에서 (width, 0.3f) 로 덮어씀
            box.isTrigger = true;

            // RangerArrow 컴포넌트
            go.AddComponent<RangerArrow>();

            SavePrefab(go, path);
            Object.DestroyImmediate(go);
        }

        // ── Mine ──────────────────────────────────────────────────────────

        private static void CreateMinePrefab()
        {
            string path = $"{PREFAB_DIR}/RangerMine.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log("[RangerPrefabSetup] RangerMine.prefab 이미 존재 — 건너뜀.");
                return;
            }

            var go = new GameObject("RangerMine");

            // SpriteRenderer — 내장 원형 스프라이트
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            sr.color        = new Color(1f, 0.4f, 0f, 0.9f); // 주황
            sr.sortingOrder = 11;
            go.transform.localScale = Vector3.one * 0.5f;

            // CircleCollider2D
            var circle      = go.AddComponent<CircleCollider2D>();
            circle.radius    = 0.75f; // Init() 에서 MineExplosionRadius * 0.5f 로 덮어씀
            circle.isTrigger = true;

            // RangerMine 컴포넌트
            go.AddComponent<RangerMine>();

            SavePrefab(go, path);
            Object.DestroyImmediate(go);
        }

        // ── 유틸 ──────────────────────────────────────────────────────────

        private static void SavePrefab(GameObject go, string path)
        {
            bool success;
            PrefabUtility.SaveAsPrefabAsset(go, path, out success);
            if (success)
                Debug.Log($"[RangerPrefabSetup] 저장 완료: {path}");
            else
                Debug.LogError($"[RangerPrefabSetup] 저장 실패: {path}");
        }
    }
}
#endif
