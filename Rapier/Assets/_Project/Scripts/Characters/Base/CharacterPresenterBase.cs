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
    ///   Tap은 다음 상태에 따라 처리된다:
    ///     - 회피 대시 중 (_isDodgeDashActive)       : 차단
    ///     - 고유 스킬 중 (_isSignatureSkillActive)  : 차단
    ///     - 차지 스킬 중 (_isChargeSkillActive)     : 차단
    ///     - 저스트 회피 슬로우 중                    : OnJustDodgeTap() 호출 (고유 스킬 발동)
    ///     - 평상시                                   : OnNormalAttack() 호출 (일반 공격)
    ///   Swipe는 회피 쿨다운 중 차단된다.
    ///   회피 대시 중에는 Tap이 차단된다.
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
        [SerializeField] private float holdDuration = 1.4f;

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
        private const float ARRIVE_THRESHOLD = 0.05f;

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
        /// 회피 대시 중 / 고유 스킬 중 / 차지 스킬 중 / 슬로우 코루틴 진행 중(Exit 구간 포함)에는 차단.
        /// 슬로우 Hold 구간(_isJustDodgeSlowActive=true)에서만 OnJustDodgeTap 분기로 처리.
        /// </summary>
        private bool IsTapBlocked =>
            _isDodgeDashActive      ||
            _isSignatureSkillActive ||
            _isChargeSkillActive    ||
            _slowCoroutine != null;

        /// <summary>
        /// 일반 공격 가능 여부.
        /// 기본: 항상 true. Base의 Tap 차단은 HandleTap이 <see cref="IsTapBlocked"/>로 직접 수행하며,
        /// CanAttack은 자식이 추가 공격 조건(예: 쿨다운, 특수 리소스 부족)을 부여하기 위한 확장점이다.
        /// </summary>
        protected virtual bool CanAttack => true;

        /// <summary>
        /// 회피 (Swipe) 입력 허용 여부.
        /// 기본: 항상 true (Rapier/Assassin 기존 동작 유지).
        /// 자식이 override 하여 특수 상태(예: RangerPresenter 차지 경직 중) 에서 회피 자체를 차단할 수 있다.
        /// Base 의 쿨다운 차단과 AND 로 결합된다.
        /// </summary>
        protected virtual bool CanDodge => true;

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
        /// 자식이 EnableJustDodge()를 호출한 시점부터 true.
        /// 저스트 회피 발동 또는 DodgeDash 완료 시 false.
        /// 한 회피(또는 패링 등 자식이 정의한 트리거)당 딱 한 번만 발동을 보장.
        /// </summary>
        protected bool JustDodgeAvailable { get; private set; }

        /// <summary>
        /// 저스트 회피 발동 가능 상태로 진입한다.
        /// 자식이 적절한 시점(예: OnSwipe, 방향성 방어 성공 등)에 직접 호출한다.
        /// </summary>
        protected void EnableJustDodge() => JustDodgeAvailable = true;

        protected void ConsumeJustDodge() => JustDodgeAvailable = false;

        /// <summary>
        /// 저스트 회피를 코드에서 직접 발동한다.
        /// Gesture 를 거치지 않고 Presenter 도메인 내에서 슬로우 진입 경로를 실행한다.
        /// 사용처: ProcessTakeDamage(회피 중 피격), 자식의 패링 콜백 등.
        /// </summary>
        protected void TriggerJustDodge(Vector2 direction) => HandleJustDodge(direction);

        // ── 회피 목적지 / 시작점 / 방향 ─────────────────────────
        protected Vector2 DodgeDest  { get; private set; }
        /// <summary>회피 시작 시점의 플레이어 위치. OnDodgeDashComplete 에서 참조 가능.</summary>
        protected Vector2 DodgeStart { get; private set; }
        /// <summary>회피 방향 (정규화). OnDodgeDashComplete 에서 참조 가능.</summary>
        protected Vector2 DodgeDir   { get; private set; }

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


        // ── 초기화 ────────────────────────────────────────────────
        /// <summary>
        /// 캐릭터 초기화. 각 Presenter 의 Awake 에서 호출된다.
        /// </summary>
        /// <param name="statData">캐릭터 스탯 SO</param>
        /// <param name="view">캐릭터 뷰</param>
        /// <param name="characterId">
        /// EquipmentManager 장비 세트 키 (PascalCase — "Rapier", "Assassin", "Warrior", "Ranger").
        /// 기본값 "Rapier" 는 하위 호환을 위해 유지하되, Phase 26-D 부터 각 자식이 명시 전달한다.
        /// </param>
        protected void Init(CharacterStatData statData, ICharacterView view, string characterId = "Rapier")
        {
            // MetaStat 주입 — ServiceLocator 에서 EquipmentManager 조회
            MetaStatContainer metaContainer = null;
            var equipmentManager = ServiceLocator.Get<EquipmentManager>();
            if (equipmentManager != null)
            {
                var provider = new EquipmentMetaStatProvider(equipmentManager);
                metaContainer = provider.BuildContainer(characterId);
            }
            else
            {
                Debug.LogWarning("[CharacterPresenterBase] EquipmentManager 미등록 — 장비 스탯 없이 baseline 진행");
            }

            // RunStat 은 Start 에서 지연 주입한다.
            // 이유: StageManager 도 MonoBehaviour 이고 Awake 순서가 비결정적이므로
            // Init(Awake) 시점의 ServiceLocator.Get<StageManager>() 는 null 일 수 있다.
            // Start 는 모든 Awake 이후 호출되므로 안전하다.
            Model = new CharacterModel(statData, metaContainer, null);
            View  = view;

            Model.OnHpChanged     += ratio => View.UpdateHpGauge(ratio / Model.MaxHp);
            Model.OnDeath         += HandleDeath;
            Model.OnChargeChanged += View.UpdateChargeGauge;
        }

        // ── 이벤트 구독 / 해제 ────────────────────────────────────
        protected virtual void OnEnable() { }

        protected virtual void OnDisable()
        {
            if (_subscribedRunStat != null)
            {
                _subscribedRunStat.OnStatChanged -= HandleRunStatChanged;
                _subscribedRunStat = null;
            }

            if (Gesture == null) return;
            Gesture.OnTap            -= HandleTap;
            Gesture.OnSwipe          -= HandleSwipe;
            Gesture.OnMoveDirection  -= HandleMoveDirection;
            Gesture.OnMoveEnd        -= HandleMoveEnd;
            Gesture.OnHold           -= HandleHold;
            Gesture.OnRelease        -= HandleRelease;
            Gesture.OnHoldSwipe      -= HandleHoldSwipe;
            Gesture.OnHoldRelease    -= HandleHoldRelease;
            Gesture.OnHoldDragUpdate -= HandleHoldDragUpdate;
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

            // 저스트 회피 슬로우 Hold 구간에서만 고유 스킬 발동.
            // Exit 구간(_isJustDodgeSlowActive=false, _slowCoroutine!=null)은 IsTapBlocked가 막는다.
            if (_isJustDodgeSlowActive)
            {
                OnJustDodgeTap();
                return;
            }

            if (IsTapBlocked) return;

            // 평상시 → 일반 공격
            if (_isAttacking || !CanAttack) return;
            View.PlayAttack();
            StartCoroutine(AttackRoutine());
            OnNormalAttack(screenPos);
        }

        private IEnumerator AttackRoutine()
        {
            _isAttacking = true;
            yield return new WaitForSecondsRealtime(Model.StatData.attackCooldown);
            _isAttacking = false;
        }

        private void HandleSwipe(Vector2 direction)
        {
            if (Model == null || !Model.IsAlive) return;
            if (!CanDodge) return;
            if (_dodgeCooldownTimer > 0f) return;

            var stat  = Model.StatData;
            DodgeStart = (Vector2)transform.position;
            DodgeDir   = direction;
            DodgeDest  = DodgeStart + direction * stat.dashDistance;

            var stage = ServiceLocator.Get<StageBuilder>();
            if (stage != null) DodgeDest = stage.ClampToStage(DodgeDest);

            _isDodgeDashActive = true;
            LockMovement();
            Model.SetInvincible(true);

            StartCoroutine(DodgeCooldownRoutine());
            StartCoroutine(DodgeDashRoutine(stat.dashSpeed));
            StartCoroutine(DodgeInvincibleRoutine(Model.DodgeInvincibleDuration));

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
        /// 기본: 회피 대시 플래그 OFF + JustDodgeAvailable false + FreeMovement(조건부).
        /// 무적 해제는 DodgeInvincibleRoutine(DodgeInvincibleDuration 경과 후)이 담당한다.
        /// 저스트 회피 슬로우나 고유 스킬이 이어지는 경우에도 "회피 대시 자체는" 끝난 것이므로
        /// _isDodgeDashActive는 항상 false로 내린다. 이동 잠금 유지 여부는 자식이 override로 결정한다.
        /// </summary>
        protected virtual void OnDodgeDashComplete()
        {
            JustDodgeAvailable  = false;
            _isDodgeDashActive  = false;

            // 무적 해제는 DodgeInvincibleRoutine이 DodgeInvincibleDuration 경과 후 담당한다.
            // 대시 완료 시점에는 이동 잠금만 해제한다 (슬로우/스킬이 진행 중이 아닌 경우).
            if (!_isJustDodgeSlowActive && !_isSignatureSkillActive)
            {
                FreeMovement();
            }
        }

        /// <summary>
        /// 회피 무적 구간 타이머. DodgeInvincibleDuration(InvincibilityBonus 반영 최종값) 경과 후
        /// 조건이 허락하면 무적을 해제한다.
        /// 저스트 회피 슬로우 또는 고유 스킬이 진행 중이면 해제를 건너뛴다 — 그 경로에서 자체 해제.
        /// </summary>
        private IEnumerator DodgeInvincibleRoutine(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);

            // 저스트 회피 슬로우 또는 고유 스킬이 진행 중이면 무적 해제 스킵.
            // (OnDodgeDashComplete 혹은 OnSlowMotionEnd / EndSignatureSkillCleanup 에서 해제됨)
            if (!_isJustDodgeSlowActive && !_isSignatureSkillActive)
            {
                Model.SetInvincible(false);
            }
        }

        private IEnumerator DodgeCooldownRoutine()
        {
            float cooldown = Model.DodgeCooldown;
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

        private void HandleHoldSwipe(Vector2 direction)   => OnHoldSwipe(direction);
        private void HandleHoldRelease(Vector2 fromStart) => OnHoldRelease(fromStart);
        private void HandleHoldDragUpdate(Vector2 delta)  => OnHoldDragUpdate(delta);

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
            Model.SetChargeRatio(Mathf.Clamp01(duration / Model.ChargeRequiredTime));
            OnHold(duration);
        }

        private void HandleRelease(InputState lastState)
        {
            if (Model == null || !Model.IsAlive) return;

            bool fullyCharged = _isCharging && _holdDuration >= Model.ChargeRequiredTime;
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

            // 회피 쿨다운 타이머 리셋 — 쿨다운 진행 중 사망 시 DodgeCooldownRoutine이 강제 중단되어
            // _dodgeCooldownTimer가 0이 아닌 값에 멈추면 부활 후 HandleSwipe가 영구 차단된다.
            _dodgeCooldownTimer = 0f;
            Model.SetDodgeCooldownRatio(1f);

            // View.PlayDeath()가 gameObject.SetActive(false)를 호출하면 코루틴이 강제 중단된다.
            // AttackRoutine이 중단되면 _isAttacking 이 정리되지 않아 좀비 상태가 남으므로
            // SetActive(false) 전에 먼저 정리한다.
            _isAttacking = false;
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
        /// 방 전환(씬 전환 없이 포탈 진입) 시 ProgressionManager.HandleRoomEntered()가 호출한다.
        /// 내부에서 <see cref="OnRoomTransition"/>을 호출하여 자식이 방 전환 정리를 수행할 수 있도록 한다.
        /// </summary>
        public void NotifyRoomTransition()
        {
            OnRoomTransition();
        }

        /// <summary>
        /// 방 전환 시 자식이 override하여 자신의 상태(예: 잔상)를 정리하는 훅.
        /// 기본 구현은 no-op.
        /// </summary>
        protected virtual void OnRoomTransition() { }

        /// <summary>
        /// 이어하기 전용 부활. HP 복구 + View 재활성화 + 제스처 재구독.
        /// ProgressionManager가 인터미션 방 진입 시 호출한다.
        /// </summary>
        public virtual void Revive()
        {
            if (Model == null) return;
            Model.Revive(Model.MaxHp);
            View?.PlayRevive();

            if (Gesture != null)
            {
                // 이중 구독 방지: 해제 후 재구독
                Gesture.OnTap            -= HandleTap;            Gesture.OnTap            += HandleTap;
                Gesture.OnSwipe          -= HandleSwipe;          Gesture.OnSwipe          += HandleSwipe;
                Gesture.OnMoveDirection  -= HandleMoveDirection;  Gesture.OnMoveDirection  += HandleMoveDirection;
                Gesture.OnMoveEnd        -= HandleMoveEnd;        Gesture.OnMoveEnd        += HandleMoveEnd;
                Gesture.OnHold           -= HandleHold;           Gesture.OnHold           += HandleHold;
                Gesture.OnRelease        -= HandleRelease;        Gesture.OnRelease        += HandleRelease;
                Gesture.OnHoldSwipe      -= HandleHoldSwipe;      Gesture.OnHoldSwipe      += HandleHoldSwipe;
                Gesture.OnHoldRelease    -= HandleHoldRelease;    Gesture.OnHoldRelease    += HandleHoldRelease;
                Gesture.OnHoldDragUpdate -= HandleHoldDragUpdate; Gesture.OnHoldDragUpdate += HandleHoldDragUpdate;
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

            // Phase 2 (Exit): 스킬 발동권과 슬로우 Tap 발동권을 즉시 만료시키고 exitCurve로 복귀한다.
            // _isJustDodgeSlowActive 를 여기서 내려 Exit 구간에서는 OnJustDodgeTap 이 발동되지 않도록 한다.
            // (Exit 커브가 진행 중인 "슬로우 해제 중" 시점에 Tap 이 들어오면 슬로우가 끝난 후
            //  스킬이 시작되는 것처럼 보이는 버그 방지)
            Model?.SetJustDodgeReady(false);
            _isJustDodgeSlowActive = false;
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

        /// <summary>
        /// 원형 AoE 공격 범위 인디케이터를 생성하고 duration 후 자동 소멸시킨다.
        /// 원형 스프라이트는 런타임 Texture2D로 생성한다 (SerializeField 없이 동작).
        /// 외부 스프라이트가 필요하면 자식 클래스에서 오버라이드 없이 해당 GO에 직접 할당할 것.
        /// </summary>
        /// <param name="center">인디케이터 월드 중심 좌표</param>
        /// <param name="radius">원의 반지름 (UnityUnit)</param>
        /// <param name="duration">표시 지속 시간 (초)</param>
        protected void ShowAoeRangeIndicator(Vector2 center, float radius, float duration)
        {
            var go = new GameObject("AoeRangeIndicator");
            go.transform.position   = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateCircleSprite(64);
            sr.color        = new Color(1f, 0.5f, 0f, 0.25f); // 주황 반투명
            sr.sortingOrder = 10;

            Destroy(go, duration);
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float cx   = size * 0.5f - 0.5f;
            float cy   = size * 0.5f - 0.5f;
            float rSq  = (size * 0.5f) * (size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                pixels[y * size + x] = (dx * dx + dy * dy) <= rSq
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
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

        // ── IDamageable 공통 구현 ─────────────────────────────────
        /// <summary>IDamageable.IsAlive 공통 구현. 각 자식이 인터페이스 구현 시 위임.</summary>
        protected bool CharacterIsAlive => Model != null && Model.IsAlive;

        // ── TakeDamage 공통 경로 ──────────────────────────────────
        /// <summary>
        /// IDamageable.TakeDamage 의 표준 처리 경로.
        /// 1) JustDodgeAvailable → TriggerJustDodge 후 리턴
        /// 2) IsInvincible → 리턴
        /// 3) Model.TakeDamage → View.PlayHit
        ///
        /// 각 자식의 TakeDamage(IDamageable 구현)에서 이 메서드를 호출한다.
        /// </summary>
        protected void ProcessTakeDamage(float amount, Vector2 knockbackDir)
        {
            if (Model == null || !Model.IsAlive) return;

            if (JustDodgeAvailable)
            {
                ConsumeJustDodge();
                TriggerJustDodge(knockbackDir * -1f);
                return;
            }

            if (Model.IsInvincible) return;

            Model.TakeDamage(amount, knockbackDir);
            if (Model.IsAlive) View.PlayHit();
        }

        // ── 위치 순간이동 ─────────────────────────────────────────
        /// <summary>Transform과 View를 동시에 지정 위치로 이동한다. ProgressionManager.ResetPlayerPosition에서 사용.</summary>
        public void Warp(Vector2 pos)
        {
            transform.position = pos;
            View?.SetPosition(pos);
        }


        // ── RunStat 이벤트 핸들러 ────────────────────────────────
        private void HandleRunStatChanged()
        {
            Model?.RecomputeFinalStats();
            // View HP 게이지는 Model.OnHpChanged 가 자동 갱신 (Init 에서 이미 구독됨)
        }

        // ── 자식 클래스 override 지점 ─────────────────────────────

        /// <summary>
        /// 평상시 Tap — 일반 공격. 근접 캐릭터는 MeleePresenterBase가 구현.
        /// 원거리 캐릭터(Ranger)는 직접 override해 투사체 발사.
        /// </summary>
        protected virtual void OnNormalAttack(Vector2 screenPos)                       { }

        /// <summary>
        /// 저스트 회피 슬로우 중 Tap — 고유 스킬 발동.
        /// 각 캐릭터가 override해 고유 스킬을 구현.
        /// </summary>
        protected virtual void OnJustDodgeTap()                                        { }

        protected virtual void OnSwipe(Vector2 direction)                              { }
        protected virtual void OnHold(float duration)                                  { }
        protected virtual void OnSkillRelease(bool fullyCharged, bool justDodgeReady)  { }
        protected virtual void OnJustDodge(Vector2 direction)                          { }
        protected virtual void OnRelease(InputState lastState)                         { }
        protected virtual void OnHitDamageable(IDamageable target)                     { }

        /// <summary>
        /// Hold 중 Swipe 감지 시 호출. Warrior/Ranger가 override.
        /// </summary>
        protected virtual void OnHoldSwipe(Vector2 direction)                          { }

        /// <summary>
        /// Hold 후 손가락을 뗄 때 호출. Warrior/Ranger가 override.
        /// 차지 풀 여부는 각 자식이 자신의 로컬 플래그(_isChargedFull 등)로 판단한다.
        /// </summary>
        protected virtual void OnHoldRelease(Vector2 fromStart)                        { }

        /// <summary>
        /// Hold 중 매 프레임 드래그 변위 발행. Ranger가 override.
        /// </summary>
        protected virtual void OnHoldDragUpdate(Vector2 fromStart)                     { }

        /// <summary>
        /// AttackRoutine 시작 시 호출되는 훅. 히트 여부 무관, 항상 호출.
        /// Assassin이 잔상 동참 공격에 사용.
        /// </summary>
        protected virtual void OnPerformAttack()                                       { }

        protected virtual void Start()
        {
            Gesture = ServiceLocator.Get<GestureRecognizer>();
            if (Gesture == null)
            {
                Debug.LogError($"[{GetType().Name}] GestureRecognizer가 ServiceLocator에 없음.");
            }
            else
            {
                Gesture.OnTap            += HandleTap;
                Gesture.OnSwipe          += HandleSwipe;
                Gesture.OnMoveDirection  += HandleMoveDirection;
                Gesture.OnMoveEnd        += HandleMoveEnd;
                Gesture.OnHold           += HandleHold;
                Gesture.OnRelease        += HandleRelease;
                Gesture.OnHoldSwipe      += HandleHoldSwipe;
                Gesture.OnHoldRelease    += HandleHoldRelease;
                Gesture.OnHoldDragUpdate += HandleHoldDragUpdate;
            }

            // RunStat 지연 주입 — StageManager 는 MonoBehaviour 라 Awake 순서가 비결정적.
            // Start 는 모든 Awake 이후 실행되므로 ServiceLocator 조회가 안전.
            var stageManager = ServiceLocator.Get<StageManager>();
            if (stageManager != null && Model != null)
            {
                var runStatContainer = stageManager.RunStat;
                Model.SetRunStat(runStatContainer);

                if (runStatContainer != null)
                {
                    runStatContainer.OnStatChanged += HandleRunStatChanged;
                    _subscribedRunStat = runStatContainer;
                }
            }
            // StageManager 미등록 (로비 등) 이면 RunStat 없이 baseline 진행.
        }
    }
}
