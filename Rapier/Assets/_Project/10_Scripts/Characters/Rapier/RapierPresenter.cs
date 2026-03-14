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
    /// [고유 메커니즘]
    ///   1. 고유 스킬 (저스트 회피 → Hold → Release):
    ///      타겟 적 방향으로 skillDashSpeed 이동
    ///      → 사각형 범위(skillAttack*) 내 모든 적에게 데미지 + 표식 부여
    ///      → DodgeDest로 skillReturnSpeed 복귀
    ///   2. 차지 스킬:
    ///      표식 보유 적 전체를 중첩 × (attackPower × chargeMarkMultiplier) 데미지 → 표식 소비
    ///
    /// [플래그 구조]
    ///   _isSkillSequenceActive:
    ///     OnJustDodge에서 타겟 확보 시 true
    ///     스킬 복귀 완료 또는 조건 불충족 시 false
    ///     → _skillPending과 _isDashSkillActive를 하나로 통합
    ///
    /// [CanAttack]
    ///   base.CanAttack (!_isDodging) AND !_isSkillSequenceActive
    ///   → 회피 대시 중, 스킬 대기/대시/복귀 중 모두 공격 차단
    ///
    /// [_isDodging 제어]
    ///   OnDodgeDashComplete() override:
    ///     _isSkillSequenceActive이면 SetDodging(false) 억제 (공격 차단 유지)
    ///     스킬 복귀 완료 시 SetDodging(false) 직접 호출
    /// </summary>
    public class RapierPresenter : CharacterPresenterBase, IDamageable, IPlayerCharacter
    {
        [Header("데이터")]
        [SerializeField] private RapierStatData _statData;

        // ── 내부 참조 ─────────────────────────────────────────────
        private CharacterView _view;

        // ── 표식 테이블 ───────────────────────────────────────────
        private readonly Dictionary<EnemyPresenter, int> _markTable
            = new Dictionary<EnemyPresenter, int>();

        public event Action<EnemyPresenter, int> OnMarkChanged;

        // ── 스킬 상태 ─────────────────────────────────────────────
        private EnemyPresenter _skillTarget;

        /// <summary>
        /// 저스트 회피 연계 스킬 진행 중 플래그.
        /// OnJustDodge에서 타겟 확보 시 true.
        /// 스킬 복귀 완료 또는 조건 불충족 시 false.
        /// (기존 _skillPending + _isDashSkillActive 통합)
        /// </summary>
        private bool _isSkillSequenceActive;

        // ── 스킬 공격 범위 인디케이터 (빨간색) ────────────────────
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

        private void OnDestroy()
        {
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
                // 회피 대시 중 피격 → 저스트 회피 발동 (한 회피당 한 번만)
                ConsumeJustDodge();
                Debug.Log("[RapierPresenter] 회피 중 피격 → 저스트 회피 발동!");
                Gesture?.TriggerJustDodge(knockbackDir * -1f);
                return;
            }

            if (Model.IsInvincible) return; // 무적 구간(슬로우/스킬 대시 중) → 피해 무시

            Model.TakeDamage(amount);
            View.PlayHit();
        }

        public CharacterModel PublicModel => Model;

        // ── CanAttack override ────────────────────────────────────
        /// <summary>
        /// 회피 대시 중(_isDodging) 또는 스킬 시퀀스 진행 중이면 공격 차단.
        /// </summary>
        protected override bool CanAttack => base.CanAttack && !_isSkillSequenceActive;

        // ── DodgeDash 완료 콜백 ───────────────────────────────────
        protected override void OnDodgeDashComplete()
        {
            ConsumeJustDodge();

            if (_isSkillSequenceActive)
            {
                // 스킬 시퀀스 진행 예정 → _isDodging 유지 (공격 차단 유지), 무적 유지
                return;
            }

            SetDodging(false);
            Model.SetInvincible(false);
            FreeMovement();
        }

        // ── 슬로우 종료 콜백 ──────────────────────────────────────
        protected override void OnSlowMotionEnd()
        {
            if (_isSkillSequenceActive) return; // 스킬 대시 중 → 무적 유지
            Model?.SetInvincible(false);
        }

        // ── 저스트 회피 훅 ────────────────────────────────────────
        protected override void OnJustDodge(Vector2 direction)
        {
            var waveManager = ServiceLocator.Get<WaveManager>();
            _skillTarget = waveManager?.GetNearestEnemy(transform.position);

            if (_skillTarget != null)
            {
                _isSkillSequenceActive = true;
                Debug.Log($"[RapierPresenter] 스킬 시퀀스 시작: {_skillTarget.name}");
            }
        }

        // ── 스킬 발동 훅 ──────────────────────────────────────────
        protected override void OnSkillRelease(bool fullyCharged, bool justDodgeReady)
        {
            if (justDodgeReady && _skillTarget != null && _skillTarget.IsAlive)
            {
                StartCoroutine(DashSkillRoutine(_skillTarget));
            }
            else if (fullyCharged)
            {
                _isSkillSequenceActive = false;
                SetDodging(false);
                Model.SetInvincible(false);
                FreeMovement();
                ExecuteChargeSkill();
            }
            else
            {
                _isSkillSequenceActive = false;
                SetDodging(false);
                Model.SetInvincible(false);
                FreeMovement();
            }

            _skillTarget = null;
        }

        // ── 고유 스킬: 대시 → 범위 공격 + 표식 → DodgeDest 복귀 ─
        private IEnumerator DashSkillRoutine(EnemyPresenter target)
        {
            yield return StartCoroutine(DashTo((Vector2)target.transform.position, _statData.skillDashSpeed));

            PerformSkillAttack();

            yield return StartCoroutine(DashTo(DodgeDest, _statData.skillReturnSpeed));

            // 스킬 시퀀스 완료 → 모든 차단 해제
            _isSkillSequenceActive = false;
            SetDodging(false);
            Model.SetInvincible(false);
            FreeMovement();
        }

        /// <summary>
        /// 스킬 공격 — 레이피어 전용 사각형 범위 내 모든 적에게 데미지 + 표식.
        /// 일반 공격 범위(attackWidth/Height/Offset)와 완전히 독립.
        /// </summary>
        private void PerformSkillAttack()
        {
            var waveManager = ServiceLocator.Get<WaveManager>();
            var nearest     = waveManager?.GetNearestEnemy(transform.position);
            var dir         = nearest != null
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
                var enemy = hit.GetComponent<EnemyPresenter>();
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.TakeDamage(_statData.attackPower, dir);
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

        /// <summary>대시 이동. Time.unscaledDeltaTime — 슬로우모션 영향 없음.</summary>
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

            var snapshot = new List<KeyValuePair<EnemyPresenter, int>>(_markTable);
            foreach (var kvp in snapshot)
            {
                var enemy  = kvp.Key;
                var stacks = kvp.Value;
                if (!enemy.IsAlive) continue;

                float damage = stacks * _statData.attackPower * _statData.chargeMarkMultiplier;
                var   dir    = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.TakeDamage(damage, dir);
                Debug.Log($"[RapierPresenter] 차지 스킬 — {enemy.name}: {stacks}중첩 × {damage:F0} 데미지");
            }

            ClearAllMarks();
        }

        // ── 표식 관리 ─────────────────────────────────────────────
        private void AddMark(EnemyPresenter enemy)
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

        private void RemoveMark(EnemyPresenter enemy)
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
