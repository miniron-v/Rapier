using System.Collections;
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
    ///   - Swipe 시 무적 0.2초 + 회피 쿨타임 (dodgeCooldown 초, unscaledTime 기반)
    ///   - 일반 공격 0.5초 딜레이 + 연타 차단
    ///   - 공격 범위 인게임 가시화
    ///   - 저스트 회피 슬로우모션 + 카메라 줌 펀치
    ///   - 캐릭터별 고유 로직은 자식 클래스에서 override
    ///
    /// [Init 규칙]
    ///   - 자식 클래스는 Awake에서 base.Init(statData, view)를 반드시 호출
    /// </summary>
    [RequireComponent(typeof(CharacterView))]
    public abstract class CharacterPresenterBase : MonoBehaviour
    {
        // ── 슬로우모션 설정 ───────────────────────────────────────
        [Header("Just Dodge Slow Motion")]
        [SerializeField] private AnimationCurve slowCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.50f, 0.15f),
            new Keyframe(0.75f, 0.05f),
            new Keyframe(1.00f, 1.00f)
        );
        [SerializeField] private float slowDuration = 3f;

        // ── 상수 ──────────────────────────────────────────────────
        private const float INVINCIBLE_DURATION = 0.2f;
        private const float ATTACK_DELAY        = 0.5f;

        // ── 내부 참조 ─────────────────────────────────────────────
        protected CharacterModel    Model   { get; private set; }
        protected ICharacterView    View    { get; private set; }
        protected GestureRecognizer Gesture { get; private set; }

        // ── 이동/대시 상태 ────────────────────────────────────────
        private float   _holdDuration;
        private bool    _isCharging;
        private Vector2 _moveDirection;
        private bool    _isDashing;
        private Vector2 _dashTarget;
        private float   _dashSpeed;

        // ── 공격 상태 ─────────────────────────────────────────────
        private bool _isAttacking;

        // ── 회피 쿨타임 ───────────────────────────────────────────
        private float _dodgeCooldownTimer; // 0이면 사용 가능

        // ── 슬로우모션 상태 ───────────────────────────────────────
        private Coroutine _slowCoroutine;

        // ── 공격 범위 인게임 가시화 ───────────────────────────────
        private GameObject     _attackRangeIndicator;
        private SpriteRenderer _attackRangeSr;

        // ── Gizmo 상태 (에디터 전용) ──────────────────────────────
        private Vector2     _lastAttackCenter;
        private Vector2     _lastAttackSize;
        private float       _lastAttackAngle;
        private bool        _showAttackGizmo;
        private float       _gizmoTimer;
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

            StopSlowMotion();
        }

        // ── 공통 입력 처리 ────────────────────────────────────────

        private void HandleTap(Vector2 screenPos)
        {
            if (!Model.IsAlive) return;
            if (_isAttacking) return;

            View.PlayAttack();
            StartCoroutine(AttackRoutine());
            OnTap(screenPos);
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            ShowAttackRangeIndicator();

            yield return new WaitForSecondsRealtime(ATTACK_DELAY);

            HideAttackRangeIndicator();
            if (Model.IsAlive) PerformAttack();
            _isAttacking = false;
        }

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

            _lastAttackCenter = boxCenter;
            _lastAttackSize   = boxSize;
            _lastAttackAngle  = angle;
            _showAttackGizmo  = true;
            _gizmoTimer       = GizmoDuration;

            var hits     = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, enemyLayer);
            int hitCount = 0;
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                damageable.TakeDamage(stat.attackPower, dir);
                hitCount++;
            }
            Debug.Log($"[Attack] 히트: {hitCount}명 / 범위 내 오브젝트: {hits.Length}");
        }

        private void HandleSwipe(Vector2 direction)
        {
            if (!Model.IsAlive) return;
            if (_dodgeCooldownTimer > 0f) return; // 쿨타임 중 차단

            var stat    = Model.StatData;
            _dashTarget = (Vector2)transform.position + direction * stat.dashDistance;
            _dashSpeed  = stat.dashSpeed;

            var stage = ServiceLocator.Get<StageBuilder>();
            if (stage != null) _dashTarget = stage.ClampToStage(_dashTarget);

            _isDashing     = true;
            _moveDirection = Vector2.zero;

            StartCoroutine(InvincibleRoutine());
            StartCoroutine(DodgeCooldownRoutine());

            View.PlayDodge(direction);
            OnSwipe(direction);
        }

        /// <summary>회피 시 무적 0.2초 (unscaledTime 기반).</summary>
        private IEnumerator InvincibleRoutine()
        {
            Model.SetInvincible(true);
            float elapsed = 0f;
            while (elapsed < INVINCIBLE_DURATION)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Model.SetInvincible(false);
        }

        /// <summary>회피 쿨타임 (unscaledTime 기반). 0→1로 차오르며 HUD에 비율 전달.</summary>
        private IEnumerator DodgeCooldownRoutine()
        {
            float cooldown = Model.StatData.dodgeCooldown;
            _dodgeCooldownTimer = cooldown;
            Model.SetDodgeCooldownRatio(0f);

            float elapsed = 0f;
            while (elapsed < cooldown)
            {
                elapsed             += Time.unscaledDeltaTime;
                _dodgeCooldownTimer  = Mathf.Max(0f, cooldown - elapsed);
                float ratio          = Mathf.Clamp01(elapsed / cooldown);
                Model.SetDodgeCooldownRatio(ratio);
                yield return null;
            }

            _dodgeCooldownTimer = 0f;
            Model.SetDodgeCooldownRatio(1f);
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

            if (_slowCoroutine != null) StopCoroutine(_slowCoroutine);
            _slowCoroutine = StartCoroutine(SlowMotionRoutine());

            ServiceLocator.Get<CameraFollow>()?.TriggerZoomPunch();

            OnJustDodge(direction);
        }

        private void HandleDeath()
        {
            View.PlayDeath();
            StopSlowMotion();
            OnDisable();
        }

        // ── 슬로우모션 ────────────────────────────────────────────
        private IEnumerator SlowMotionRoutine()
        {
            float elapsed = 0f;
            while (elapsed < slowDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slowDuration);
                Time.timeScale = slowCurve.Evaluate(t);
                yield return null;
            }
            Time.timeScale = 1f;
            _slowCoroutine = null;
        }

        private void StopSlowMotion()
        {
            if (_slowCoroutine != null)
            {
                StopCoroutine(_slowCoroutine);
                _slowCoroutine = null;
            }
            Time.timeScale = 1f;
        }

        // ── 공격 범위 인게임 가시화 ───────────────────────────────

        private void ShowAttackRangeIndicator()
        {
            if (_attackRangeIndicator == null)
                CreateAttackRangeIndicator();

            var stat    = Model.StatData;
            var nearest = ServiceLocator.Get<WaveManager>()?.GetNearestEnemy(transform.position);
            var dir     = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            var boxCenter = (Vector2)transform.position + dir * stat.attackOffset;
            float angle   = Vector2.SignedAngle(Vector2.up, dir);

            _attackRangeIndicator.transform.position   = new Vector3(boxCenter.x, boxCenter.y, 0f);
            _attackRangeIndicator.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
            _attackRangeIndicator.transform.localScale = new Vector3(stat.attackWidth, stat.attackHeight, 1f);
            _attackRangeIndicator.SetActive(true);
        }

        private void HideAttackRangeIndicator()
        {
            if (_attackRangeIndicator != null)
                _attackRangeIndicator.SetActive(false);
        }

        private void CreateAttackRangeIndicator()
        {
            _attackRangeIndicator       = new GameObject("AttackRangeIndicator");
            _attackRangeSr              = _attackRangeIndicator.AddComponent<SpriteRenderer>();
            _attackRangeSr.sprite       = CreateSquareSprite();
            _attackRangeSr.color        = new Color(1f, 1f, 0f, 0.25f);
            _attackRangeSr.sortingOrder = 10;
            _attackRangeIndicator.SetActive(false);
        }

        private Sprite CreateSquareSprite()
        {
            const int size = 32;
            var tex        = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels     = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ── Gizmo 시각화 (에디터 전용) ───────────────────────────
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_showAttackGizmo) return;

            Gizmos.color  = new Color(1f, 1f, 0f, 0.4f);
            var rot       = Quaternion.Euler(0f, 0f, _lastAttackAngle);
            var oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(
                new Vector3(_lastAttackCenter.x, _lastAttackCenter.y, 0f), rot, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, new Vector3(_lastAttackSize.x, _lastAttackSize.y, 0.1f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_lastAttackSize.x, _lastAttackSize.y, 0.1f));
            Gizmos.matrix = oldMatrix;
            Gizmos.color  = Color.red;
            Gizmos.DrawSphere(new Vector3(_lastAttackCenter.x, _lastAttackCenter.y, 0f), 0.1f);
        }

        // ── 자식 클래스 override 지점 ─────────────────────────────
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
                Debug.LogError($"[{GetType().Name}] GestureRecognizer가 ServiceLocator에 없음.");
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
