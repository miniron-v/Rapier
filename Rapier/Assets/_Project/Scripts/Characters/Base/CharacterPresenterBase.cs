using System;
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Core.Stage;
using Game.Input;
using Game.Combat;
using Game.Enemies;
using Game.Data.Equipment;
using Game.Data.MetaStats;
using Game.Data.RunStats;

namespace Game.Characters
{
    /// <summary>
    /// 모든 플레이어 캐릭터 Presenter의 추상 베이스.
    ///
    /// [무적 구간]
    ///   일반 회피 : HandleSwipe → SetInvincible(true)
    ///               OnDodgeDashComplete()에서, 후속 상태가 없으면 SetInvincible(false)
    ///   저스트 회피: HandleJustDodge → SetInvincible(true)
    ///               OnSlowMotionEnd()에서, 고유 스킬이 이어지지 않으면 SetInvincible(false)
    ///   고유 스킬이 이어지는 경우 자식이 스킬 종료 시점에 무적 해제를 책임진다.
    ///
    /// [저스트 회피 트리거]
    ///   _justDodgeAvailable: Swipe 시 true, 발동 또는 DodgeDash 완료 시 false.
    ///   "한 회피당 딱 한 번만" 저스트 회피 발동을 보장.
    ///
    /// [입력 차단 — INPUT.md §5]
    ///   Tap은 다음 4개 상태 중 하나라도 활성이면 즉시 무시된다 (큐잉 없음):
    ///     1) 회피 대시 중          : _isDodgeDashActive
    ///     2) 저스트 회피 슬로우 중 : _isJustDodgeSlowActive
    ///     3) 고유 스킬 발동~복귀   : _isSignatureSkillActive
    ///     4) 차지 스킬 발동 중     : _isChargeSkillActive
    ///   Swipe는 회피 쿨다운 중 차단된다.
    ///
    ///   네 플래그 모두 Base가 소유하며, 차단 검사(IsTapBlocked)도 Base의
    ///   HandleTap 초입에서 수행된다. 자식은 상태 진입/이탈 시 Begin*/End*
    ///   훅으로만 신호를 보낼 수 있다 — 자식이 플래그 자체를 읽거나 쓰지 못하므로
    ///   차단 규칙을 우회할 수 없다 (OCP 보장).
    ///
    /// [CanAttack]
    ///   자식이 추가 공격 조건을 부여하고 싶을 때 override하는 확장점.
    ///   Base 차단과 AND로 결합된다.
    ///
    /// [MoveState]
    ///   Free   : Walk 허용
    ///   Locked : Walk 차단
    ///
    /// [게임 루프]
    ///   플레이어 사망 시 OnPlayerDeath 이벤트 발행.
    ///   BossRushManager가 구독하여 GAME OVER 처리.
    /// </summary>
    [RequireComponent(typeof(CharacterView))]
    public abstract class CharacterPresenterBase : MonoBehaviour
    {
        // ── 게임 루프 이벤트 ──────────────────────────────────────
        /// <summary>플레이어 사망 시 발행. BossRushManager가 구독.</summary>
        public event Action OnPlayerDeath;

