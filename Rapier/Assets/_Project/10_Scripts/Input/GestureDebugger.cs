using UnityEngine;
using UnityEngine.UI;
using Game.Input;
using Game.Core;

namespace Game.UI.Debug
{
    /// <summary>
    /// Phase 1 테스트 전용. GestureRecognizer 이벤트를 화면에 표시한다.
    /// Phase 2 시작 전 제거 예정.
    /// </summary>
    [RequireComponent(typeof(GestureRecognizer))]
    public class GestureDebugger : MonoBehaviour
    {
        [Header("UI (자동 생성됨)")]
        private Text _stateText;
        private Text _logText;
        private string _logBuffer = "";
        private const int MAX_LOG_LINES = 8;

        private GestureRecognizer _gesture;

        private void Awake()
        {
            _gesture = GetComponent<GestureRecognizer>();
            ServiceLocator.Register(_gesture);
            BuildDebugUI();
        }

        private void OnEnable()
        {
            _gesture.OnTap       += dir  => Log($"TAP  @ {dir}");
            _gesture.OnSwipe     += dir  => Log($"SWIPE → {dir}");
            _gesture.OnDragDelta += d    => UpdateState("DRAG");
            _gesture.OnHold      += dur  => UpdateState($"HOLD {dur:F1}s");
            _gesture.OnRelease   += last => Log($"RELEASE (was {last})");
            _gesture.OnJustDodge += dir  => Log($"★ JUST DODGE → {dir}");
        }

        private void OnDisable()
        {
            _gesture.OnTap       -= dir  => Log($"TAP  @ {dir}");
            _gesture.OnSwipe     -= dir  => Log($"SWIPE → {dir}");
            _gesture.OnDragDelta -= d    => UpdateState("DRAG");
            _gesture.OnHold      -= dur  => UpdateState($"HOLD {dur:F1}s");
            _gesture.OnRelease   -= last => Log($"RELEASE (was {last})");
            _gesture.OnJustDodge -= dir  => Log($"★ JUST DODGE → {dir}");
        }

        private void Update()
        {
            if (_stateText != null)
                _stateText.text = $"State: {_gesture.CurrentState}";
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<GestureRecognizer>();
        }

        // ── UI 자동 생성 ──────────────────────────────────────────────
private void BuildDebugUI()
        {
            // Canvas
            var canvasGO = new GameObject("DebugCanvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            // CanvasScaler: ScaleWithScreenSize (iPhone 12 기준 1170x2532, 데비시뮬레이터 데도 맞게)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1080, 1920);
            scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight   = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // 상태 텍스트 (좌상단)
            _stateText = CreateText(canvasGO, "StateText",
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(0f, 1f),
                pivot:     new Vector2(0f, 1f),
                anchoredPos: new Vector2(20f, -20f),
                sizeDelta: new Vector2(600f, 60f),
                color: Color.yellow, fontSize: 36);

            // 로그 텍스트 (상태 아래)
            _logText = CreateText(canvasGO, "LogText",
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(0f, 1f),
                pivot:     new Vector2(0f, 1f),
                anchoredPos: new Vector2(20f, -90f),
                sizeDelta: new Vector2(700f, 400f),
                color: Color.white, fontSize: 28);
        }

private Text CreateText(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            Color color, int fontSize)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin        = anchorMin;
            rect.anchorMax        = anchorMax;
            rect.pivot            = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta        = sizeDelta;
            var text = go.AddComponent<Text>();
            text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize  = fontSize;
            text.color     = color;
            text.text      = "";
            return text;
        }

        // ── 로그 ──────────────────────────────────────────────────────
        private void UpdateState(string msg)
        {
            if (_stateText != null) _stateText.text = $"State: {msg}";
        }

        private void Log(string msg)
        {
            UnityEngine.Debug.Log($"[Gesture] {msg}");
            var lines = _logBuffer.Split('\n');
            if (lines.Length >= MAX_LOG_LINES)
                _logBuffer = string.Join("\n", lines, 1, lines.Length - 1);
            _logBuffer += $"\n{msg}";
            if (_logText != null) _logText.text = _logBuffer;
        }
    }
}
