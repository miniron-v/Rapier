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
    /// [무적 구간]
    ///   일반 회피 : HandleSwipe → SetInvincible(true)
    ///               OnDodgeDashComplete() → SetInvincible(false)
    ///   저스트 회피: HandleJustDodge → SetInvincible(true)
    ///               OnSlowMotionEnd() → SetInvincible(false)
    ///   자식이 각 콜백을 override하여 무적 해제 타이밍을 늦출 수 있음.
    ///
    /// [저스트 회피 트리거]
    ///   _justDodgeAvailable: Swipe 시 true, 발동 또는 DodgeDash 완료 시 false.
    ///   "한 회피당 딱 한 번만" 저스트 회피 발동을 보장.
    ///
    /// [공격 차단]
    ///   CanAttack: _isDodging 기반. 회피 대시 구간에서 false.
    ///   자식이 override하여 추가 조건 부여 가능.
    ///
    /// [_isDodging]
    ///   HandleSwipe → true
    ///   OnDodgeDashComplete() → false (자식이 SetDodging(false) 호출 억제 가능)
    ///   자식은 SetDodging(bool)으로 직접 제어.
    ///
    /// [MoveState]
    ///   Free   : Walk 허용
    ///   Locked : Walk 차단
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

        // ── 회피 대시 Ease 커브 ───────────────────────────────────
        [Header("Dodge Dash Ease (x=진행비율 0→1, y=속도배율 0→1)")]
        [SerializeField] private AnimationCurve dodgeDashCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(1.00f, 0.50f)
        );

        // ── 상수 ──────────────────────────────────────────────────
        private const float ATTACK_INDICATOR_DURATION = 0.4f;
        private const float ARRIVE_THRESHOLD          = 0.05f;

        // ── 내부 참조 ─────────────────────────────────────────────
        protected CharacterModel    Model   { get; private set; }
        protected ICharacterView    View    { get; private set; }
        protected GestureRecognizer Gesture { get; private set; }

        // ── MoveState ─────────────────────────────────────────────
        protected enum MoveState { Free, Locked }
        protected MoveState CurrentMoveState { get; private set; } = MoveState.Free;

        protected void LockMovement()
        {
            CurrentMoveState = MoveState.Locked;
            _moveDirection   = Vector2.zero;
        }

        protected void FreeMovement()
        {
            CurrentMoveState = MoveState.Free;
            _moveDirection   = Vector2.zero;
        }

        // ── 회피/공격 차단 플래그 ─────────────────────────────────
        private bool _isDodging;

        /// <summary>
        /// 회피 대시가 진행 중인지 여부.
        /// HandleSwipe → true, OnDodgeDashComplete() → false.
        /// 자식은 SetDodging()으로 직접 제어한다.
        /// </summary>
        protected void SetDodging(bool value) => _isDodging = value;

        /// <summary>
        /// 일반 공격 가능 여부.
        /// 기본: 회피 대시 중(_isDodging)이면 false.
        /// 자식이 override하여 추가 차단 조건 부여 가능.
        /// </summary>
        protected virtual bool CanAttack => !_isDodging;

        // ── 저스트 회피 가용 플래그 ───────────────────────────────
        /// <summary>
        /// Swipe 시작 시 true, 저스트 회피 발동 또는 DodgeDash 완료 시 false.
        /// 한 회피당 딱 한 번만 저스트 회피 발동을 보장.
        /// </summary>
        protected bool JustDodgeAvailable { get; private set; }

        protected void ConsumeJustDodge() => JustDodgeAvailable = false;

        // ── 회피 목적지 ───────────────────────────────────────────
        protected Vector2 DodgeDest { get; private set; }

        // ── 이동 ─────────────────────────────────────────────────
        private Vector2 _moveDirection;

        // ── 차지/홀드 ─────────────────────────────────────────────
        private float _holdDuration;
        private bool  _isCharging;

        // ── 공격 ─────────────────────────────────────────────────
        private bool _isAttacking;

        // ── 회피 쿨타임 ───────────────────────────────────────────
        private float _dodgeCooldownTimer;

        // ── 슬로우모션 ────────────────────────────────────────────
        private Coroutine _slowCoroutine;

        // ── 공격 범위 가시화 ──────────────────────────────────────
        private GameObject     _attackRangeIndicator;
        private SpriteRenderer _attackRangeSr;

        // ── Gizmo ─────────────────────────────────────────────────
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

        // ── 입력 처리 ─────────────────────────────────────────────
        private void HandleTap(Vector2 screenPos)
        {
            if (Model == null || !Model.IsAlive) return;
            if (_isAttacking || !CanAttack) return;
            View.PlayAttack();
            StartCoroutine(AttackRoutine());
            OnTap(screenPos);
        }

        /// <summary>
        /// 즉시 공격 루틴.
        /// PerformAttack()을 인디케이터 표시 직후 즉시 실행.
        /// 인디케이터는 ATTACK_INDICATOR_DURATION 동안 유지 후 숨김.
        /// </summary>
        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            ShowAttackRangeIndicator();
            PerformAttack();
            yield return new WaitForSecondsRealtime(ATTACK_INDICATOR_DURATION);
            HideAttackRangeIndicator();
            _isAttacking = false;
        }

        private void PerformAttack()
        {
            // WaveManager 우선, 없으면 BossRushManager 폴백
            EnemyPresenterBase nearestEnemy = null;
            var waveManager = ServiceLocator.Get<WaveManager>();
            if (waveManager != null)
                nearestEnemy = waveManager.GetNearestEnemy(transform.position);
            if (nearestEnemy == null)
                nearestEnemy = ServiceLocator.Get<BossRushManager>()?.GetCurrentBoss();

            var stat = Model.StatData;
            var dir  = nearestEnemy != null
                ? ((Vector2)nearestEnemy.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            var   boxCenter  = (Vector2)transform.position + dir * stat.attackOffset;
            var   boxSize    = new Vector2(stat.attackWidth, stat.attackHeight);
            float angle      = Vector2.SignedAngle(Vector2.up, dir);
            int   enemyLayer = LayerMask.GetMask("Enemy");

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
                OnHitDamageable(damageable);
                hitCount++;
            }
            Debug.Log($"[Attack] 히트: {hitCount}명 / 범위 내 오브젝트: {hits.Length}");
        }

        private void HandleSwipe(Vector2 direction)
        {
            if (Model == null || !Model.IsAlive) return;
            if (_dodgeCooldownTimer > 0f) return;

            var stat  = Model.StatData;
            DodgeDest = (Vector2)transform.position + direction * stat.dashDistance;

            var stage = ServiceLocator.Get<StageBuilder>();
            if (stage != null) DodgeDest = stage.ClampToStage(DodgeDest);

            JustDodgeAvailable = true;
            SetDodging(true);
            LockMovement();
            Model.SetInvincible(true);

            StartCoroutine(DodgeCooldownRoutine());
            StartCoroutine(DodgeDashRoutine(stat.dashSpeed));

            View.PlayDodge(direction);
            OnSwipe(direction);
        }

