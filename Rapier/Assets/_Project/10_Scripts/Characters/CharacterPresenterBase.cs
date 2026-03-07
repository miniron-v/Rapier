using UnityEngine;
using Game.Core;
using Game.Input;

namespace Game.Characters
{
    /// <summary>
    /// 모든 플레이어 캐릭터 Presenter의 추상 베이스.
    ///
    /// [책임]
    ///   - ServiceLocator에서 GestureRecognizer를 가져와 이벤트 구독
    ///   - 공통 입력(Tap/Swipe/Drag/Hold/JustDodge)을 처리
    ///   - 캐릭터별 고유 로직은 자식 클래스에서 override
    ///
    /// [Init 규칙]
    ///   - 자식 클래스는 Awake에서 base.Init(statData, view)를 반드시 호출
    /// </summary>
    [RequireComponent(typeof(CharacterView))]
    public abstract class CharacterPresenterBase : MonoBehaviour
    {
        // ── 내부 참조 ─────────────────────────────────────────────
        protected CharacterModel     Model  { get; private set; }
        protected ICharacterView     View   { get; private set; }
        protected GestureRecognizer  Gesture { get; private set; }

        // ── 차지 상태 ─────────────────────────────────────────────
        private float _holdDuration;
        private bool  _isCharging;

        // ── 초기화 ────────────────────────────────────────────────
        protected void Init(CharacterStatData statData, ICharacterView view)
        {
            Model = new CharacterModel(statData);
            View  = view;

            // 모델 이벤트 → View 연결
            Model.OnHpChanged     += ratio => View.UpdateHpGauge(ratio / statData.maxHp);
            Model.OnDeath         += HandleDeath;
            Model.OnChargeChanged += View.UpdateChargeGauge;

            // ServiceLocator에서 GestureRecognizer 획득
            Gesture = ServiceLocator.Get<GestureRecognizer>();
            if (Gesture == null)
                Debug.LogError($"[{GetType().Name}] GestureRecognizer가 ServiceLocator에 등록되지 않음.");
        }

        // ── 이벤트 구독 / 해제 ────────────────────────────────────
        protected virtual void OnEnable()
        {
            if (Gesture == null) return;
            Gesture.OnTap       += HandleTap;
            Gesture.OnSwipe     += HandleSwipe;
            Gesture.OnDragDelta += HandleDrag;
            Gesture.OnHold      += HandleHold;
            Gesture.OnRelease   += HandleRelease;
            Gesture.OnJustDodge += HandleJustDodge;
        }

        protected virtual void OnDisable()
        {
            if (Gesture == null) return;
            Gesture.OnTap       -= HandleTap;
            Gesture.OnSwipe     -= HandleSwipe;
            Gesture.OnDragDelta -= HandleDrag;
            Gesture.OnHold      -= HandleHold;
            Gesture.OnRelease   -= HandleRelease;
            Gesture.OnJustDodge -= HandleJustDodge;
        }

        // ── 공통 입력 처리 ────────────────────────────────────────
        private void HandleTap(Vector2 screenPos)
        {
            if (!Model.IsAlive) return;
            View.PlayAttack();
            OnTap(screenPos);
        }

        private void HandleSwipe(Vector2 direction)
        {
            if (!Model.IsAlive) return;
            View.PlayDodge(direction);
            OnSwipe(direction);
        }

        private void HandleDrag(Vector2 delta)
        {
            if (!Model.IsAlive) return;

            // 스크린 델타 → 월드 이동량으로 변환
            var worldDelta = delta * (Model.StatData.moveSpeed * Time.deltaTime);
            var nextPos    = (Vector2)transform.position + worldDelta;
            View.MoveTo(nextPos);
            OnDrag(delta);
        }

        private void HandleHold(float duration)
        {
            if (!Model.IsAlive) return;
            _holdDuration = duration;
            _isCharging   = true;

            float ratio = Mathf.Clamp01(duration / Model.StatData.chargeRequiredTime);
            Model.SetChargeRatio(ratio);
            OnHold(duration);
        }

        private void HandleRelease(InputState lastState)
        {
            if (!Model.IsAlive) return;

            bool fullyCharged = _isCharging &&
                                _holdDuration >= Model.StatData.chargeRequiredTime;

            if (fullyCharged || Model.IsJustDodgeReady)
                OnSkillRelease(fullyCharged, Model.IsJustDodgeReady);

            _holdDuration = 0f;
            _isCharging   = false;
            Model.SetChargeRatio(0f);
            Model.SetJustDodgeReady(false);
            OnRelease(lastState);
        }

        private void HandleJustDodge(Vector2 direction)
        {
            if (!Model.IsAlive) return;
            View.PlayDodge(direction);
            Model.SetJustDodgeReady(true);
            OnJustDodge(direction);
        }

        private void HandleDeath()
        {
            View.PlayDeath();
            OnDisable(); // 이벤트 구독 해제
        }

        // ── 자식 클래스 override 지점 (선택적) ───────────────────
        /// <summary>Tap 후 캐릭터별 추가 처리.</summary>
        protected virtual void OnTap(Vector2 screenPos) { }

        /// <summary>Swipe 후 캐릭터별 추가 처리 (일반 회피).</summary>
        protected virtual void OnSwipe(Vector2 direction) { }

        /// <summary>Drag 중 캐릭터별 추가 처리.</summary>
        protected virtual void OnDrag(Vector2 delta) { }

        /// <summary>Hold 중 캐릭터별 추가 처리.</summary>
        protected virtual void OnHold(float duration) { }

        /// <summary>
        /// Release 시 스킬 발동 지점.
        /// fullyCharged: 차지 완충 여부 / justDodgeReady: 저스트 회피 슬로우 상태 여부.
        /// </summary>
        protected virtual void OnSkillRelease(bool fullyCharged, bool justDodgeReady) { }

        /// <summary>저스트 회피 후 캐릭터별 추가 처리.</summary>
        protected virtual void OnJustDodge(Vector2 direction) { }

        /// <summary>Release 후 공통 정리 이후 캐릭터별 후처리.</summary>
        protected virtual void OnRelease(InputState lastState) { }
    }
}
