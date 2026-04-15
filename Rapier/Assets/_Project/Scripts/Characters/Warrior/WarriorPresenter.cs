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
    ///   차지 Full  → _isChargedFull = true        (로컬 플래그)
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
    ///   _isChargedFull = true        ↔ _isChargedFull = false
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
    public class WarriorPresenter : MeleePresenterBase, IDamageable, IPlayerCharacter
    {
        // ── 직렬화 필드 ───────────────────────────────────────────
        [Header("데이터")]
        [SerializeField] private WarriorStatData _statData;

        // ── 런타임 비직렬화 필드 ──────────────────────────────────
        private CharacterView _view;

        /// <summary>현재 차지가 Full 에 도달했는지 여부.</summary>
        [NonSerialized] private bool _isChargedFull;

        /// <summary>현재 Hold 차지 중인지 여부. DamageMultiplier 복구 조건에 사용.</summary>
        [NonSerialized] private bool _isHolding;

        /// <summary>방패 휘두르기 코루틴 핸들. 사망/OnDisable 시 중단에 사용.</summary>
        [NonSerialized] private Coroutine _shieldSwingCoroutine;

        /// <summary>패링 성립 시 TriggerJustDodge 방향 계산용으로 보관하는 방패 노멀.</summary>
        [NonSerialized] private Vector2 _shieldNormalAtParry;

        /// <summary>대지 분쇄 중인지 플래그 (사망 경로에서 EndSignatureSkill 중복 호출 방지).</summary>
        [NonSerialized] private bool _isGroundSmashing;

        /// <summary>
        /// 현재 슬로우 구간에서 대지 분쇄를 이미 사용했는지 여부.
        /// 슬로우 1회당 대지 분쇄 1회 제한을 보장한다.
        /// HandleParry → TriggerJustDodge → HandleJustDodge 흐름에서 false 로 리셋된다.
        /// </summary>
        [NonSerialized] private bool _groundSmashUsed;

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

        protected override void OnDisable()
        {
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
        public bool IsAlive => CharacterIsAlive;

        /// <inheritdoc/>
        public void TakeDamage(float amount, Vector2 knockbackDir) => ProcessTakeDamage(amount, knockbackDir);

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
                Debug.Log("[WarriorPresenter] 차지 Full 도달");
            }
        }

        // ── Hold 확장 이벤트 override ────────────────────────────
        /// <summary>
        /// Hold 중 Swipe 감지 → 방패 휘두르기 루틴 시작.
        /// 차지 Full 전이면 무시.
        /// </summary>
        protected override void OnHoldSwipe(Vector2 direction)
        {
            if (Model == null || !Model.IsAlive) return;
            if (!_isChargedFull) return; // 차지 Full 전 → 무반응

            // 이전 방패 휘두르기가 아직 실행 중이면 먼저 중단
            if (_shieldSwingCoroutine != null)
            {
                StopCoroutine(_shieldSwingCoroutine);
                _shieldSwingCoroutine = null;
                Model.ClearDirectionalGuard();
            }

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
        protected override void OnHoldRelease(Vector2 fromStart)
        {
            if (Model == null || !Model.IsAlive) return;

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
            // 방향성 방어 활성 (패링 콜백용 방향 저장)
            _shieldNormalAtParry = direction;
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
        /// 패링 부채꼴(방향 ±ShieldGuardHalfAngle, 반경 attackOffset + attackHeight*0.5)과 동일한 범위를 사용한다.
        /// </summary>
        private void ExecuteShieldHit(Vector2 direction)
        {
            var stat       = _statData;
            float radius   = stat.attackOffset + stat.attackHeight * 0.5f;
            var origin     = (Vector2)transform.position;
            int enemyLayer = LayerMask.GetMask("Enemy");

            // 인디케이터 표시 — 패링 범위와 동일한 부채꼴, 빨간색
            ShowShieldFanIndicator(origin, direction, radius, stat.ShieldGuardHalfAngle, 0.35f);

            var hits     = Physics2D.OverlapCircleAll(origin, radius, enemyLayer);
            int hitCount = 0;
            float damage = Model.AttackPower * (stat.ShieldSwingDamagePercent / 100f);

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                // 패링과 동일한 부채꼴 각도 필터링
                Vector2 toEnemy = ((Vector2)hit.transform.position - origin).normalized;
                float dot       = Vector2.Dot(direction.normalized, toEnemy);
                float cosAngle  = Mathf.Cos(stat.ShieldGuardHalfAngle * Mathf.Deg2Rad);
                if (dot < cosAngle) continue; // 부채꼴 범위 밖

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

            var stat       = _statData;
            var origin     = (Vector2)transform.position;
            // 대지 분쇄: 플레이어 중심 원형, 기존 반경 × 1.5
            float radius   = (stat.attackOffset + stat.attackHeight * 0.5f) * 1.5f;
            int enemyLayer = LayerMask.GetMask("Enemy");

            ShowAoeRangeIndicator(origin, radius, 0.35f);

            float damage   = Model.AttackPower * (stat.ChargeDamagePercent / 100f)
                             * Model.SkillDamageMultiplier;

            var hits     = Physics2D.OverlapCircleAll(origin, radius, enemyLayer);
            int hitCount = 0;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;
                // 넉백 방향: 플레이어 → 적
                var knockDir = ((Vector2)hit.transform.position - origin);
                var dir      = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : Vector2.up;
                enemy.TakeDamage(damage, dir);
                hitCount++;
            }

            Debug.Log($"[WarriorPresenter] 대지 분쇄 히트: {hitCount}명 / 데미지: {damage:F0}");

            EndSignatureSkill();
            _isGroundSmashing = false;
        }

        // ── 저스트 회피 진입 훅 ──────────────────────────────────
        /// <summary>
        /// 패링 → TriggerJustDodge → HandleJustDodge 흐름에서 호출된다.
        /// 새 슬로우가 시작되므로 대지 분쇄 사용 플래그를 리셋한다.
        /// </summary>
        protected override void OnJustDodge(Vector2 direction)
        {
            _groundSmashUsed = false;
        }

        // ── 저스트 회피 슬로우 중 Tap → 대지 분쇄 ──────────────────
        /// <summary>
        /// 패링 후 슬로우 중 Tap → 대지 분쇄 발동.
        /// 슬로우 1회당 1번만 발동한다 (_groundSmashUsed 가드).
        /// </summary>
        protected override void OnJustDodgeTap()
        {
            if (Model == null || !Model.IsAlive) return;
            if (_groundSmashUsed) return;
            _groundSmashUsed = true;
            Debug.Log("[WarriorPresenter] JustDodge Tap → 대지 분쇄 발동");
            ExecuteGroundSmash();
        }

        // ── 패링 콜백 ─────────────────────────────────────────────
        /// <summary>
        /// 방향성 방어 패링 성립 → Base 의 저스트 회피 슬로우를 트리거한다.
        /// 슬로우 중 Tap 으로 대지 분쇄를 발동한다 (OnJustDodgeTap).
        /// </summary>
        private void HandleParry()
        {
            if (Model == null || !Model.IsAlive) return;
            Debug.Log("[WarriorPresenter] 패링 성립 → 저스트 회피 슬로우 진입");
            // 패링 방향은 방패 방향의 반대 (공격이 날아온 방향)로 전달
            TriggerJustDodge(_shieldNormalAtParry * -1f);
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

            _groundSmashUsed = false;

            // 방어/차지 상태 전부 리셋
            ResetChargeState();
        }

        // ── DodgeDash 완료 훅 ────────────────────────────────────
        protected override void OnDodgeDashComplete()
        {
            // Warrior 는 OnSwipe 에서 EnableJustDodge() 를 호출하지 않으므로
            // JustDodgeAvailable 은 항상 false — Base 기본 처리(이동 잠금 해제 등)만 수행.
            base.OnDodgeDashComplete();
        }

        // ── OnSkillRelease — Warrior 차지는 OnHoldRelease 에서 처리 ──
        protected override void OnSkillRelease(bool fullyCharged, bool justDodgeReady)
        {
            // 차지 스킬은 OnHoldRelease 에서 처리. 여기서는 아무것도 하지 않음.
        }

        // ── 인디케이터 유틸 ──────────────────────────────────────
        /// <summary>
        /// 부채꼴 인디케이터를 런타임 Texture2D로 생성해 duration 초 후 파괴한다.
        /// origin 기준, direction 방향 ±halfAngleDeg 범위, 반경 radius.
        /// </summary>
        private void ShowShieldFanIndicator(Vector2 origin, Vector2 direction,
                                            float radius, float halfAngleDeg, float duration)
        {
            const int texSize = 128;

            // 부채꼴 텍스처 생성 (흰색 부채꼴, 나머지 투명)
            var tex    = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[texSize * texSize];
            float cx   = texSize * 0.5f - 0.5f;
            float cy   = texSize * 0.5f - 0.5f;
            float rSq  = (texSize * 0.5f) * (texSize * 0.5f);
            float cosA = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);

            // 텍스처 좌표계: +Y = 위 = 방패 기준 "전방"
            // 부채꼴 방향은 항상 +Y 로 그리고 GO 회전으로 direction 에 맞춤
            for (int y = 0; y < texSize; y++)
            for (int x = 0; x < texSize; x++)
            {
                float dx = x - cx, dy = y - cy;
                float distSq = dx * dx + dy * dy;
                if (distSq > rSq) { pixels[y * texSize + x] = new Color32(0,0,0,0); continue; }
                if (distSq < 0.01f) { pixels[y * texSize + x] = new Color32(255,255,255,255); continue; }
                // +Y 축 기준 dot
                float len = Mathf.Sqrt(distSq);
                float dotVal = dy / len; // dot((dx,dy).normalized, (0,1))
                pixels[y * texSize + x] = dotVal >= cosA
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize),
                                       new Vector2(0.5f, 0.5f), texSize);

            // GO 배치: origin 기준, direction 을 +Y 로 회전
            var go = new GameObject("ShieldFanIndicator");
            go.transform.position   = new Vector3(origin.x, origin.y, 0f);
            // Vector2.up → direction 으로의 회전
            float angle = Vector2.SignedAngle(Vector2.up, direction);
            go.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.color        = new Color(1f, 0f, 0f, 0.40f); // 빨강 반투명
            sr.sortingOrder = 11;

            Destroy(go, duration);
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