private IEnumerator DodgeDashRoutine(float dashSpeed)
        {
            float totalDist = Vector2.Distance(transform.position, DodgeDest);
            if (totalDist < ARRIVE_THRESHOLD)
            {
                View.SetPosition(DodgeDest);
                OnDodgeDashComplete();
                yield break;
            }

            float elapsed           = 0f;
            float estimatedDuration = totalDist / (dashSpeed * 0.6f);
            // 타임아웃: 예상 시간의 2배 내 었으면 반드시 완료
            float timeout           = estimatedDuration * 2f + 1f;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                float t          = Mathf.Clamp01(elapsed / estimatedDuration);
                float easedSpeed = dashSpeed * dodgeDashCurve.Evaluate(t);

                // 커브 끝 부분에서 속도가 거의 0이 되면 MinSpeed로 보증
                easedSpeed = Mathf.Max(easedSpeed, dashSpeed * 0.05f);

                var next = Vector2.MoveTowards(
                    transform.position, DodgeDest, easedSpeed * Time.deltaTime);
                View.SetPosition(next);

                if (Vector2.Distance(transform.position, DodgeDest) <= ARRIVE_THRESHOLD)
                    break;

                yield return null;
            }

            // 타임아웃이든 정상 도달이든 반드시 종점으로 스냅
            View.SetPosition(DodgeDest);
            OnDodgeDashComplete();
        }

        /// <summary>
        /// 회피 대시 완료 콜백.
        /// 기본: SetDodging(false) + JustDodgeAvailable false + 무적 OFF + FreeMovement.
        /// 자식(Rapier): 스킬 대기 중이면 SetDodging(false)와 무적 해제를 억제.
        /// </summary>
        protected virtual void OnDodgeDashComplete()
        {
            JustDodgeAvailable = false;
            SetDodging(false);
            Model.SetInvincible(false);
            FreeMovement();
        }

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
                Model.SetDodgeCooldownRatio(Mathf.Clamp01(elapsed / cooldown));
                yield return null;
            }
            _dodgeCooldownTimer = 0f;
            Model.SetDodgeCooldownRatio(1f);
        }

        private void HandleMoveDirection(Vector2 dir)
        {
            if (Model == null || !Model.IsAlive) return;
            if (CurrentMoveState == MoveState.Locked) return;
            _moveDirection = dir;
        }

        private void HandleMoveEnd() => _moveDirection = Vector2.zero;

        protected virtual void Update()
        {
            if (Model == null || !Model.IsAlive) return;

            if (_showAttackGizmo)
            {
                _gizmoTimer -= Time.deltaTime;
                if (_gizmoTimer <= 0f) _showAttackGizmo = false;
            }

            if (CurrentMoveState == MoveState.Free && _moveDirection.sqrMagnitude > 0.01f)
            {
                var nextPos = (Vector2)transform.position
                            + _moveDirection * (Model.StatData.moveSpeed * Time.deltaTime);

                var stage = ServiceLocator.Get<StageBuilder>();
                if (stage != null) nextPos = stage.ClampToStage(nextPos);

                View.SetPosition(nextPos);
            }
        }

        private void HandleHold(float duration)
        {
            if (Model == null || !Model.IsAlive) return;
            _holdDuration = duration;
            _isCharging   = true;
            Model.SetChargeRatio(Mathf.Clamp01(duration / Model.StatData.chargeRequiredTime));
            OnHold(duration);
        }

        private void HandleRelease(InputState lastState)
        {
            if (Model == null || !Model.IsAlive) return;

            bool fullyCharged = _isCharging && _holdDuration >= Model.StatData.chargeRequiredTime;

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
            if (Model == null || !Model.IsAlive) return;
            View.PlayDodge(direction);
            Model.SetJustDodgeReady(true);
            Model.SetInvincible(true);

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
                Time.timeScale = slowCurve.Evaluate(Mathf.Clamp01(elapsed / slowDuration));
                yield return null;
            }
            Time.timeScale = 1f;
            _slowCoroutine = null;
            OnSlowMotionEnd();
        }

        /// <summary>
        /// 슬로우모션 종료 콜백.
        /// 기본: 무적 OFF.
        /// 자식(Rapier): 스킬 대시 중이면 억제.
        /// </summary>
        protected virtual void OnSlowMotionEnd()
        {
            Model?.SetInvincible(false);
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

        // ── 공격 범위 가시화 ──────────────────────────────────────
        private void ShowAttackRangeIndicator()
        {
            if (_attackRangeIndicator == null) CreateAttackRangeIndicator();

            var stat    = Model.StatData;
            EnemyPresenterBase nearest = ServiceLocator.Get<WaveManager>()?.GetNearestEnemy(transform.position)
                ?? ServiceLocator.Get<BossRushManager>()?.GetCurrentBoss();
            var dir     = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            var   boxCenter = (Vector2)transform.position + dir * stat.attackOffset;
            float angle     = Vector2.SignedAngle(Vector2.up, dir);

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

        protected Sprite CreateSquareSprite()
        {
            const int size = 32;
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ── Gizmo ─────────────────────────────────────────────────
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
        protected virtual void OnTap(Vector2 screenPos)                                { }
        protected virtual void OnSwipe(Vector2 direction)                              { }
        protected virtual void OnHold(float duration)                                  { }
        protected virtual void OnSkillRelease(bool fullyCharged, bool justDodgeReady)  { }
        protected virtual void OnJustDodge(Vector2 direction)                          { }
        protected virtual void OnRelease(InputState lastState)                         { }
        protected virtual void OnHitDamageable(IDamageable target)                     { }

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
