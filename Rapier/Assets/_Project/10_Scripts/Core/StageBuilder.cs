using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 격자 패턴 배경과 벽(경계)을 런타임에 생성한다.
    /// 프로토타입 전용. 아트 교체 시 제거 예정.
    /// </summary>
    public class StageBuilder : MonoBehaviour
    {
        [Header("스테이지 크기 (월드 단위)")]
        public float stageWidth  = 20f;
        public float stageHeight = 30f;

        [Header("격자 설정")]
        public float gridSize    = 2f;
        public Color gridColor   = new Color(0.25f, 0.25f, 0.35f, 1f);
        public Color bgColor     = new Color(0.15f, 0.15f, 0.2f,  1f);

        [Header("벽 설정")]
        public float wallThickness = 0.5f;
        public Color wallColor     = new Color(0.4f, 0.4f, 0.5f, 1f);

private void Awake()
        {
            ServiceLocator.Register(this);
            BuildBackground();
            BuildGrid();
            BuildWalls();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<StageBuilder>();
        }

        // ── 배경 ─────────────────────────────────────────────────
        private void BuildBackground()
        {
            var go = new GameObject("BG_Base");
            go.transform.SetParent(transform);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = LoadSprite("Square");
            sr.color        = bgColor;
            sr.sortingOrder = -10;

            go.transform.localScale = new Vector3(stageWidth, stageHeight, 1f);
            go.transform.position   = Vector3.zero;
        }

        // ── 격자선 ────────────────────────────────────────────────
        private void BuildGrid()
        {
            var parent = new GameObject("BG_Grid");
            parent.transform.SetParent(transform);

            float lineThickness = 0.05f;
            float halfW = stageWidth  * 0.5f;
            float halfH = stageHeight * 0.5f;

            // 세로선
            for (float x = -halfW; x <= halfW + 0.01f; x += gridSize)
            {
                CreateLine(parent.transform,
                    new Vector3(x, 0f, 0f),
                    new Vector3(lineThickness, stageHeight, 1f));
            }
            // 가로선
            for (float y = -halfH; y <= halfH + 0.01f; y += gridSize)
            {
                CreateLine(parent.transform,
                    new Vector3(0f, y, 0f),
                    new Vector3(stageWidth, lineThickness, 1f));
            }
        }

        private void CreateLine(Transform parent, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent);
            go.transform.position   = pos;
            go.transform.localScale = scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = LoadSprite("Square");
            sr.color        = gridColor;
            sr.sortingOrder = -9;
        }

        // ── 벽 (경계) ─────────────────────────────────────────────
        private void BuildWalls()
        {
            float halfW = stageWidth  * 0.5f;
            float halfH = stageHeight * 0.5f;
            float t     = wallThickness;

            // 상/하/좌/우
            MakeWall("Wall_Top",    new Vector3(0f,        halfH + t * 0.5f, 0f), new Vector3(stageWidth + t * 2, t, 1f));
            MakeWall("Wall_Bottom", new Vector3(0f,       -halfH - t * 0.5f, 0f), new Vector3(stageWidth + t * 2, t, 1f));
            MakeWall("Wall_Left",   new Vector3(-halfW - t * 0.5f, 0f, 0f),       new Vector3(t, stageHeight,     1f));
            MakeWall("Wall_Right",  new Vector3( halfW + t * 0.5f, 0f, 0f),       new Vector3(t, stageHeight,     1f));
        }

        private void MakeWall(string wallName, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(wallName);
            go.transform.SetParent(transform);
            go.transform.position   = pos;
            go.transform.localScale = scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = LoadSprite("Square");
            sr.color        = wallColor;
            sr.sortingOrder = -8;

            // 물리 콜라이더 (플레이어 이동 제한용)
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        // ── 플레이어 이동 제한 (경계 클램프) ─────────────────────
        /// <summary>월드 좌표를 스테이지 안으로 클램프한다.</summary>
        public Vector2 ClampToStage(Vector2 pos, float radius = 0.5f)
        {
            float halfW = stageWidth  * 0.5f - radius;
            float halfH = stageHeight * 0.5f - radius;
            return new Vector2(
                Mathf.Clamp(pos.x, -halfW, halfW),
                Mathf.Clamp(pos.y, -halfH, halfH));
        }

private static Sprite LoadSprite(string name)
        {
            string path = $"Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/{name}.png";
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            // 맰타임에서는 화이트 이미지 사용
            return Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 4, 4),
                new Vector2(0.5f, 0.5f));
#endif
        }
    }
}
