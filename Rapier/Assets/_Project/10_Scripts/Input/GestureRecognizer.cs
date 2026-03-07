using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Game.Input
{
    /// <summary>
    /// 터치 입력을 Tap / Swipe / Drag / Hold / JustDodge 로 판별해 발행한다.
    ///
    /// [판별 기준]
    ///   Tap      : 거리 < 20px, 지속 < 0.2초
    ///   Swipe    : 거리 >= 20px, 지속 < 0.3초
    ///   Drag     : 거리 >= 20px, 지속 >= 0.3초  (매 프레임 Delta 발행)
    ///   Hold     : 정지 상태, 지속 >= 0.3초      (매 프레임 발행)
    ///   JustDodge: 외부에서 MarkEnemyAttackWindow() 호출 중 Swipe 입력 시
    ///
    /// [유효 영역]
    ///   화면 하단 40% 이내에서 시작된 터치만 처리한다.
    ///
    /// [이벤트]
    ///   OnTap       (Vector2 screenPos)
    ///   OnSwipe     (Vector2 direction)
    ///   OnDragDelta (Vector2 delta)
    ///   OnHold      (float duration)
    ///   OnRelease   (InputState lastState)
    ///   OnJustDodge (Vector2 direction)
    /// </summary>
    public class GestureRecognizer : MonoBehaviour
    {
        // ── 판별 기준 상수 ──────────────────────────────────────────
        private const float TAP_MAX_DISTANCE  = 20f;  // px
        private const float TAP_MAX_DURATION  = 0.2f; // 초
        private const float SWIPE_MAX_DURATION = 0.3f; // 초
        private const float HOLD_MIN_DURATION  = 0.3f; // 초
        private const float DRAG_MIN_DISTANCE  = 20f;  // px
        private const float VALID_AREA_RATIO   = 0.4f; // 화면 하단 40%

        // ── 이벤트 ──────────────────────────────────────────────────
        public event Action<Vector2> OnTap;
        public event Action<Vector2> OnSwipe;
        public event Action<Vector2> OnDragDelta;
        public event Action<float>   OnHold;
        public event Action<InputState> OnRelease;
        public event Action<Vector2> OnJustDodge;

        // ── 현재 상태 (읽기 전용 공개) ─────────────────────────────
        public InputState CurrentState { get; private set; } = InputState.None;

        // ── 내부 상태 ────────────────────────────────────────────────
        private bool    _isTouching;
        private Vector2 _startPos;
        private Vector2 _prevPos;
        private float   _touchDuration;
        private bool    _enemyAttackWindow; // 저스트 회피 판정 가능 여부

        // ── 라이프사이클 ─────────────────────────────────────────────
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

            // Hold 판정: 정지 상태에서 일정 시간 이상
            if (CurrentState == InputState.Hold || IsStationary())
            {
                if (_touchDuration >= HOLD_MIN_DURATION)
                {
                    CurrentState = InputState.Hold;
                    OnHold?.Invoke(_touchDuration);
                }
            }
        }

        // ── 터치 이벤트 핸들러 ───────────────────────────────────────
        private void HandleFingerDown(Finger finger)
        {
            // 멀티 터치 무시 (단일 손가락만)
            if (_isTouching) return;

            var pos = finger.screenPosition;

            // 유효 영역 검사: 화면 하단 40%
            if (pos.y > Screen.height * VALID_AREA_RATIO) return;

            _isTouching    = true;
            _startPos      = pos;
            _prevPos       = pos;
            _touchDuration = 0f;
            CurrentState   = InputState.None;
        }

        private void HandleFingerMove(Finger finger)
        {
            if (!_isTouching) return;

            var pos   = finger.screenPosition;
            var delta = pos - _prevPos;
            _prevPos  = pos;

            float totalDist = Vector2.Distance(pos, _startPos);

            // Drag 판정: 거리 조건 충족 + 지속 시간 >= 0.3초
            if (totalDist >= DRAG_MIN_DISTANCE && _touchDuration >= SWIPE_MAX_DURATION)
            {
                CurrentState = InputState.Drag;
                OnDragDelta?.Invoke(delta);
            }
        }

        private void HandleFingerUp(Finger finger)
        {
            if (!_isTouching) return;

            var endPos   = finger.screenPosition;
            float dist   = Vector2.Distance(endPos, _startPos);
            var dir      = (endPos - _startPos).normalized;
            var lastState = CurrentState;

            // ── 제스처 최종 판별 ──
            if (dist < TAP_MAX_DISTANCE && _touchDuration < TAP_MAX_DURATION)
            {
                // Tap
                CurrentState = InputState.Tap;
                OnTap?.Invoke(_startPos);
            }
            else if (dist >= DRAG_MIN_DISTANCE && _touchDuration < SWIPE_MAX_DURATION)
            {
                // Swipe (혹은 JustDodge)
                if (_enemyAttackWindow)
                {
                    CurrentState = InputState.JustDodge;
                    OnJustDodge?.Invoke(dir);
                }
                else
                {
                    CurrentState = InputState.Swipe;
                    OnSwipe?.Invoke(dir);
                }
            }
            // Hold / Drag 는 Up 시 별도 판정 없이 Release만 알림

            OnRelease?.Invoke(lastState);
            ResetState();
        }

        // ── 외부 API ─────────────────────────────────────────────────
        /// <summary>
        /// 적 공격 판정 윈도우 시작/종료를 알린다.
        /// EnemyPresenter가 공격 직전 true, 이후 false로 호출한다.
        /// </summary>
        public void MarkEnemyAttackWindow(bool isOpen)
        {
            _enemyAttackWindow = isOpen;
        }

        // ── 내부 유틸 ────────────────────────────────────────────────
        private bool IsStationary()
        {
            return Vector2.Distance(_prevPos, _startPos) < TAP_MAX_DISTANCE;
        }

        private void ResetState()
        {
            _isTouching    = false;
            _touchDuration = 0f;
            CurrentState   = InputState.None;
        }
    }
}
