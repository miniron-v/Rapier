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
        protected CharacterModel     Model   { get; private set; }
        protected ICharacterView     View    { get; private set; }
        protected GestureRecognizer  Gesture { get; private set; }

        // ── 이동/대시 상태 ─────────────────────────────────────────────
        private float   _holdDuration;
        private bool    _isCharging;
        private Vector2 _moveDirection;
        private bool    _isDashing;
        private Vector2 _dashTarget;
        private float   _dashSpeed;

        // ── 공격 Gizmo 상태 (시각화용) ────────────────────────────
        private Vector2 _lastAttackCenter;
        private Vector2 _lastAttackSize;
        private float   _lastAttackAngle;
        private bool    _showAttackGizmo;
        private float   _gizmoTimer;
        private const float GizmoDuration = 0.5f;

        // ── 초기화 ────────────────────────────────────────────────
        protected void Init(CharacterStatData statData, ICharacterView view)
        {
            Model = new CharacterModel(statData);
            View  = view;

            Model.OnHpChanged     += ratio => View.UpdateHpGauge(ratio / statData.maxHp);
            Model.OnDeath         += HandleDeath;
            Model.OnChargeChanged += View.UpdateChargeGauge;
        }

        // ── 이벤트 구독 / 해제 ────────────────────────────────────
        protected virtual void OnEnable() { }

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

            var boxCenter = (Vector2)transform.position + dir * stat.attackOffset;
            var boxSize   = new Vector2(stat.attackWidth, stat.attackHeight);
            float angle   = Vector2.SignedAngle(Vector2.up, dir);
            int enemyLayer = LayerMask.GetMask("Enemy");

            // ── Gizmo 갱신 ──────────────────────────────────────
            _lastAttackCenter = boxCenter;
            _lastAttackSize   = boxSize;
            _lastAttackAngle  = angle;
            _showAttackGizmo  = true;
            _gizmoTimer       = GizmoDuration;

            Debug.Log($"[Attack] dir={dir} | center={boxCenter} | size={boxSize} | angle={angle:F1}° | layer={enemyLayer}");

            var hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, enemyLayer);
            int hitCount = 0;
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                damageable.TakeDamage(stat.attackPower, dir);
                hitCount++;
            }
            Debug.Log($"[Attack] 히트: {hitCount}명 / 적 오브젝트 수: {hits.Length}");
        }

        private void HandleSwipe(Vector2 direction)
        {
            if (!Model.IsAlive) return;

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

        protected virtual void Update()
        {
            if (!Model.IsAlive) return;

            // Gizmo 타이머
            if (_showAttackGizmo)
            {
                _gizmoTimer -= Time.deltaTime;
                if (_gizmoTimer <= 0f) _showAttackGizmo = false;
            }

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
            OnDisable();
        }

        // ── Gizmo 시각화 ──────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (!_showAttackGizmo) return;

            // 공격 범위 박스 (노란색)
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            var rot = Quaternion.Euler(0f, 0f, _lastAttackAngle);
            var oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(
                new Vector3(_lastAttackCenter.x, _lastAttackCenter.y, 0f),
                rot,
                Vector3.one);
            Gizmos.DrawCube(Vector3.zero, new Vector3(_lastAttackSize.x, _lastAttackSize.y, 0.1f));

            // 외곽선 (진한 노란색)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_lastAttackSize.x, _lastAttackSize.y, 0.1f));
            Gizmos.matrix = oldMatrix;

            // 중심점 (빨간색 십자)
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(new Vector3(_lastAttackCenter.x, _lastAttackCenter.y, 0f), 0.1f);
        }

        // ── 자식 클래스 override 지점 (선택적) ───────────────────
        protected virtual void OnTap(Vector2 screenPos)        { }
        protected virtual void OnSwipe(Vector2 direction)      { }
        protected virtual void OnDrag(Vector2 delta)           { }
        protected virtual void OnHold(float duration)          { }
        protected virtual void OnSkillRelease(bool fullyCharged, bool justDodgeReady) { }
        protected virtual void OnJustDodge(Vector2 direction)  { }
        protected virtual void OnRelease(InputState lastState) { }

        protected virtual void Start()
        {
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
