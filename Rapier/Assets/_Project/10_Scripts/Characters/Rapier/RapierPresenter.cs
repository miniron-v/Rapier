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
    /// [고유 스킬 시퀀스]
    ///   OnJustDodge : 타겟 확보 후 BeginSignatureSkill() — 슬로우 종료 후에도 Tap/무적/이동잠금 유지
    ///   OnSkillRelease(justDodgeReady=true) : 타겟 생존 시 DashSkillRoutine 시작
    ///     - 대시 → 공격 → 복귀 전 구간 무적/Tap 차단
    ///     - 복귀 완료 시 EndSignatureSkill() + 무적 OFF + FreeMovement
    ///   OnSlowMotionEnd : 타겟이 없어 스킬 시퀀스가 시작되지 않았고 사용자가 Hold를 안 했다면
    ///     Base가 _isJustDodgeSlowActive를 내리며, BeginSignatureSkill이 호출되지 않았으므로
    ///     RapierPresenter도 추가로 정리할 것이 없다.
    ///   OnSkillRelease(fullyCharged=true): 차지 스킬은 동기 실행이며 Base가 _isChargeSkillActive로
    ///     실행 구간을 자동 차단한다.
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
        protected override void OnJustDodge(Vector2 direction)
        {
            var waveManager = ServiceLocator.Get<WaveManager>();
            _skillTarget = waveManager?.GetNearestEnemy(transform.position);

            if (_skillTarget == null)
            {
                var bossRushManager = ServiceLocator.Get<BossRushManager>();
                _skillTarget = bossRushManager?.GetCurrentBoss();
            }

            if (_skillTarget != null)
            {
                // 고유 스킬 시퀀스 대기 — Base에게 Tap 차단/무적/이동잠금 유지를 요청.
                BeginSignatureSkill();
                LockMovement();
                Debug.Log($"[RapierPresenter] 스킬 시퀀스 대기: {_skillTarget.name}");
            }
        }

        // ── 스킬 발동 훅 ──────────────────────────────────────────
        protected override void OnSkillRelease(bool fullyCharged, bool justDodgeReady)
        {
            if (justDodgeReady && _skillTarget != null && _skillTarget.IsAlive)
            {
                // 대시 시퀀스 시작. BeginSignatureSkill은 이미 OnJustDodge에서 호출됨.
                StartCoroutine(DashSkillRoutine(_skillTarget));
            }
            else if (fullyCharged)
            {
                // 저스트 회피로 진입했지만 타겟이 없거나, 순수 차지 스킬인 경우.
                // 만약 저스트 회피 대기 상태가 있었다면(BeginSignatureSkill 호출됨) 정리한다.
                if (IsSignatureSkillActive) EndSignatureSkillCleanup();
                ExecuteChargeSkill();
            }
            else
            {
                // Hold 직후 Release지만 차지가 안 차고 저스트 회피 타겟도 없는 경우.
                if (IsSignatureSkillActive) EndSignatureSkillCleanup();
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
            EnemyPresenterBase nearest = null;

            var waveManager = ServiceLocator.Get<WaveManager>();
            if (waveManager != null)
                nearest = waveManager.GetNearestEnemy(transform.position);

            if (nearest == null)
            {
                var bossRushManager = ServiceLocator.Get<BossRushManager>();
                nearest = bossRushManager?.GetCurrentBoss();
            }

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

                float damage = stacks * _statData.attackPower * _statData.chargeMarkMultiplier;
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