        // ── 슬로우모션 설정 ───────────────────────────────────────
        [Header("Just Dodge Slow Motion — Hold (감속·유지)")]
        [SerializeField] private AnimationCurve holdCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.06f, 0.10f),  // 약 0.15초 이내에 0.1배속으로 급강하
            new Keyframe(1.00f, 0.10f)   // 이후 끝까지 0.1배속 유지
        );
        [SerializeField] private float holdDuration = 2.4f;

        [Header("Just Dodge Slow Motion — Exit (복귀)")]
        [SerializeField] private AnimationCurve exitCurve = new AnimationCurve(
            new Keyframe(0.00f, 0.10f),
            new Keyframe(1.00f, 1.00f)   // 0.1배속에서 정상으로 복귀
        );
        [SerializeField] private float exitDuration = 0.6f;

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

        // ── RunStat 구독 관리 ─────────────────────────────────────
        // 구독한 컨테이너를 보관 — OnDisable / OnDestroy 에서 동일 인스턴스 해제에 사용
        private RunStatContainer _subscribedRunStat;

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

        // ── 입력 차단 플래그 (Base 소유, 자식 접근 불가) ───────────
        // Tap 차단 규칙(INPUT.md §5)을 Base 레벨에서 일관되게 집행하기 위해
        // 네 개의 상태 플래그를 Base가 독점 소유한다. 자식 클래스는
        // Begin*/End* 훅으로만 상태를 토글할 수 있으며, 플래그 자체를
        // 읽거나 쓰지 못한다 — 이로써 차단 규칙의 OCP 우회가 원천 차단된다.
        private bool _isDodgeDashActive;
        private bool _isJustDodgeSlowActive;
        private bool _isSignatureSkillActive;
        private bool _isChargeSkillActive;

        /// <summary>
        /// Tap 입력이 즉시 무시되어야 하는지 여부.
        /// INPUT.md §5 네 가지 상태 중 하나라도 활성이면 true.
        /// </summary>
        private bool IsTapBlocked =>
            _isDodgeDashActive      ||
            _isJustDodgeSlowActive  ||
            _isSignatureSkillActive ||
            _isChargeSkillActive;

        /// <summary>
        /// 일반 공격 가능 여부.
        /// 기본: 항상 true. Base의 Tap 차단은 HandleTap이 <see cref="IsTapBlocked"/>로 직접 수행하며,
        /// CanAttack은 자식이 추가 공격 조건(예: 쿨다운, 특수 리소스 부족)을 부여하기 위한 확장점이다.
        /// </summary>
        protected virtual bool CanAttack => true;

        // ── 자식이 사용하는 상태 토글 훅 ──────────────────────────
        /// <summary>
        /// 자식 캐릭터가 자신의 고유 스킬 시퀀스에 진입할 때 호출한다.
        /// 호출 후 Tap 입력은 <see cref="EndSignatureSkill"/>가 호출될 때까지 차단된다.
        /// </summary>
        protected void BeginSignatureSkill() => _isSignatureSkillActive = true;

        /// <summary>
        /// 자식 캐릭터가 자신의 고유 스킬 시퀀스를 완전히 종료했을 때 호출한다.
        /// </summary>
        protected void EndSignatureSkill() => _isSignatureSkillActive = false;

        /// <summary>
        /// 자식 캐릭터가 차지 스킬을 비동기로 수행할 때 진입 시점에 호출한다.
        /// 동기 차지 스킬은 Base가 자동으로 관리하므로 호출할 필요가 없다.
        /// </summary>
        protected void BeginChargeSkill() => _isChargeSkillActive = true;

        /// <summary>
        /// 자식 캐릭터가 차지 스킬(비동기)을 완전히 종료했을 때 호출한다.
        /// </summary>
        protected void EndChargeSkill() => _isChargeSkillActive = false;

        /// <summary>
        /// 고유 스킬 시퀀스가 현재 활성인지 자식이 읽기 전용으로 확인할 수 있는 창구.
        /// OnDodgeDashComplete 등에서 "스킬 대기 중인지" 판단할 때 사용한다.
        /// </summary>
        protected bool IsSignatureSkillActive => _isSignatureSkillActive;

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
            // MetaStat 주입 — ServiceLocator 에서 EquipmentManager 조회
            MetaStatContainer metaContainer = null;
            var equipmentManager = ServiceLocator.Get<EquipmentManager>();
            if (equipmentManager != null)
            {
                var provider = new EquipmentMetaStatProvider(equipmentManager);
                // CharacterStatData 에 characterId 필드 없음 → "rapier" 고정 (현재 1종만 구현)
                metaContainer = provider.BuildContainer("rapier");
            }
            else
            {
                Debug.LogWarning("[CharacterPresenterBase] EquipmentManager 미등록 — 장비 스탯 없이 baseline 진행");
            }

            // RunStat 주입 — ServiceLocator 에서 StageManager 조회
            // StageManager 미등록(로비 등) 이면 runStat = null 로 RunStat 없이 진행
            RunStatContainer runStatContainer = null;
            var stageManager = ServiceLocator.Get<StageManager>();
            if (stageManager != null)
            {
                runStatContainer = stageManager.RunStat;
            }
            else
            {
                Debug.LogWarning("[CharacterPresenterBase] StageManager 미등록 — RunStat 없이 baseline 진행 (로비 등 정상)");
            }

            Model = new CharacterModel(statData, metaContainer, runStatContainer);
            View  = view;

            Model.OnHpChanged     += ratio => View.UpdateHpGauge(ratio / Model.MaxHp);
            Model.OnDeath         += HandleDeath;
            Model.OnChargeChanged += View.UpdateChargeGauge;

            // RunStat 픽 이벤트 구독 (스테이지 진입 시에만; 로비에선 null 이므로 no-op)
            if (runStatContainer != null)
            {
                runStatContainer.OnStatChanged += HandleRunStatChanged;
                _subscribedRunStat = runStatContainer;
            }
        }

        // ── 이벤트 구독 / 해제 ────────────────────────────────────
        protected virtual void OnEnable() { }

        protected virtual void OnDisable()
        {
            // RunStat 구독 해제는 Gesture 유무와 독립적으로 먼저 수행.
            // Init(Awake) 과 Gesture 초기화(Start) 사이에 OnDisable 이 발생할 경우,
            // Gesture null early-return 이 RunStat 해제를 skip 하지 않도록.
            if (_subscribedRunStat != null)
            {
                _subscribedRunStat.OnStatChanged -= HandleRunStatChanged;
                _subscribedRunStat = null;
            }

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

        protected virtual void OnDestroy()
        {
            // OnDisable 이 먼저 호출되면 _subscribedRunStat 은 이미 null → no-op
            // 파괴 경로에서 OnDisable 가 호출되지 않는 경우 방어
            if (_subscribedRunStat != null)
            {
                _subscribedRunStat.OnStatChanged -= HandleRunStatChanged;
                _subscribedRunStat = null;
            }
        }

        // ── 입력 처리 ─────────────────────────────────────────────
        private void HandleTap(Vector2 screenPos)
        {
            if (Model == null || !Model.IsAlive) return;

            // INPUT.md §5: 회피 대시 / 저스트 회피 슬로우 / 고유 스킬 / 차지 스킬
            // 진행 중에는 Tap을 즉시 무시한다 (큐잉 없음).
            // Base가 소유한 네 플래그로 검사하므로 자식이 우회할 수 없다.
            if (IsTapBlocked) return;

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
            EnemyPresenterBase nearestEnemy = FindNearestEnemy(30f);

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
                damageable.TakeDamage(Model.AttackPower, dir);
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

            JustDodgeAvailable     = true;
            _isDodgeDashActive     = true;
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
            float timeout           = estimatedDuration * 2f + 1f;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                float t          = Mathf.Clamp01(elapsed / estimatedDuration);
                float easedSpeed = dashSpeed * dodgeDashCurve.Evaluate(t);

                easedSpeed = Mathf.Max(easedSpeed, dashSpeed * 0.05f);

                var next = Vector2.MoveTowards(
                    transform.position, DodgeDest, easedSpeed * Time.deltaTime);
                View.SetPosition(next);

                if (Vector2.Distance(transform.position, DodgeDest) <= ARRIVE_THRESHOLD)
                    break;

                yield return null;
            }

            View.SetPosition(DodgeDest);
            OnDodgeDashComplete();
        }

        /// <summary>
        /// 회피 대시 완료 콜백.
        /// 기본: 회피 대시 플래그 OFF + JustDodgeAvailable false + 무적 OFF + FreeMovement.
        /// 저스트 회피 슬로우나 고유 스킬이 이어지는 경우에도 "회피 대시 자체는" 끝난 것이므로
        /// _isDodgeDashActive는 항상 false로 내린다. 무적/이동 잠금 유지 여부는 자식이 override로 결정한다.
        /// </summary>
        protected virtual void OnDodgeDashComplete()
        {
            JustDodgeAvailable  = false;
            _isDodgeDashActive  = false;

            // 저스트 회피 슬로우나 고유 스킬이 진행 중이면 무적/이동 잠금을 유지해야 하므로
            // 해당 플래그들이 모두 꺼진 경우에만 여기서 해제한다.
            if (!_isJustDodgeSlowActive && !_isSignatureSkillActive)
            {
                Model.SetInvincible(false);
                FreeMovement();
            }
        }

        private IEnumerator DodgeCooldownRoutine()
        {
            float cooldown = Model.StatData.dodgeCooldown;
            _dodgeCooldownTimer = cooldown;
            Model.SetDodgeCooldownRatio(0f);

            float elapsed = 0f;
            while (elapsed < cooldown)
            {
                elapsed             += Time.deltaTime;
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
                            + _moveDirection * (Model.MoveSpeed * Time.deltaTime);

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
            bool triggerSkill = fullyCharged || Model.IsJustDodgeReady;

            if (triggerSkill)
            {
                // 차지 스킬이 발동되는 동안 Tap을 차단한다.
                // 동기 차지 스킬(현재 Rapier)은 OnSkillRelease 호출 사이에만 활성이면 충분.
                // 향후 자식이 비동기 차지 스킬을 구현할 경우 자식 내부에서
                // BeginChargeSkill()/EndChargeSkill()로 수명을 명시적으로 관리해야 한다.
                bool chargeSkillLaunched = fullyCharged;
                if (chargeSkillLaunched) _isChargeSkillActive = true;
                try
                {
                    OnSkillRelease(fullyCharged, Model.IsJustDodgeReady);
                }
                finally
                {
                    // 자식이 비동기로 이어가려면 OnSkillRelease 내부에서 이미
                    // BeginChargeSkill()을 다시 호출했을 테지만, 현재 정책상 동기 완료로 간주하고
                    // 여기서 해제한다. 자식이 비동기를 원하면 EndChargeSkill()을 직접 호출하면 되고
                    // 그 사이 시간 동안 Tap을 차단하려면 BeginSignatureSkill()을 사용해야 한다.
                    if (chargeSkillLaunched) _isChargeSkillActive = false;
                }
            }

            _holdDuration = 0f;
            _isCharging   = false;
            Model.SetChargeRatio(0f);
            Model.SetJustDodgeReady(false);
            OnRelease(lastState);
        }

        private void HandleJustDodge(Vector2 direction)
        {
            if (Model == null || !Model.IsAlive) return;

            // 슬로우모션 구간 동안 Tap을 차단한다.
            // 자식이 OnJustDodge 안에서 BeginSignatureSkill을 호출해 스킬 시퀀스로 이어가면
            // 슬로우 종료 후에도 차단이 계속되고, 그렇지 않으면 OnSlowMotionEnd에서 해제된다.
            _isJustDodgeSlowActive = true;

            View.PlayDodge(direction);
            Model.SetJustDodgeReady(true);
            Model.SetInvincible(true);

            if (_slowCoroutine != null) StopCoroutine(_slowCoroutine);
            _slowCoroutine = StartCoroutine(SlowMotionRoutine());

            ServiceLocator.Get<CameraFollow>()?.TriggerZoomIn();
            OnJustDodge(direction);
        }

        private void HandleDeath()
        {
            // 차단 플래그 전부 해제 — 사망 이후 좀비 상태가 남아 입력이 끝까지 막히는 것을 방지.
            _isDodgeDashActive      = false;
            _isJustDodgeSlowActive  = false;
            _isSignatureSkillActive = false;
            _isChargeSkillActive    = false;

            // View.PlayDeath()가 gameObject.SetActive(false)를 호출하면 코루틴이 강제 중단된다.
            // AttackRoutine이 중단되면 _isAttacking·인디케이터가 정리되지 않아 좀비 상태가 남으므로,
            // SetActive(false) 전에 먼저 정리한다.
            _isAttacking = false;
            HideAttackRangeIndicator();
            OnBeforeDeath();          // 자식이 자신의 인디케이터/상태를 정리하는 훅

            StopSlowMotion();
            View.PlayDeath();
            OnDisable();
            OnPlayerDeath?.Invoke();
        }

        /// <summary>
        /// 사망 처리 직전 훅. View.PlayDeath()(→ SetActive(false)) 호출 전에 실행된다.
        /// 자식은 override 해서 자신의 인디케이터·코루틴·상태를 정리한다.
        /// </summary>
        protected virtual void OnBeforeDeath() { }

        /// <summary>
        /// 이어하기 전용 부활. HP 복구 + View 재활성화 + 제스처 재구독.
        /// ProgressionManager가 인터미션 방 진입 시 호출한다.
        /// </summary>
        public void Revive()
        {
            if (Model == null) return;
            Model.Revive(Model.MaxHp);
            View?.PlayRevive();

            if (Gesture != null)
            {
                // 이중 구독 방지를 위해 해제 후 재구독
                Gesture.OnTap           -= HandleTap;
                Gesture.OnTap           += HandleTap;
                Gesture.OnSwipe         -= HandleSwipe;
                Gesture.OnSwipe         += HandleSwipe;
                Gesture.OnMoveDirection -= HandleMoveDirection;
                Gesture.OnMoveDirection += HandleMoveDirection;
                Gesture.OnMoveEnd       -= HandleMoveEnd;
                Gesture.OnMoveEnd       += HandleMoveEnd;
                Gesture.OnHold          -= HandleHold;
                Gesture.OnHold          += HandleHold;
                Gesture.OnRelease       -= HandleRelease;
                Gesture.OnRelease       += HandleRelease;
                Gesture.OnJustDodge     -= HandleJustDodge;
                Gesture.OnJustDodge     += HandleJustDodge;
            }
            Debug.Log($"[{GetType().Name}] 부활 완료. HP: {Model.CurrentHp}/{Model.MaxHp}");
        }

        // ── 슬로우모션 ────────────────────────────────────────────
        private IEnumerator SlowMotionRoutine()
        {
            // Phase 1 (Hold): holdCurve를 끝까지 재생한다.
            // 스킬 발동 여부와 무관하게 항상 완주 — 커브를 끊지 않는다.
            float elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                Time.timeScale = holdCurve.Evaluate(Mathf.Clamp01(elapsed / holdDuration));
                yield return null;
            }

            // Bridge: holdCurve 완료 시점에 스킬이 아직 진행 중이면
            // holdCurve 끝 배속(0.10x)을 유지하며 스킬 종료를 대기한다.
            if (_isSignatureSkillActive)
            {
                float holdEndScale = holdCurve.Evaluate(1f);
                while (_isSignatureSkillActive)
                {
                    Time.timeScale = holdEndScale;
                    yield return null;
                }
            }

            // Phase 2 (Exit): 스킬 발동권을 즉시 만료시키고 exitCurve로 복귀한다.
            // 이 시점 이후에는 Hold/Release로 고유 스킬을 발동할 수 없다.
            Model?.SetJustDodgeReady(false);
            ServiceLocator.Get<CameraFollow>()?.TriggerZoomReturn(exitDuration);

            elapsed = 0f;
            while (elapsed < exitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                Time.timeScale = exitCurve.Evaluate(Mathf.Clamp01(elapsed / exitDuration));
                yield return null;
            }

            Time.timeScale = 1f;
            _slowCoroutine = null;
            OnSlowMotionEnd();
        }

        /// <summary>
        /// 슬로우모션 종료 콜백.
        /// 기본 순서:
        ///   1) JustDodgeReady(발동권) 을 명시적으로 만료시킨다 — 사용자가 슬로우 구간 내에
        ///      Hold/Release로 표식 대시 스킬 분기에 진입하지 않았다면 이후 Hold/Release는
        ///      일반 차지 스킬 분기만 탈 수 있도록 보장한다.
        ///   2) 저스트 회피 슬로우 플래그 OFF.
        ///   3) 고유 스킬 시퀀스가 이어지지 않은 경우에만 무적 OFF + FreeMovement
        ///      (이어졌다면 자식이 스킬 종료 시점에 해제한다).
        /// 자식은 override해서 추가 연출/상태 정리를 할 수 있지만, Tap 차단 플래그 관리는
        /// Base가 담당하므로 자식이 따로 건드릴 필요가 없다.
        /// </summary>
        protected virtual void OnSlowMotionEnd()
        {
            // SetJustDodgeReady(false)는 Exit 구간 진입 시점(SlowMotionRoutine Phase 2)에서
            // 이미 호출되었으므로 여기서는 생략한다.

            _isJustDodgeSlowActive = false;

            // 고유 스킬 시퀀스가 이어지는 경우 무적/이동잠금 해제는 스킬 종료 시점으로 미룬다.
            if (!_isSignatureSkillActive)
            {
                Model?.SetInvincible(false);
                FreeMovement();
            }
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
            EnemyPresenterBase nearest = FindNearestEnemy(30f);
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

        // ── 타겟 탐색 ─────────────────────────────────────────────
        /// <summary>
        /// Physics2D 기반 근접 적 탐색. WaveManager/BossRushManager 의존 없이 어느 씬에서든 동작.
        /// </summary>
        protected EnemyPresenterBase FindNearestEnemy(float searchRadius)
        {
            var hits         = Physics2D.OverlapCircleAll(transform.position, searchRadius,
                                                          LayerMask.GetMask("Enemy"));
            EnemyPresenterBase nearest  = null;
            float              minDistSq = float.MaxValue;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                float distSq = ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest   = enemy;
                }
            }
            return nearest;
        }

        // ── 위치 순간이동 ─────────────────────────────────────────
        /// <summary>Transform과 View를 동시에 지정 위치로 이동한다. ProgressionManager.ResetPlayerPosition에서 사용.</summary>
        public void Warp(Vector2 pos)
        {
            transform.position = pos;
            View?.SetPosition(pos);
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

        // ── RunStat 이벤트 핸들러 ────────────────────────────────
        private void HandleRunStatChanged()
        {
            Model?.RecomputeFinalStats();
            // View HP 게이지는 Model.OnHpChanged 가 자동 갱신 (Init 에서 이미 구독됨)
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
