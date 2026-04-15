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
    ///                 - dist >= SWIPE_MIN_DISTANCE AND duration < SWIPE_MAX_DURATION → Swipe
    ///                 - dist < TAP_MAX_DISTANCE AND duration < TAP_MAX_DURATION → Tap
    ///
    /// 전투 도메인 판단(JustDodge 발동 여부, 차지 풀 여부 등)은 Presenter가 담당한다.
    /// GestureRecognizer 는 입력 인식과 이벤트 발행만 수행한다.
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

        /// <summary>
        /// Hold 성립 후 매 프레임 발행. 시작점 대비 손가락 변위(raw, 정규화 안 함).
        /// </summary>
        public event Action<Vector2> OnHoldDragUpdate;

        /// <summary>
        /// Hold 중 SWIPE_MIN_DISTANCE 이상 이동 + SWIPE_MAX_DURATION 이내 완료 시 단발 발행.
        /// 발행 후 해당 터치는 소비됨(OnHoldRelease 차단).
        /// </summary>
        public event Action<Vector2> OnHoldSwipe;

        /// <summary>
        /// Hold 중 손가락을 뗄 때 발행. OnHoldSwipe 가 이미 발행된 경우에는 발행하지 않음.
        /// </summary>
        public event Action<Vector2> OnHoldRelease;

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

        // Hold 확장 이벤트용 내부 상태
        private bool    _holdConsumed;       // OnHoldSwipe 발행 시 true → OnHoldRelease 차단
        private Vector2 _holdSwipeStartPos;  // Swipe 윈도우 시작 위치
        private float   _holdSwipeStartTime; // Swipe 윈도우 시작 시각

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
                }
                else if (dist < TAP_MAX_DISTANCE && _touchDuration >= HOLD_MIN_DURATION)
                {
                    _gestureCommitted    = true;
                    CurrentState         = InputState.Hold;
                    // Hold 성립 시 Swipe 윈도우 초기화
                    _holdSwipeStartPos   = _currentPos;
                    _holdSwipeStartTime  = _touchDuration;
                    _holdConsumed        = false;
                }
            }

            if (CurrentState == InputState.Drag)
            {
                JoystickOrigin  = _startPos;
                JoystickCurrent = _currentPos;
                OnMoveDirection?.Invoke((_currentPos - _startPos).normalized);
            }

            if (CurrentState == InputState.Hold)
            {
                OnHold?.Invoke(_touchDuration);

                // Hold 확장 이벤트: 매 프레임 변위 발행
                OnHoldDragUpdate?.Invoke(_currentPos - _startPos);

                // Hold 중 Swipe 윈도우 갱신: 빠른 이동이 너무 느려진 경우 윈도우 리셋만 수행.
                // 실제 OnHoldSwipe 발행은 손가락을 뗄 때(HandleFingerUp) 수행한다.
                if (!_holdConsumed)
                {
                    float holdSwipeDist = Vector2.Distance(_currentPos, _holdSwipeStartPos);
                    if (holdSwipeDist >= SWIPE_MIN_DISTANCE)
                    {
                        float elapsed = _touchDuration - _holdSwipeStartTime;
                        if (elapsed > SWIPE_MAX_DURATION)
                        {
                            // 너무 느리게 이동: 윈도우 리셋
                            _holdSwipeStartPos  = _currentPos;
                            _holdSwipeStartTime = _touchDuration;
                        }
                        // elapsed <= SWIPE_MAX_DURATION 인 경우는 FingerUp 때 발행
                    }
                }
            }
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
            else if (last == InputState.Hold)
            {
                // Hold 상태에서 손가락을 뗀 경우
                if (!_holdConsumed)
                {
                    // FingerUp 시점에 Swipe 윈도우 조건 재검사
                    float holdSwipeDist = Vector2.Distance(endPos, _holdSwipeStartPos);
                    float holdSwipeElapsed = _touchDuration - _holdSwipeStartTime;
                    if (holdSwipeDist >= SWIPE_MIN_DISTANCE && holdSwipeElapsed <= SWIPE_MAX_DURATION)
                    {
                        // Swipe 성립: OnHoldSwipe 발행 + OnHoldRelease 차단
                        var swipeDir = (endPos - _holdSwipeStartPos).normalized;
                        _holdConsumed = true;
                        OnHoldSwipe?.Invoke(swipeDir);
                    }
                    else
                    {
                        // Swipe 없음: Release 발행
                        OnHoldRelease?.Invoke(_currentPos - _startPos);
                    }
                }
                // _holdConsumed == true 면 이미 처리됨 → 양쪽 모두 차단
            }
            else if (dist >= SWIPE_MIN_DISTANCE && _touchDuration < SWIPE_MAX_DURATION)
            {
                CurrentState = InputState.Swipe;
                OnSwipe?.Invoke(dir);
            }
            else if (dist < TAP_MAX_DISTANCE && _touchDuration < TAP_MAX_DURATION)
            {
                CurrentState = InputState.Tap;
                OnTap?.Invoke(_startPos);
            }

            OnRelease?.Invoke(last);
            ResetState();
        }


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
            _isTouching         = false;
            _touchDuration      = 0f;
            _gestureCommitted   = false;
            JoystickOrigin      = Vector2.zero;
            JoystickCurrent     = Vector2.zero;
            CurrentState        = InputState.None;
            // Hold 확장 상태 초기화
            _holdConsumed       = false;
            _holdSwipeStartPos  = Vector2.zero;
            _holdSwipeStartTime = 0f;
        }
    }
}
