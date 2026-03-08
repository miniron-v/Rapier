#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Phase 5 HUD 씬 셋업 도우미.
    /// [TIP-01 준수] Image.Type.Filled 는 반드시 Sprite 를 할당해야 동작한다.
    ///   내장 Sprite 경로: Packages/com.unity.2d.sprite/.../Textures/v2/
    ///   HP 바  → Square.png  / 차지 게이지 → Circle.png
    /// </summary>
    public static class HudSetup
    {
        private const string SPRITE_BASE =
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/";

        private static readonly Color HP_BG_COLOR       = new Color(0.10f, 0.10f, 0.10f, 0.75f);
        private static readonly Color HP_FILL_COLOR     = new Color(0.25f, 0.85f, 0.35f, 1.00f);
        private static readonly Color CHARGE_BG_COLOR   = new Color(1.00f, 1.00f, 1.00f, 0.12f);
        private static readonly Color CHARGE_FILL_COLOR = new Color(1.00f, 0.85f, 0.10f, 0.90f);

        // ── 메뉴 ──────────────────────────────────────────────────
        [MenuItem("Rapier/Setup/Create HUD Canvas")]
        public static void CreateHudCanvas()  => BuildHud(false);

        [MenuItem("Rapier/Setup/Rebuild HUD Canvas")]
        public static void RebuildHudCanvas() => BuildHud(true);

        // ── HUD 생성 ──────────────────────────────────────────────
        private static void BuildHud(bool forceRebuild)
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[HudSetup] 'Player' 오브젝트를 찾을 수 없습니다.");
                return;
            }

            var existing = player.transform.Find("PlayerHudCanvas");
            if (existing != null)
            {
                if (!forceRebuild)
                {
                    Debug.LogWarning("[HudSetup] PlayerHudCanvas 이미 존재. Rebuild HUD Canvas 를 사용하세요.");
                    return;
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            // TIP-01: Filled 모드는 Sprite 없이는 사각형으로 렌더링됨 — 반드시 할당
            var sq = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Square.png");
            var ci = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Circle.png");
            Debug.Log($"[HudSetup] 내장 Sprite — Square={sq != null}, Circle={ci != null}");

            // World Space Canvas (Player 자식, localScale 0.01 → 논리 200px = 월드 2유닛)
            var cvGo = new GameObject("PlayerHudCanvas");
            Undo.RegisterCreatedObjectUndo(cvGo, "Create PlayerHudCanvas");
            cvGo.transform.SetParent(player.transform, false);
            var cv           = cvGo.AddComponent<Canvas>();
            cv.renderMode    = RenderMode.WorldSpace;
            cv.sortingOrder  = 10;
            cvGo.AddComponent<CanvasScaler>();
            cvGo.AddComponent<GraphicRaycaster>();
            var cvRect       = cvGo.GetComponent<RectTransform>();
            cvRect.sizeDelta = new Vector2(200f, 200f);
            cvGo.transform.localScale    = Vector3.one * 0.01f;
            cvGo.transform.localPosition = Vector3.zero;

            // ── HP 바 (위쪽 60유닛 = 월드 0.6유닛) ──
            // 배경
            var hpBg     = Img(cvGo.transform, "HpBarBg", HP_BG_COLOR, sq);
            SetCenter(hpBg.GetComponent<RectTransform>(), new Vector2(140f, 14f), new Vector2(0f, 60f));

            // Fill (Horizontal)
            var hpFillImg = Img(hpBg.transform, "HpFill", HP_FILL_COLOR, sq).GetComponent<Image>();
            hpFillImg.type       = Image.Type.Filled;
            hpFillImg.fillMethod = Image.FillMethod.Horizontal;
            hpFillImg.fillOrigin = 0; // Left
            hpFillImg.fillAmount = 1f;
            Stretch(hpFillImg.GetComponent<RectTransform>(), new Vector2(0f, 0.5f));

            // ── 원형 차지 게이지 (중심, 80×80유닛) ──
            // 배경 원
            var cgBgImg = Img(cvGo.transform, "ChargeGaugeBg", CHARGE_BG_COLOR, ci).GetComponent<Image>();
            cgBgImg.type       = Image.Type.Filled;
            cgBgImg.fillMethod = Image.FillMethod.Radial360;
            cgBgImg.fillAmount = 1f;
            SetCenter(cgBgImg.GetComponent<RectTransform>(), new Vector2(80f, 80f), Vector2.zero);

            // 전경 원 (채워짐)
            var cgFillImg = Img(cvGo.transform, "ChargeGaugeFill", CHARGE_FILL_COLOR, ci).GetComponent<Image>();
            cgFillImg.type       = Image.Type.Filled;
            cgFillImg.fillMethod = Image.FillMethod.Radial360;
            cgFillImg.fillAmount = 0f;
            SetCenter(cgFillImg.GetComponent<RectTransform>(), new Vector2(80f, 80f), Vector2.zero);

            // HudView (Player 루트에 부착, 자식 Canvas 에서 이름으로 자동 찾음)
            if (player.GetComponent<Game.UI.HudView>() == null)
                player.AddComponent<Game.UI.HudView>();

            Selection.activeGameObject = cvGo;
            Debug.Log("[HudSetup] PlayerHudCanvas 생성 완료.");
        }

        // ── Enemy_Template HP 바 ──────────────────────────────────
        [MenuItem("Rapier/Setup/Add EnemyHpBar to Template")]
        public static void AddEnemyHpBarToTemplate()
        {
            var guids = AssetDatabase.FindAssets("Enemy_Template t:Prefab");
            if (guids.Length == 0) { Debug.LogError("[HudSetup] Enemy_Template.prefab 없음."); return; }

            var sq   = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_BASE + "Square.png");
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);

            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = scope.prefabContentsRoot;
                if (root.transform.Find("EnemyHpBarCanvas") != null)
                {
                    Debug.LogWarning("[HudSetup] EnemyHpBarCanvas 이미 존재.");
                    return;
                }

                var cvGo = new GameObject("EnemyHpBarCanvas");
                cvGo.transform.SetParent(root.transform, false);
                var cv = cvGo.AddComponent<Canvas>();
                cv.renderMode   = RenderMode.WorldSpace;
                cv.sortingOrder = 5;
                cvGo.AddComponent<CanvasScaler>();
                cvGo.AddComponent<GraphicRaycaster>();
                var cvRect = cvGo.GetComponent<RectTransform>();
                cvRect.sizeDelta             = new Vector2(100f, 12f);
                cvGo.transform.localScale    = Vector3.one * 0.012f;
                cvGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);

                var bgImg = Img(cvGo.transform, "HpBg", new Color(0.1f, 0.1f, 0.1f, 0.8f), sq).GetComponent<Image>();
                Stretch(bgImg.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f));

                var fillImg = Img(cvGo.transform, "HpFill", new Color(0.9f, 0.25f, 0.25f), sq).GetComponent<Image>();
                fillImg.type       = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillAmount = 1f;
                Stretch(fillImg.GetComponent<RectTransform>(), new Vector2(0f, 0.5f));

                var hpBar = cvGo.AddComponent<Game.Enemies.EnemyHpBar>();
                typeof(Game.Enemies.EnemyHpBar)
                    .GetField("_fillImage",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance)
                    ?.SetValue(hpBar, fillImg);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[HudSetup] EnemyHpBar 추가 완료. ({path})");
        }

        // ── 헬퍼 ──────────────────────────────────────────────────
        /// <summary>Image GO 생성. TIP-01: sprite 는 항상 명시적으로 전달.</summary>
        private static GameObject Img(Transform parent, string name, Color color, Sprite sprite)
        {
            var go     = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img    = go.AddComponent<Image>();
            img.color  = color;
            img.sprite = sprite;
            return go;
        }

        private static void SetCenter(RectTransform rt, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = size;
            rt.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rt, Vector2 pivot)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = pivot;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
