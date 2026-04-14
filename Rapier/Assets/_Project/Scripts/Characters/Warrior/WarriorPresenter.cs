using System;
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Data.Characters;
using Game.Input;

namespace Game.Characters.Warrior
{
    /// <summary>
    /// 워리어 캐릭터 Presenter.
    ///
    /// [핵심 철학]
    ///   일반 저스트 회피를 쓰지 않는 유일한 캐릭터.
    ///   모든 고유 스킬 트리거는 "차지 Full + 방패 휘두르기(Swipe) → 패링" 또는
    ///   "차지 Full + Release → 대지 분쇄" 경로로 집중된다.
    ///
    /// [차지 상태 전이]
    ///   Hold 진입 → SetDamageMultiplier(0.5f)  (피격 50% 감소)
    ///   차지 Full  → SetChargedFull(true)        (Gesture 에 알림)
    ///   Full 전 Release/Swipe → 무반응
    ///   Full 후 Swipe → 방패 휘두르기 (방향성 방어 + 히트박스)
    ///   Full 후 Release → 대지 분쇄 (전방 광역)
    ///   패링 성립 → 슬로우 + 즉시 대지 분쇄
    ///
    /// [잠금-해제 매핑]
    ///   SetDamageMultiplier(0.5f)    ↔ SetDamageMultiplier(1.0f)
    ///     · 정상 Full 전환: 유지 (Full 후 스킬 발동까지)
    ///     · Release(Full 후): ExecuteGroundSmash → 복구
    ///     · Swipe(Full 후): ShieldSwingRoutine 완료 → 복구
    ///     · 사망: OnBeforeDeath → 복구
    ///     · OnDisable: ResetChargeState → 복구
    ///
    ///   SetChargedFull(true)         ↔ SetChargedFull(false)
    ///     · Release(Full 후): ResetChargeState → false
    ///     · Swipe(Full 후): ResetChargeState → false
    ///     · 사망: OnBeforeDeath → false
    ///     · OnDisable: ResetChargeState → false
    ///
    ///   SetDirectionalGuard          ↔ ClearDirectionalGuard
    ///     · 정상 완료: ShieldSwingRoutine dodgeDashDuration 경과 → 복구
    ///     · 패링 성립: CharacterModel.TakeDamage 내부에서 자동 해제 후 OnParry 호출
    ///     · 사망: OnBeforeDeath → ClearDirectionalGuard
    ///     · OnDisable: ResetChargeState → ClearDirectionalGuard
    ///
    ///   BeginSignatureSkill          ↔ EndSignatureSkill
    ///     · 대지 분쇄: 동기 실행 (GroundSmash 자체가 동기) — Base 가 자동 관리하지 않으므로
    ///       GroundSmash 전에 BeginSignatureSkill, 후에 EndSignatureSkill 직접 페어링
    ///     · 사망: OnBeforeDeath → EndSignatureSkill 직접 호출
    ///     · OnDisable: ResetChargeState → EndSignatureSkill
    /// </summary>
    [RequireComponent(typeof(CharacterView))]
    public class WarriorPresenter : CharacterPresenterBase, IDamageable, IPlayerCharacter
    {
        // ── 직렬화 필드 ───────────────────────────────────────────
        [Header("데이터")]
        [SerializeField] private WarriorStatData _statData;

        // ── 런타임 비직렬화 필드 ──────────────────────────────────
        private CharacterView _view;

        /// <summary>현재 차지가 Full 에 도달했는지 여부. Gesture.SetChargedFull 과 동기화됨.</summary>
        [NonSerialized] private bool _isChargedFull;

        /// <summary>현재 Hold 차지 중인지 여부. DamageMultiplier 복구 조건에 사용.</summary>
        [NonSerialized] private bool _isHolding;

        /// <summary>방패 휘두르기 코루틴 핸들. 사망/OnDisable 시 중단에 사용.</summary>
        [NonSerialized] private Coroutine _shieldSwingCoroutine;

