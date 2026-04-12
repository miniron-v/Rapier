using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Game.Input
{
    /// <summary>
    /// 터치 입력을 제스처로 판별해 이벤트로 발행한다.
    ///
    /// [판별 흐름]
    ///   FingerDown  → 터치 시작, 모든 상태 초기화
    ///   FingerMove  → _currentPos 갱신만 (판별 없음)
    ///   Update      → 시간 누적
    ///                 - dist >= MOVE_START_DISTANCE AND duration >= SWIPE_MAX_DURATION
    ///                   → Move 확정 (조이스틱 발행 시작)
    ///                 - dist < TAP_MAX_DISTANCE AND duration >= HOLD_MIN_DURATION
    ///                   → Hold 확정
    ///   FingerUp    → 최종 판별
    ///                 - Move 중 → MoveEnd
    ///                 - dist >= SWIPE_MIN_DISTANCE AND duration < SWIPE_MAX_DURATION
    ///                   → Swipe 또는 JustDodge (AttackWindow 열려있을 때)
    ///                 - dist < TAP_MAX_DISTANCE AND duration < TAP_MAX_DURATION
    ///                   → Tap
    ///
    /// [JustDodge 트리거]
    ///   TriggerJustDodge(direction) : 게임 로직에서 직접 호출하는 정식 API.
    ///   회피 중 피격 판정 등 코드에서 직접 발동할 때 사용.
    /// </summary>
    public class GestureRecognizer : MonoBehaviour
    {
        // ── 판별 기준 상수 ───────────────────────────────────────
        private const float TAP_MAX_DISTANCE    = 20f;
        private const float TAP_MAX_DURATION    = 0.2f;
        private const float SWIPE_MIN_DISTANCE  = 60f;
        private const float SWIPE_MAX_DURATION  = 0.25f;
        private const float MOVE_START_DISTANCE = 20f;
        private const float HOLD_MIN_DURATION   = 0.3f;

        // ── 이벤트 ───────────────────────────────────────────────
        public event Action<Vector2>    OnTap;
        public event Action<Vector2>    OnSwipe;
        public event Action<Vector2>    OnMoveDirection;
        public event Action             OnMoveEnd;
        public event Action<float>      OnHold;
        public event Action<InputState> OnRelease;
        public event Action<Vector2>    OnJustDodge;

        // 조이스틱 상태 공개 (UI 표시용)
        public Vector2    JoystickOrigin  { get; private set; }
        public Vector2    JoystickCurrent { get; private set; }
        public bool       IsMoving        { get; private set; }
        public InputState CurrentState    { get; private set; } = InputState.None;

        // ── 내부 상태 ────────────────────────────────────────────
        private bool    _isTouching;
        private Vector2 _startPos;
        private Vector2 _currentPos;
        private float   _touchDuration;
        private bool    _gestureCommitted;
        private int     _attackWindowCount;

        // ── 라이프사이클 ─────────────────────────────────────────
        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            Touch.onFingerDown += HandleFingerDown;
            Touch.onFingerMove += HandleFingerMove;
            Touch.onFingerUp   += HandleFingerUp;
        }

        private void OnDisable()
        {
            Touch.onFingerDown -= HandleFingerDown;
            Touch.onFingerMove -= HandleFingerMove;
            Touch.onFingerUp   -= HandleFingerUp;
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (!_isTouching) return;
            _touchDuration += Time.deltaTime;

            float dist = Vector2.Distance(_currentPos, _startPos);

            if (!_gestureCommitted)
            {
                if (dist >= MOVE_START_DISTANCE && _touchDuration >= SWIPE_MAX_DURATION)
                {
                    _gestureCommitted = true;
                    CurrentState      = InputState.Drag;
                    IsMoving          = true;
                    Debug.Log("[Input] MOVE 시작");
                }
                else if (dist < TAP_MAX_DISTANCE && _touchDuration >= HOLD_MIN_DURATION)
                {
                    _gestureCommitted = true;
                    CurrentState      = InputState.Hold;
                    Debug.Log("[Input] HOLD 시작");
                }
            }

            if (CurrentState == InputState.Drag)
            {
                JoystickOrigin  = _startPos;
                JoystickCurrent = _currentPos;
                OnMoveDirection?.Invoke((_currentPos - _startPos).normalized);
            }

            if (CurrentState == InputState.Hold)
                OnHold?.Invoke(_touchDuration);
        }

        // ── 터치 핸들러 ──────────────────────────────────────────
private void HandleFingerDown(Finger finger)
        {
            if (_isTouching) return;

            // UI 위 터치는 게임 입력으로 처리하지 않음
            if (IsPointerOverUI(finger.screenPosition)) return;

            var pos = finger.screenPosition;

            _isTouching       = true;
            _startPos         = pos;
            _currentPos       = pos;
            _touchDuration    = 0f;
            _gestureCommitted = false;
            CurrentState      = InputState.None;
            IsMoving          = false;
        }

        private void HandleFingerMove(Finger finger)
        {
            if (!_isTouching) return;
            _currentPos = finger.screenPosition;
        }

        private void HandleFingerUp(Finger finger)
        {
            if (!_isTouching) return;

            var   endPos = finger.screenPosition;
            float dist   = Vector2.Distance(endPos, _startPos);
            var   dir    = dist > 0.01f ? (endPos - _startPos).normalized : Vector2.up;
            var   last   = CurrentState;

            if (IsMoving)
            {
                IsMoving = false;
                OnMoveEnd?.Invoke();
            }
            else if (dist >= SWIPE_MIN_DISTANCE && _touchDuration < SWIPE_MAX_DURATION)
            {
                if (_attackWindowCount > 0)
                {
                    CurrentState = InputState.JustDodge;
                    Debug.Log("[Input] JUST DODGE 판정");
                    OnJustDodge?.Invoke(dir);
                }
                else
                {
                    CurrentState = InputState.Swipe;
                    string dirLabel = Mathf.Abs(dir.x) > Mathf.Abs(dir.y)
                        ? (dir.x > 0 ? "→" : "←")
                        : (dir.y > 0 ? "↑" : "↓");
                    Debug.Log($"[Input] SWIPE {dirLabel}");
                    OnSwipe?.Invoke(dir);
                }
            }
            else if (dist < TAP_MAX_DISTANCE && _touchDuration < TAP_MAX_DURATION)
            {
                CurrentState = InputState.Tap;
                Debug.Log("[Input] TAP");
                OnTap?.Invoke(_startPos);
            }

            OnRelease?.Invoke(last);
            ResetState();
        }

        // ── 외부 API ─────────────────────────────────────────────

        /// <summary>
        /// 게임 로직에서 저스트 회피를 직접 발동할 때 호출하는 정식 API.
        /// 회피 중 피격 등 코드 기반 트리거에서 사용.
        /// </summary>
        public void TriggerJustDodge(Vector2 direction)
        {
            CurrentState = InputState.JustDodge;
            OnJustDodge?.Invoke(direction);
        }

        public void OpenAttackWindow()  => _attackWindowCount++;
        public void CloseAttackWindow() => _attackWindowCount = Mathf.Max(0, _attackWindowCount - 1);
        public bool IsAttackWindowOpen  => _attackWindowCount > 0;

        // ── 내부 초기화 ──────────────────────────────────────────
        // ── UI 필터링 ──────────────────────────────────────────
        /// <summary>
        /// 터치 위치가 UI 위인지 확인. UI 위면 게임 입력 무시.
        /// New Input System + EventSystem 환경에서 동작.
        /// </summary>
        private static bool IsPointerOverUI(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;

            // New Input System: PointerEventData로 레이캐스트
            var eventData = new UnityEngine.EventSystems.PointerEventData(EventSystem.current)
            {
                position = screenPos
            };
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        
private void ResetState()
        {
            _isTouching       = false;
            _touchDuration    = 0f;
            _gestureCommitted = false;
            JoystickOrigin    = Vector2.zero;
            JoystickCurrent   = Vector2.zero;
            CurrentState      = InputState.None;
        }
    }
}
