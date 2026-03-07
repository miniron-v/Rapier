using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Input;
using Game.Core;

namespace Game.UI.Debug
{
    /// <summary>
    /// 테스트용 온스크린 로그.
    /// GestureRecognizer 이벤트를 화면에 표시한다.
    /// Phase 5 HUD 완성 후 제거 예정.
    /// </summary>
    public class OnScreenLog : MonoBehaviour
    {
        private Text   _logText;
        private readonly Queue<string> _lines = new Queue<string>();
        private const int MAX_LINES = 10;

        private GestureRecognizer _gesture;

        private void Start()
        {
            _gesture = ServiceLocator.Get<GestureRecognizer>();
            if (_gesture == null)
            {
                UnityEngine.Debug.LogWarning("[OnScreenLog] GestureRecognizer 없음.");
                return;
            }

            BuildUI();

            _gesture.OnTap       += dir  => AddLog($"TAP @ ({dir.x:F0},{dir.y:F0})");
            _gesture.OnSwipe     += dir  => AddLog($"SWIPE {DirLabel(dir)}");
            _gesture.OnMoveDirection += _ => AddLog("MOVE");
            _gesture.OnHold      += dur  => AddLog($"HOLD {dur:F1}s");
            _gesture.OnRelease   += last => AddLog($"RELEASE ({last})");
            _gesture.OnJustDodge += dir  => AddLog($"★ JUST DODGE {DirLabel(dir)}");
        }

        private void AddLog(string msg)
        {
            if (_lines.Count >= MAX_LINES) _lines.Dequeue();
            _lines.Enqueue(msg);
            if (_logText != null)
                _logText.text = string.Join("\n", _lines);
        }

        private static string DirLabel(Vector2 dir)
        {
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                return dir.x > 0 ? "→" : "←";
            return dir.y > 0 ? "↑" : "↓";
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("OnScreenLogCanvas");
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // 로그 패널 (우상단)
            var panel = new GameObject("LogPanel");
            panel.transform.SetParent(canvasGO.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin        = new Vector2(1f, 1f);
            panelRect.anchorMax        = new Vector2(1f, 1f);
            panelRect.pivot            = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-20f, -20f);
            panelRect.sizeDelta        = new Vector2(500f, 450f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.5f);

            var textGO = new GameObject("LogText");
            textGO.transform.SetParent(panel.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin        = Vector2.zero;
            textRect.anchorMax        = Vector2.one;
            textRect.pivot            = new Vector2(0.5f, 0.5f);
            textRect.offsetMin        = new Vector2(10f, 10f);
            textRect.offsetMax        = new Vector2(-10f, -10f);

            _logText           = textGO.AddComponent<Text>();
            _logText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _logText.fontSize  = 28;
            _logText.color     = Color.white;
            _logText.alignment = TextAnchor.UpperRight;
            _logText.text      = "-- 입력 로그 --";
        }
    }
}