        /// <summary>대지 분쇄 중인지 플래그 (사망 경로에서 EndSignatureSkill 중복 호출 방지).</summary>
        [NonSerialized] private bool _isGroundSmashing;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            _view = GetComponent<CharacterView>();

            if (_statData == null)
            {
                Debug.LogError("[WarriorPresenter] WarriorStatData 가 할당되지 않음.");
                return;
            }

            Init(_statData, _view, "Warrior");

            if (_statData.sprite != null)
                _view.SetSprite(_statData.sprite);

            ServiceLocator.Register<IPlayerCharacter>(this);
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ServiceLocator.Unregister<IPlayerCharacter>();
            ServiceLocator.Unregister<WarriorPresenter>();
        }

        // ── Hold 이벤트 추가 구독 (OnHoldDragUpdate / OnHoldSwipe / OnHoldRelease) ──
        /// <summary>
        /// Base 의 Start 가 Gesture 를 초기화한 뒤 추가 이벤트를 구독한다.
        /// </summary>
        protected override void Start()
        {
            base.Start();

            if (Gesture != null)
            {
                // OnHoldDragUpdate 는 무시 (Warrior 는 드래그 방향 불필요)
                Gesture.OnHoldSwipe   += HandleHoldSwipe;
                Gesture.OnHoldRelease += HandleHoldRelease;
            }
        }

        protected override void OnDisable()
        {
            // Hold 확장 이벤트 해제
            if (Gesture != null)
            {
                Gesture.OnHoldSwipe   -= HandleHoldSwipe;
                Gesture.OnHoldRelease -= HandleHoldRelease;
            }

            // 방패 휘두르기 코루틴 중단
            if (_shieldSwingCoroutine != null)
            {
                StopCoroutine(_shieldSwingCoroutine);
                _shieldSwingCoroutine = null;
            }

            // 모든 상태 리셋
            ResetChargeState();

            base.OnDisable();
        }

        // ── IDamageable / IPlayerCharacter ────────────────────────
        /// <inheritdoc/>
        public bool IsAlive => Model != null && Model.IsAlive;

        /// <inheritdoc/>
        public void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;

            // Warrior 는 저스트 회피(JustDodgeAvailable) 발동 안 함 — 직접 패스
            if (Model.IsInvincible) return;

