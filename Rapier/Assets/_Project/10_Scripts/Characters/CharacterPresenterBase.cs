using UnityEngine;
using Game.Core;
using Game.Input;
using Game.Combat;
using Game.Enemies;

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

        // ── 이동/대시 상태 ─────────────────────────────────────────────
        private float   _holdDuration;
        private bool    _isCharging;
        private Vector2 _moveDirection;      // 현재 조이스틱 방향
        private bool    _isDashing;
        private Vector2 _dashTarget;
        private float   _dashSpeed;

        // ── 초기화 ────────────────────────────────────────────────
protected void Init(CharacterStatData statData, ICharacterView view)
        {
            Model = new CharacterModel(statData);
            View  = view;

            Model.OnHpChanged     += ratio => View.UpdateHpGauge(ratio / statData.maxHp);
            Model.OnDeath         += HandleDeath;
            Model.OnChargeChanged += View.UpdateChargeGauge;

            // GestureRecognizer는 Start()에서 가져옴 (모든 Awake 완료 후)
        }

        // ── 이벤트 구독 / 해제 ────────────────────────────────────
protected virtual void OnEnable() { }
        // 실제 구독은 Start()에서 수행

protected virtual void OnDisable()
        {
            if (Gesture == null) return;
            Gesture.OnTap           -= HandleTap;
            Gesture.OnSwipe         -= HandleSwipe;
            Gesture.OnMoveDirection -= HandleMoveDirection;
            Gesture.OnMoveEnd       -= HandleMoveEnd;
            Gesture.OnHold          -= HandleHold;
            Gesture.OnRelease       -= HandleRelease;
            Gesture.OnJustDodge     -= HandleJustDodge;
        }

        // ── 공통 입력 처리 ────────────────────────────────────────
private void HandleTap(Vector2 screenPos)
        {
            if (!Model.IsAlive) return;
            View.PlayAttack();
            PerformAttack();
            OnTap(screenPos);
        }

        /// <summary>
        /// 가장 가까운 적을 향해 사각형 범위 판정.
        /// attackWidth, attackHeight, attackOffset 은 CharacterStatData에서 조정.
        /// </summary>
private void PerformAttack()
        {
            var waveManager = ServiceLocator.Get<WaveManager>();
            if (waveManager == null) return;

            var stat    = Model.StatData;
            var nearest = waveManager.GetNearestEnemy(transform.position);

            var dir = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            var boxCenter  = (Vector2)transform.position + dir * stat.attackOffset;
            var boxSize    = new Vector2(stat.attackWidth, stat.attackHeight);
            float angle    = Vector2.SignedAngle(Vector2.up, dir);
            int enemyLayer = LayerMask.GetMask("Enemy");

            var hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, enemyLayer);
            int hitCount = 0;
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                damageable.TakeDamage(stat.attackPower, dir);
                hitCount++;
            }
            Debug.Log($"[Attack] 히트: {hitCount}명 / 범위 중심: {boxCenter} / 크기: {boxSize}");
        }

private void HandleSwipe(Vector2 direction)
        {
            if (!Model.IsAlive) return;

            // 대시: Swipe 방향으로 dashDistance만큼 빠르게 이동
            var stat    = Model.StatData;
            _dashTarget = (Vector2)transform.position + direction * stat.dashDistance;
            _dashSpeed  = stat.dashSpeed;

            var stage = ServiceLocator.Get<StageBuilder>();
            if (stage != null) _dashTarget = stage.ClampToStage(_dashTarget);

            _isDashing     = true;
            _moveDirection = Vector2.zero;

            View.PlayDodge(direction);
            OnSwipe(direction);
        }

private void HandleMoveDirection(Vector2 dir)
        {
            if (!Model.IsAlive || _isDashing) return;
            _moveDirection = dir;
        }

        private void HandleMoveEnd()
        {
            _moveDirection = Vector2.zero;
        }

        // Update로 이동 처리 (매 프레임 일정 속도 유지)
        protected virtual void Update()
        {
            if (!Model.IsAlive) return;

            if (_isDashing)
            {
                var next = Vector2.MoveTowards(
                    transform.position, _dashTarget, _dashSpeed * Time.deltaTime);
                View.MoveTo(next);
                if (Vector2.Distance(next, _dashTarget) < 0.05f)
                    _isDashing = false;
                return;
            }

            if (_moveDirection.sqrMagnitude > 0.01f)
            {
                var worldDelta = _moveDirection * (Model.StatData.moveSpeed * Time.deltaTime);
                var nextPos    = (Vector2)transform.position + worldDelta;

                var stage = ServiceLocator.Get<StageBuilder>();
                if (stage != null) nextPos = stage.ClampToStage(nextPos);

                View.MoveTo(nextPos);
            }
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
    

protected virtual void Start()
        {
            // 모든 Awake 완료 후 GestureRecognizer 구독
            Gesture = ServiceLocator.Get<GestureRecognizer>();
            if (Gesture == null)
            {
                Debug.LogError($"[{GetType().Name}] GestureRecognizer가 ServiceLocator에 없음. InputSystemInitializer가 먼저 Awake되어야 함.");
                return;
            }
            Gesture.OnTap           += HandleTap;
            Gesture.OnSwipe         += HandleSwipe;
            Gesture.OnMoveDirection += HandleMoveDirection;
            Gesture.OnMoveEnd       += HandleMoveEnd;
            Gesture.OnHold          += HandleHold;
            Gesture.OnRelease       += HandleRelease;
            Gesture.OnJustDodge     += HandleJustDodge;
        }
}
}
