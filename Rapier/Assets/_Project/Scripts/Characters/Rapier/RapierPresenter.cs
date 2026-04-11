using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Characters
{
    /// <summary>
    /// 레이피어 캐릭터 Presenter.
    ///
    /// [입력 차단 — Base가 단독 관리]
    ///   회피 대시 / 저스트 회피 슬로우 / 차지 스킬 Tap 차단은 모두 Base가 플래그로 관리한다.
    ///   Rapier는 고유 스킬 시퀀스에 한해 BeginSignatureSkill() / EndSignatureSkill() 훅으로만
    ///   Base에 신호를 보낸다. 자식은 차단 플래그를 직접 읽거나 쓰지 않는다.
    ///
    /// [고유 스킬 시퀀스 — 지연 진입 모델]
    ///   OnJustDodge : 이 시점에 표식 대시 스킬을 "예약"만 한다. _skillTarget 만 캐싱하고
    ///     BeginSignatureSkill / LockMovement 는 호출하지 않는다. 이유는 사용자가 슬로우
    ///     구간 내에 Hold/Release 를 하지 않고 그냥 흘려보낼 수 있기 때문이며, 그 경우
    ///     스킬 시퀀스 자체가 시작되지 않아야 한다 (회귀 버그 방지 — 영구 잠금 차단).
    ///   OnSkillRelease(justDodgeReady=true, 타겟 생존) :
    ///     여기서 비로소 BeginSignatureSkill() + LockMovement() 를 호출하고 DashSkillRoutine 시작.
    ///     - 대시 → 공격 → 복귀 전 구간 무적/Tap 차단
    ///     - 복귀 완료 시 EndSignatureSkillCleanup() = EndSignatureSkill() + 무적 OFF + FreeMovement
    ///   OnSlowMotionEnd : 사용자가 슬로우 동안 Hold/Release 하지 않았다면 Base 가
    ///     _isJustDodgeSlowActive 와 Model.IsJustDodgeReady 를 내리고,
    ///     RapierPresenter 는 _skillTarget 만 정리한 뒤 평범한 상태로 복귀한다.
    ///     (스킬 시퀀스가 이미 시작된 경우엔 DashSkillRoutine 이 _skillTarget 을 사용 중이므로 보존)
    ///   OnSkillRelease(fullyCharged=true): 차지 스킬은 동기 실행이며 Base 가 _isChargeSkillActive
    ///     로 실행 구간을 자동 차단한다.
    /// </summary>
    public class RapierPresenter : CharacterPresenterBase, IDamageable, IPlayerCharacter
    {
        [Header("데이터")]
        [SerializeField] private RapierStatData _statData;

        private CharacterView _view;

        // ── 표식 테이블 ───────────────────────────────────────────
        private readonly Dictionary<EnemyPresenterBase, int> _markTable
            = new Dictionary<EnemyPresenterBase, int>();

        public event Action<EnemyPresenterBase, int> OnMarkChanged;

        // ── 스킬 상태 ─────────────────────────────────────────────
        private EnemyPresenterBase _skillTarget;

        // ── 스킬 공격 범위 인디케이터 ─────────────────────────────
        private GameObject     _skillRangeIndicator;
        private SpriteRenderer _skillRangeSr;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            _view = GetComponent<CharacterView>();

            if (_statData == null)
            {
                Debug.LogError("[RapierPresenter] RapierStatData가 할당되지 않음.");
                return;
            }

            Init(_statData, _view);

            if (_statData.sprite != null)
                _view.SetSprite(_statData.sprite);

            ServiceLocator.Register<IPlayerCharacter>(this);
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ServiceLocator.Unregister<IPlayerCharacter>();
            ServiceLocator.Unregister<RapierPresenter>();
            ClearAllMarks();
        }

        // ── IDamageable / IPlayerCharacter ────────────────────────
        public bool IsAlive => Model != null && Model.IsAlive;

        public void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;

            if (JustDodgeAvailable)
            {
                ConsumeJustDodge();
                Debug.Log("[RapierPresenter] 회피 중 피격 → 저스트 회피 발동!");
                Gesture?.TriggerJustDodge(knockbackDir * -1f);
                return;
            }

            if (Model.IsInvincible) return;

            Model.TakeDamage(amount);
            View.PlayHit();
        }

        public CharacterModel PublicModel => Model;

        // ── DodgeDash 완료 콜백 ───────────────────────────────────
        protected override void OnDodgeDashComplete()
        {
            // 저스트 회피가 일어나지 않은 평범한 회피라면 Base의 기본 처리로 충분.
            // (Base는 _isJustDodgeSlowActive, _isSignatureSkillActive를 확인한 뒤
            //  해당 구간이 아니면 무적/이동 잠금을 해제한다.)
            base.OnDodgeDashComplete();
            ConsumeJustDodge();
        }

        // ── 저스트 회피 훅 ────────────────────────────────────────
        /// <summary>
        /// 저스트 회피가 발동된 순간의 훅.
        /// 여기서는 "표식 대시 스킬 후보 타겟"만 캐싱한다 — BeginSignatureSkill 및 LockMovement 는
        /// 호출하지 않는다. 사용자가 슬로우 구간 내에 실제로 Hold/Release 를 해서
        /// <see cref="OnSkillRelease"/>가 저스트 회피 분기로 진입할 때 비로소 시그니처 스킬을 시작한다.
        /// 사용자가 슬로우 동안 아무 입력도 하지 않으면 시그니처 스킬 자체가 시작되지 않으므로
        /// OnSlowMotionEnd 에서 평범한 상태로 복귀할 수 있다 (영구 잠금 회귀 방지).
        /// </summary>
        protected override void OnJustDodge(Vector2 direction)
        {
            _skillTarget = FindNearestEnemy(30f);

            if (_skillTarget != null)
                Debug.Log($"[RapierPresenter] 스킬 시퀀스 후보 캐싱: {_skillTarget.name}");
        }

        // ── 슬로우모션 종료 훅 ────────────────────────────────────
        /// <summary>
        /// 슬로우 종료 시 Base 후처리 (JustDodgeReady 만료, 슬로우 플래그 OFF, 무적 해제)를 먼저 수행하고,
        /// 시그니처 스킬 시퀀스가 아직 시작되지 않은 경우에 한해 캐싱한 _skillTarget 을 비운다.
        /// 스킬 시퀀스가 이미 시작되었다면 DashSkillRoutine 이 _skillTarget 을 사용 중이므로 건드리지 않는다.
        /// </summary>
        protected override void OnSlowMotionEnd()
        {
            base.OnSlowMotionEnd();

            if (!IsSignatureSkillActive)
                _skillTarget = null;
        }

        // ── 사망 전처리 훅 ───────────────────────────────────────
        protected override void OnBeforeDeath()
        {
            if (_skillRangeIndicator != null)
                _skillRangeIndicator.SetActive(false);
            _skillTarget = null;
        }

        // ── 스킬 발동 훅 ──────────────────────────────────────────
        /// <summary>
        /// Hold → Release 시 Base 에서 호출된다.
        /// 저스트 회피 발동권이 살아 있고 캐싱된 타겟이 생존해 있을 때만 표식 대시 스킬로 진입하며,
        /// 이 분기 안에서 비로소 BeginSignatureSkill() + LockMovement() 를 호출한다.
        /// 이렇게 하면 BeginSignatureSkill 호출 시점과 DashSkillRoutine 실행이 1:1 로 묶여
        /// cleanup 누락에 의한 영구 잠금이 구조적으로 불가능하다.
        /// </summary>
        protected override void OnSkillRelease(bool fullyCharged, bool justDodgeReady)
        {
            if (justDodgeReady && _skillTarget != null && _skillTarget.IsAlive)
            {
                // 표식 대시 스킬 시퀀스 시작. BeginSignatureSkill / LockMovement 는 여기서만 호출된다.
                BeginSignatureSkill();
                LockMovement();
                StartCoroutine(DashSkillRoutine(_skillTarget));
            }
            else if (fullyCharged)
            {
                // 순수 차지 스킬. Base 가 _isChargeSkillActive 로 실행 구간을 자동 차단한다.
                ExecuteChargeSkill();
            }

            _skillTarget = null;
        }

        /// <summary>
        /// 고유 스킬 시퀀스 종료 시 공통 뒷정리.
        /// Base의 EndSignatureSkill을 호출하고 무적/이동잠금을 해제한다.
        /// </summary>
        private void EndSignatureSkillCleanup()
        {
            EndSignatureSkill();
            Model.SetInvincible(false);
            FreeMovement();
        }

        // ── 고유 스킬: 대시 → 범위 공격 + 표식 → DodgeDest 복귀 ─
        private IEnumerator DashSkillRoutine(EnemyPresenterBase target)
        {
            yield return StartCoroutine(DashTo((Vector2)target.transform.position, _statData.skillDashSpeed));

            PerformSkillAttack();

            yield return StartCoroutine(DashTo(DodgeDest, _statData.skillReturnSpeed));

            EndSignatureSkillCleanup();
        }

        // ── 스킬 공격 ─────────────────────────────────────────────
        private void PerformSkillAttack()
        {
            EnemyPresenterBase nearest = FindNearestEnemy(30f);

            var dir = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            var   boxCenter  = (Vector2)transform.position + dir * _statData.skillAttackOffset;
            var   boxSize    = new Vector2(_statData.skillAttackWidth, _statData.skillAttackHeight);
            float angle      = Vector2.SignedAngle(Vector2.up, dir);
            int   enemyLayer = LayerMask.GetMask("Enemy");

            ShowSkillRangeIndicator(boxCenter, boxSize, angle);
            StartCoroutine(HideSkillIndicatorAfterDelay(0.35f));

            var hits     = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, enemyLayer);
            int hitCount = 0;
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.TakeDamage(Model.AttackPower, dir);
                AddMark(enemy);
                hitCount++;
            }
            Debug.Log($"[RapierPresenter] 스킬 공격 히트: {hitCount}명");
        }

        private void ShowSkillRangeIndicator(Vector2 center, Vector2 size, float angle)
        {
            if (_skillRangeIndicator == null)
            {
                _skillRangeIndicator       = new GameObject("SkillRangeIndicator");
                _skillRangeSr              = _skillRangeIndicator.AddComponent<SpriteRenderer>();
                _skillRangeSr.sprite       = CreateSquareSprite();
                _skillRangeSr.color        = new Color(1f, 0f, 0f, 0.35f);
                _skillRangeSr.sortingOrder = 11;
                _skillRangeIndicator.SetActive(false);
            }

            _skillRangeIndicator.transform.position   = new Vector3(center.x, center.y, 0f);
            _skillRangeIndicator.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
            _skillRangeIndicator.transform.localScale = new Vector3(size.x, size.y, 1f);
            _skillRangeIndicator.SetActive(true);
        }

        private IEnumerator HideSkillIndicatorAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (_skillRangeIndicator != null)
                _skillRangeIndicator.SetActive(false);
        }

        private IEnumerator DashTo(Vector2 destination, float speed)
        {
            while (Vector2.Distance(transform.position, destination) > 0.05f)
            {
                var next = Vector2.MoveTowards(
                    transform.position, destination, speed * Time.unscaledDeltaTime);
                View.SetPosition(next);
                yield return null;
            }
            View.SetPosition(destination);
        }

        // ── 차지 스킬 ─────────────────────────────────────────────
        private void ExecuteChargeSkill()
        {
            if (_markTable.Count == 0)
            {
                Debug.Log("[RapierPresenter] 차지 스킬: 표식 보유 적 없음.");
                return;
            }

            var snapshot = new List<KeyValuePair<EnemyPresenterBase, int>>(_markTable);
            foreach (var kvp in snapshot)
            {
                var enemy  = kvp.Key;
                var stacks = kvp.Value;
                if (!enemy.IsAlive) continue;

                float damage = stacks * Model.AttackPower * _statData.chargeMarkMultiplier;
                var   dir    = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.TakeDamage(damage, dir);
                Debug.Log($"[RapierPresenter] 차지 스킬 — {enemy.name}: {stacks}중첩 × {damage:F0} 데미지");
            }

            ClearAllMarks();
        }

        // ── 표식 관리 ─────────────────────────────────────────────
        private void AddMark(EnemyPresenterBase enemy)
        {
            if (!enemy.IsAlive) return;

            _markTable.TryGetValue(enemy, out int current);
            int newCount = Mathf.Min(current + 1, _statData.markMaxStack);
            _markTable[enemy] = newCount;

            if (current == 0)
                enemy.OnDeath += () => RemoveMark(enemy);

            OnMarkChanged?.Invoke(enemy, newCount);
            Debug.Log($"[RapierPresenter] 표식 부여: {enemy.name} → {newCount}중첩");
        }

        private void RemoveMark(EnemyPresenterBase enemy)
        {
            if (!_markTable.ContainsKey(enemy)) return;
            _markTable.Remove(enemy);
            OnMarkChanged?.Invoke(enemy, 0);
        }

        private void ClearAllMarks()
        {
            foreach (var kvp in _markTable)
                OnMarkChanged?.Invoke(kvp.Key, 0);
            _markTable.Clear();
        }
    }
}