            // 방향성 방어 판정은 CharacterModel.TakeDamage 내부에서 처리된다.
            // knockbackDir 을 그대로 전달하면 됨.
            Model.TakeDamage(amount, knockbackDir);
            if (Model.IsAlive)
                View.PlayHit();
        }

        /// <inheritdoc/>
        public CharacterModel PublicModel => Model;

        // ── Hold 진입 훅 — 차지 시작 ─────────────────────────────
        /// <summary>
        /// Hold 이벤트에 반응. 차지 첫 프레임에 DamageMultiplier 를 0.5f 로 세팅한다.
        /// 이미 홀딩 중이면 재진입 하지 않는다.
        /// </summary>
        protected override void OnHold(float duration)
        {
            if (!_isHolding)
            {
                _isHolding = true;
                Model.SetDamageMultiplier(1f - _statData.DamageReductionPercent / 100f);
                Debug.Log("[WarriorPresenter] 차지 시작 — 피격 데미지 감소 적용");
            }

            // 차지 Full 도달 판정
            if (!_isChargedFull && duration >= Model.ChargeRequiredTime)
            {
                _isChargedFull = true;
                Gesture?.SetChargedFull(true);
                Debug.Log("[WarriorPresenter] 차지 Full 도달");
            }
        }

        // ── Hold 확장 이벤트 핸들러 ──────────────────────────────
        /// <summary>
        /// Hold 중 Swipe 감지 → 방패 휘두르기 루틴 시작.
        /// 차지 Full 전이면 무시.
        /// </summary>
        private void HandleHoldSwipe(Vector2 direction)
        {
            if (Model == null || !Model.IsAlive) return;
            if (!_isChargedFull) return; // 차지 Full 전 → 무반응

            // 차지 플래그/DamageMultiplier 소비 (방어는 ShieldSwingRoutine 에서 관리)
            ResetChargeStateExceptGuard();

            // 방패 휘두르기 시작 — 방어 활성 및 해제는 루틴 내부에서 처리
            Vector2 swipeDir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.up;
            _shieldSwingCoroutine = StartCoroutine(ShieldSwingRoutine(swipeDir));
        }

        /// <summary>
        /// Hold 후 Release → 대지 분쇄.
        /// 차지 Full 전이면 무시.
        /// </summary>
        private void HandleHoldRelease(Vector2 fromStart, bool chargedFull)
        {
            if (Model == null || !Model.IsAlive) return;

            // chargedFull 페이로드보다 _isChargedFull 로컬 플래그 우선
            // (Warrior 가 직접 Gesture.SetChargedFull 로 세팅했으므로 일치)
            if (!_isChargedFull) return; // 차지 Full 전 → 무반응

            ExecuteGroundSmash();

            // 차지 상태 소비
            ResetChargeState();
        }

        // ── 방패 휘두르기 ─────────────────────────────────────────
        /// <summary>
        /// 방패 휘두르기 루틴.
        /// 1. 방향성 방어(SetDirectionalGuard) 활성.
        /// 2. 히트박스 1회 생성 (ATK × 150%).
        /// 3. dodgeDashDuration 후 방어 해제(ClearDirectionalGuard).
        /// </summary>
        private IEnumerator ShieldSwingRoutine(Vector2 direction)
        {
            // 방향성 방어 활성
            Model.SetDirectionalGuard(direction, _statData.ShieldGuardHalfAngle, HandleParry);
            Debug.Log($"[WarriorPresenter] 방패 휘두르기 — 방향: {direction}, 방어 활성");

            // 히트박스 1회 생성
            ExecuteShieldHit(direction);

            // dodgeDashDuration 동안 방어 유지 (StatData 에 별도 필드 없음 → dashSpeed 기반 추정 불필요, dodgeCooldown 재사용)
            // 기획서: "dodgeDashDuration 재활용" = 회피 대시에 걸리는 실제 시간
            // → 회피 대시 거리 / 대시 속도로 근사 (별도 SerializeField 없으므로 계산)
            float guardDuration = (_statData.dashDistance / Mathf.Max(_statData.dashSpeed, 0.1f));
            yield return new WaitForSeconds(guardDuration);

            // 방어 해제 (패링으로 이미 해제된 경우 ClearDirectionalGuard는 멱등)
            Model.ClearDirectionalGuard();
            _shieldSwingCoroutine = null;
            Debug.Log("[WarriorPresenter] 방패 휘두르기 종료 — 방어 해제");
        }

        /// <summary>
        /// 방패 휘두르기 히트박스 실행. ATK × ShieldSwingDamagePercent%.
        /// </summary>
        private void ExecuteShieldHit(Vector2 direction)
        {
            var stat        = _statData;
            var boxCenter   = (Vector2)transform.position + direction * stat.attackOffset;
            var boxSize     = new Vector2(stat.attackWidth, stat.attackHeight);
            float angle     = Vector2.SignedAngle(Vector2.up, direction);
            int enemyLayer  = LayerMask.GetMask("Enemy");

            // 인디케이터 표시 (Base 의 ShowAoeRangeIndicator 재사용 또는 별도 사각형)
            ShowShieldSwingIndicator(boxCenter, boxSize, angle);

            var hits     = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, enemyLayer);
            int hitCount = 0;
            float damage = Model.AttackPower * (stat.ShieldSwingDamagePercent / 100f);

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                // 넉백 방향: Swipe 방향
                enemy.TakeDamage(damage, direction);
                hitCount++;
            }

            Debug.Log($"[WarriorPresenter] 방패 휘두르기 히트: {hitCount}명 / 데미지: {damage:F0}");
        }

        // ── 대지 분쇄 ─────────────────────────────────────────────
        /// <summary>
        /// 대지 분쇄 실행. 전방 광역 ATK × ChargeDamagePercent% × SkillDmgMult.
        /// OnParry 콜백에서도 재사용된다.
        /// </summary>
        private void ExecuteGroundSmash()
        {
            if (Model == null || !Model.IsAlive) return;

            _isGroundSmashing = true;
            BeginSignatureSkill();

            EnemyPresenterBase nearest = FindNearestEnemy(30f);
            var dir = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            var stat       = _statData;
            var boxCenter  = (Vector2)transform.position + dir * stat.attackOffset;
            // 대지 분쇄는 전방 광역 → 공격 범위를 넉넉하게 (너비 × 2 적용)
            var boxSize    = new Vector2(stat.attackWidth * 2f, stat.attackHeight * 2f);
            float angle    = Vector2.SignedAngle(Vector2.up, dir);
            int enemyLayer = LayerMask.GetMask("Enemy");

            ShowAoeRangeIndicator(boxCenter, boxSize.x * 0.5f, 0.35f);

            float damage   = Model.AttackPower * (stat.ChargeDamagePercent / 100f)
                             * Model.SkillDamageMultiplier;

            var hits     = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, enemyLayer);
            int hitCount = 0;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.TakeDamage(damage, dir);
                hitCount++;
            }

            Debug.Log($"[WarriorPresenter] 대지 분쇄 히트: {hitCount}명 / 데미지: {damage:F0}");

            EndSignatureSkill();
            _isGroundSmashing = false;
        }

        // ── 패링 콜백 ─────────────────────────────────────────────
        /// <summary>
        /// 방향성 방어 판정 패링 성립 시 CharacterModel.TakeDamage 에서 호출된다.
        /// 슬로우 진입 후 즉시 대지 분쇄 발동.
        /// </summary>
        private void HandleParry()
        {
            if (Model == null || !Model.IsAlive) return;
            Debug.Log("[WarriorPresenter] 패링 성립! 슬로우 + 대지 분쇄 발동");

            // 슬로우 모션 진입 (Base 의 저스트 회피 슬로우를 직접 트리거할 수 없으므로
            // Time.timeScale 직접 제어로 간이 슬로우 구현)
            StartCoroutine(ParrySlowRoutine());

            // 즉시 대지 분쇄
            ExecuteGroundSmash();
        }

        /// <summary>
        /// 패링 후 간이 슬로우 — 0.5초 동안 0.2배속 후 복귀.
        /// Base 의 JustDodge 슬로우와 달리 Warrior 전용 간이 버전.
        /// </summary>
        private IEnumerator ParrySlowRoutine()
        {
            Time.timeScale = 0.2f;
            yield return new WaitForSecondsRealtime(0.5f);
            Time.timeScale = 1f;
        }

        // ── 사망 전처리 훅 ────────────────────────────────────────
        /// <summary>
        /// 사망 처리 직전 훅. 모든 차지/방어 상태를 즉시 해제한다.
        /// </summary>
        protected override void OnBeforeDeath()
        {
            // 방패 휘두르기 코루틴 중단
            if (_shieldSwingCoroutine != null)
            {
                StopCoroutine(_shieldSwingCoroutine);
                _shieldSwingCoroutine = null;
            }

            // 대지 분쇄 중이면 EndSignatureSkill 강제 해제
            if (_isGroundSmashing)
            {
                EndSignatureSkill();
                _isGroundSmashing = false;
            }

            // 방어/차지 상태 전부 리셋
            ResetChargeState();
        }

        // ── DodgeDash 완료 훅 ────────────────────────────────────
        protected override void OnDodgeDashComplete()
        {
            // Warrior 는 저스트 회피를 발동하지 않으므로 JustDodgeAvailable 은 항상 false 유지
            // Base 기본 처리(이동 잠금 해제 등)만 수행
            base.OnDodgeDashComplete();
            // JustDodgeAvailable 이 true 로 올라갔다면 즉시 소비 (저스트 회피 차단)
            ConsumeJustDodge();
        }

        // ── 저스트 회피 훅 — Warrior 는 발동 안 함 ───────────────
        protected override void OnJustDodge(Vector2 direction)
        {
            // Warrior 는 저스트 회피 고유 스킬 없음 — no-op
        }

        // ── 스킬 발동 훅 (Base OnRelease 경로) ───────────────────
        /// <summary>
        /// Base 의 HandleRelease → OnSkillRelease 경로.
        /// Warrior 의 차지 스킬은 OnHoldRelease 에서 처리하므로 여기서는 no-op.
        /// (Base 는 chargedFull 판정 시 OnSkillRelease 를 호출하지만 Hold 확장 이벤트와
        ///  이중 처리되지 않도록 비워 둔다.)
        /// </summary>
        protected override void OnSkillRelease(bool fullyCharged, bool justDodgeReady)
        {
            // OnHoldRelease 에서 이미 처리됨 — no-op
        }

        // ── 인디케이터 유틸 ──────────────────────────────────────
        private GameObject     _shieldIndicator;
        private SpriteRenderer _shieldIndicatorSr;

        private void ShowShieldSwingIndicator(Vector2 center, Vector2 size, float angle)
        {
            if (_shieldIndicator == null)
            {
                _shieldIndicator              = new GameObject("ShieldSwingIndicator");
                _shieldIndicatorSr            = _shieldIndicator.AddComponent<SpriteRenderer>();
                _shieldIndicatorSr.sprite     = CreateSquareSprite();
                _shieldIndicatorSr.color      = new Color(0f, 0.5f, 1f, 0.35f); // 파랑 반투명
                _shieldIndicatorSr.sortingOrder = 11;
                _shieldIndicator.SetActive(false);
            }

            _shieldIndicator.transform.position   = new Vector3(center.x, center.y, 0f);
            _shieldIndicator.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
            _shieldIndicator.transform.localScale = new Vector3(size.x, size.y, 1f);
            _shieldIndicator.SetActive(true);

            StartCoroutine(HideIndicatorAfterDelay(_shieldIndicator, 0.35f));
        }

        private IEnumerator HideIndicatorAfterDelay(GameObject go, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (go != null) go.SetActive(false);
        }

        // ── 상태 리셋 ─────────────────────────────────────────────
        /// <summary>
        /// 차지/방어 관련 플래그를 모두 초기 상태로 복구한다.
        /// OnDisable / 사망 / Release 소비 후 공통으로 호출.
        /// 방향성 방어도 함께 해제한다.
        /// </summary>
        private void ResetChargeState()
        {
            ResetChargeStateExceptGuard();
            // 방향성 방어 해제
            Model?.ClearDirectionalGuard();
        }

        /// <summary>
        /// 차지 플래그 / DamageMultiplier 만 리셋한다. 방향성 방어는 건드리지 않는다.
        /// Swipe 소비 시 ShieldSwingRoutine 이 방어를 관리하므로 이 버전을 사용한다.
        /// </summary>
        private void ResetChargeStateExceptGuard()
        {
            // 차지 Full 플래그 해제
            if (_isChargedFull)
            {
                _isChargedFull = false;
                Gesture?.SetChargedFull(false);
            }

            // 피격 데미지 배수 복구
            if (_isHolding)
            {
                _isHolding = false;
                Model?.SetDamageMultiplier(1f);
            }

            // 대지 분쇄 플래그 (Swipe 도중 중단된 경우)
            if (_isGroundSmashing)
            {
                EndSignatureSkill();
                _isGroundSmashing = false;
            }
        }
    }
}
