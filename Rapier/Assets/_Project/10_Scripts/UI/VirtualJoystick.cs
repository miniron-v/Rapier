using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 가상 조이스틱 UI.
    /// GestureRecognizer의 JoystickOrigin/Current를 읽어 표시한다.
    /// Move 중에만 표시, Tap/Hold/Swipe 시 숨김.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour
    {
        // ── 조이스틱 UI 설정 ─────────────────────────────────────
        [Header("크기 (기준 해상도 1080x1920 기준)")]
        [SerializeField] private float _outerRadius = 120f;
        [SerializeField] private float _innerRadius = 50f;
        [SerializeField] private Color _outerColor  = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private Color _innerColor  = new Color(1f, 1f, 1f, 0.5f);

        // ── 내부 참조 ─────────────────────────────────────────────
        private Game.Input.GestureRecognizer _gesture;
        private RectTransform _outerRect;
        private RectTransform _innerRect;
        private Canvas        _canvas;
        private CanvasScaler  _scaler;

        private void Start()
        {
            _gesture = ServiceLocator.Get<Game.Input.GestureRecognizer>();
            BuildUI();
            SetVisible(false);
        }

        private void Update()
        {
            if (_gesture == null) return;

            if (_gesture.IsMoving)
            {
                SetVisible(true);
                UpdatePosition();
            }
            else
            {
                SetVisible(false);
            }
        }

        // ── UI 위치 갱신 ─────────────────────────────────────────
private void UpdatePosition()
        {
            // 스크린 좌표 → Canvas RectTransform 로칼 좌표로 변환
            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, _gesture.JoystickOrigin, uiCam, out var originLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, _gesture.JoystickCurrent, uiCam, out var currentLocal);

            _outerRect.anchoredPosition = originLocal;

            // Inner: Outer 반경 내로 클램프
            var offset = currentLocal - originLocal;
            if (offset.magnitude > _outerRadius)
                offset = offset.normalized * _outerRadius;

            _innerRect.anchoredPosition = originLocal + offset;
        }

        // ── 표시/숨김 ─────────────────────────────────────────────
        private void SetVisible(bool visible)
        {
            if (_outerRect != null) _outerRect.gameObject.SetActive(visible);
            if (_innerRect != null) _innerRect.gameObject.SetActive(visible);
        }

        // ── UI 자동 생성 ─────────────────────────────────────────
        private void BuildUI()
        {
            var canvasGO = new GameObject("JoystickCanvas");
            _canvas      = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            _scaler = canvasGO.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution  = new Vector2(1080, 1920);
            _scaler.matchWidthOrHeight   = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Outer (배경 원)
            var outerGO   = CreateCircle(canvasGO.transform, "Joystick_Outer",
                                         _outerRadius * 2f, _outerColor, 10);
            _outerRect    = outerGO.GetComponent<RectTransform>();

            // Inner (핸들 원)
            var innerGO   = CreateCircle(canvasGO.transform, "Joystick_Inner",
                                         _innerRadius * 2f, _innerColor, 11);
            _innerRect    = innerGO.GetComponent<RectTransform>();
        }

        private GameObject CreateCircle(Transform parent, string name, float size, Color color, int order)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta        = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.sprite      = LoadCircleSprite();
            img.color       = color;
            img.raycastTarget = false;

            var sr = go.GetComponent<UnityEngine.UI.Graphic>();
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder    = order;

            return go;
        }

        private Sprite LoadCircleSprite()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/Circle.png");
#else
            return Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,4,4), new Vector2(0.5f, 0.5f));
#endif
        }
    }
}
