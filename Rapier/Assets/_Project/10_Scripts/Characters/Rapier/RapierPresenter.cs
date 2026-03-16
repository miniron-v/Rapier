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
    /// [플래그 구조 — 수정됨]
    ///   _isSkillSequenceActive : OnJustDodge에서 타겟 확보 시 true.
    ///                            스킬 복귀 완료 또는 조건 불충족 시 false.
    ///   _dashSkillStarted      : DashSkillRoutine이 실제로 시작됐을 때 true.
    ///                            OnDodgeDashComplete에서 억제 여부 판단에 사용.
    ///
    /// [버그 수정]
    ///   기존: _isSkillSequenceActive만으로 OnDodgeDashComplete 억제
    ///         → 저스트 회피 후 스킬 대시가 시작되지 않아도 억제되어 이동/공격 영구 잠금
    ///   수정: _dashSkillStarted가 true일 때만 억제
    ///         → 스킬 대시가 실제로 시작된 경우에만 잠금 유지
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

        /// <summary>
        /// 저스트 회피 연계 스킬 대기/진행 중 플래그.
        /// OnJustDodge에서 타겟 확보 시 true.
        /// </summary>
        private bool _isSkillSequenceActive;

        /// <summary>
        /// DashSkillRoutine이 실제로 StartCoroutine된 경우 true.
        /// OnDodgeDashComplete에서 억제 여부를 결정하는 데만 사용.
        /// </summary>
        private bool _dashSkillStarted;

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

        // ── CanAttack override ────────────────────────────────────
        protected override bool CanAttack => base.CanAttack && !_isSkillSequenceActive;

        // ── DodgeDash 완료 콜백 ───────────────────────────────────
        protected override void OnDodgeDashComplete()
        {
            ConsumeJustDodge();

            // _dashSkillStarted: DashSkillRoutine이 실제로 시작된 경우에만 억제
            // _isSkillSequenceActive만으로 억제하면, 스킬 대시가 시작 안 됐는데
            // 잠금이 해제되지 않는 버그 발생
            if (_dashSkillStarted)
                return;

            // 스킬 대기만 하고 대시가 안 시작된 경우 → 모두 해제
            _isSkillSequenceActive = false;
            SetDodging(false);
            Model.SetInvincible(false);
            FreeMovement();
        }

        // ── 슬로우 종료 콜백 ──────────────────────────────────────
protected override void OnSlowMotionEnd()
        {
            // 대시 스킬이 실제 시작된 경우에만 무적 유지
            if (_dashSkillStarted) return;

            // 슬로우 종료 시 스킬 대기 상태도 함께 해제
            // Hold 없이 슬로우가 끝난 경우 영구 잠금 방지
            if (_isSkillSequenceActive)
            {
                _isSkillSequenceActive = false;
                _skillTarget           = null;
                Debug.Log("[RapierPresenter] 슬로우 종료 → 스킬 시퀀스 대기 해제");
            }

            Model?.SetInvincible(false);
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
                _isSkillSequenceActive = true;
                _dashSkillStarted      = false; // 대시는 아직 시작 안 됨
                Debug.Log($"[RapierPresenter] 스킬 시퀀스 대기: {_skillTarget.name}");
            }
        }

        // ── 스킬 발동 훅 ──────────────────────────────────────────
        protected override void OnSkillRelease(bool fullyCharged, bool justDodgeReady)
        {
            if (justDodgeReady && _skillTarget != null && _skillTarget.IsAlive)
            {
                _dashSkillStarted = true; // 대시 실제 시작
                StartCoroutine(DashSkillRoutine(_skillTarget));
            }
            else if (fullyCharged)
            {
                ResetSkillState();
                ExecuteChargeSkill();
            }
            else
            {
                ResetSkillState();
            }

            _skillTarget = null;
        }

        // ── 스킬 상태 초기화 헬퍼 ────────────────────────────────
        private void ResetSkillState()
        {
            _isSkillSequenceActive = false;
            _dashSkillStarted      = false;
            SetDodging(false);
            Model.SetInvincible(false);
            FreeMovement();
        }

        // ── 고유 스킬: 대시 → 범위 공격 + 표식 → DodgeDest 복귀 ─
        private IEnumerator DashSkillRoutine(EnemyPresenterBase target)
        {
            yield return StartCoroutine(DashTo((Vector2)target.transform.position, _statData.skillDashSpeed));

            PerformSkillAttack();

            yield return StartCoroutine(DashTo(DodgeDest, _statData.skillReturnSpeed));

            ResetSkillState();
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
