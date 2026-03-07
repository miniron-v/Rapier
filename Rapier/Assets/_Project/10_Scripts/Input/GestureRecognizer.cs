using System;
using UnityEngine;
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
    ///                   → Swipe (또는 JustDodge)
    ///                 - dist < TAP_MAX_DISTANCE AND duration < TAP_MAX_DURATION
    ///                   → Tap
    ///                 - 그 외 Hold/Move → Release만
    ///
    /// [유효 영역] 화면 하단 40%
    /// </summary>
    public class GestureRecognizer : MonoBehaviour
    {
        // ── 판별 기준 상수 ───────────────────────────────────────
        private const float VALID_AREA_RATIO    = 0.4f;
        private const float TAP_MAX_DISTANCE    = 20f;   // px
        private const float TAP_MAX_DURATION    = 0.2f;  // 초
        private const float SWIPE_MIN_DISTANCE  = 60f;   // px  (빠른 플릭 최소 거리)
        private const float SWIPE_MAX_DURATION  = 0.25f; // 초  (이 시간 이상 = Move)
        private const float MOVE_START_DISTANCE = 20f;   // px  (조이스틱 활성화 거리)
        private const float HOLD_MIN_DURATION   = 0.3f;  // 초

        // ── 이벤트 ───────────────────────────────────────────────
        public event Action<Vector2>    OnTap;
        public event Action<Vector2>    OnSwipe;
        public event Action<Vector2>    OnMoveDirection; // Move 중 매 프레임
        public event Action             OnMoveEnd;
        public event Action<float>      OnHold;          // Hold 중 매 프레임
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
        private bool    _gestureCommitted;  // Move 또는 Hold 확정 여부
        private int  _attackWindowCount;

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
                // Move 확정: 거리 + 시간 모두 충족 (Swipe와 구분 핵심)
                if (dist >= MOVE_START_DISTANCE && _touchDuration >= SWIPE_MAX_DURATION)
                {
                    _gestureCommitted = true;
                    CurrentState      = InputState.Drag;
                    IsMoving          = true;
                }
                // Hold 확정: 정지 + 시간 충족
                else if (dist < TAP_MAX_DISTANCE && _touchDuration >= HOLD_MIN_DURATION)
                {
                    _gestureCommitted = true;
                    CurrentState      = InputState.Hold;
                }
            }

            // Move: 조이스틱 방향 매 프레임 발행
            if (CurrentState == InputState.Drag)
            {
                JoystickOrigin  = _startPos;
                JoystickCurrent = _currentPos;
                OnMoveDirection?.Invoke((_currentPos - _startPos).normalized);
            }

            // Hold: 지속시간 매 프레임 발행
            if (CurrentState == InputState.Hold)
                OnHold?.Invoke(_touchDuration);
        }

        // ── 터치 핸들러 ──────────────────────────────────────────
        private void HandleFingerDown(Finger finger)
        {
            if (_isTouching) return;
            var pos = finger.screenPosition;
            if (pos.y > Screen.height * VALID_AREA_RATIO) return;

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

            var endPos = finger.screenPosition;
            float dist = Vector2.Distance(endPos, _startPos);
            var dir    = dist > 0.01f ? (endPos - _startPos).normalized : Vector2.up;
            var last   = CurrentState;

            if (IsMoving)
            {
                // Move 종료
                IsMoving = false;
                OnMoveEnd?.Invoke();
            }
            else if (dist >= SWIPE_MIN_DISTANCE && _touchDuration < SWIPE_MAX_DURATION)
            {
                // Swipe: 빠른 플릭 (Move 확정 전에 뗀 경우)
                if (_attackWindowCount > 0)
                {
                    CurrentState = InputState.JustDodge;
                    OnJustDodge?.Invoke(dir);
                }
                else
                {
                    CurrentState = InputState.Swipe;
                    OnSwipe?.Invoke(dir);
                }
                {
                    CurrentState = InputState.Swipe;
                    OnSwipe?.Invoke(dir);
                }
            }
            else if (dist < TAP_MAX_DISTANCE && _touchDuration < TAP_MAX_DURATION)
            {
                // Tap
                CurrentState = InputState.Tap;
                OnTap?.Invoke(_startPos);
            }
            // Hold / 미확정 → Release만 발행

            OnRelease?.Invoke(last);
            ResetState();
        }

        // ── 외부 API ─────────────────────────────────────────────
        public void OpenAttackWindow()  => _attackWindowCount++;
        public void CloseAttackWindow() => _attackWindowCount = Mathf.Max(0, _attackWindowCount - 1);
        public bool IsAttackWindowOpen  => _attackWindowCount > 0;

        // ── 내부 초기화 ──────────────────────────────────────────
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
