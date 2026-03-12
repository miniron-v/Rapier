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
    ///   1. 고유 스킬 (저스트 회피 → 스킬 발동):
    ///      타겟 방향으로 skillDashSpeed 이동 → 사각형 범위 내 모든 적에게 데미지 + 표식
    ///      → DodgeDest로 skillReturnSpeed 복귀
    ///   2. 차지 스킬:
    ///      표식 보유 적 전체를 중첩 수 × (attackPower × chargeMarkMultiplier) 데미지 → 표식 소비
    ///
    /// [CanAttack 사용]
    ///   Base의 protected bool CanAttack.
    ///   스킬 대시 시작 시 false → 복귀 완료 시 true.
    ///   HandleTap에서 CanAttack == false면 일반 공격 차단.
    ///   동시에 무적 억제 조건으로도 사용 (OnDodgeDashComplete / OnSlowMotionEnd override).
    ///
    /// [스킬 공격 범위]
    ///   RapierStatData.skillAttackWidth/Height/Offset — 일반 공격 범위와 완전 독립.
    ///   스킬 발동 시 빨간 사각형 인디케이터 표시.
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

        /// <summary>표식 변경 시 발행. (적 Presenter, 현재 중첩 수) — 0이면 제거.</summary>
        public event Action<EnemyPresenter, int> OnMarkChanged;

        // ── 스킬 상태 ─────────────────────────────────────────────
        private EnemyPresenter _skillTarget;
        private bool           _skillPending;  // DodgeDash 완료 후 스킬 발동 대기 중
        // CanAttack (Base) == false : 스킬 대시~복귀 구간. 일반 공격 차단 + 무적 억제.

        // ── 스킬 범위 인디케이터 (빨간, 레이피어 전용) ────────────
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
            if (_skillRangeIndicator != null)
                Destroy(_skillRangeIndicator);
        }

        // ── IDamageable / IPlayerCharacter ────────────────────────
        public bool IsAlive => Model != null && Model.IsAlive;

        public void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;
            if (Model.IsInvincible)
            {
                Debug.Log("[RapierPresenter] 무적 중 피격 → JustDodge 트리거!");
                Gesture?.ForceJustDodge(knockbackDir * -1f);
                return;
            }
            Model.TakeDamage(amount);
            View.PlayHit();
        }

        public CharacterModel PublicModel => Model;

        // ── DodgeDash 완료 콜백 override ─────────────────────────
        protected override void OnDodgeDashComplete()
        {
            if (_skillPending)
            {
                // 스킬 대기 중 → Locked + 무적 유지
                return;
            }
            Model.SetInvincible(false);
            FreeMovement();
        }

        // ── 슬로우 종료 콜백 override ─────────────────────────────
        protected override void OnSlowMotionEnd()
        {
            // 스킬 대시 중(CanAttack == false)이면 무적 유지
            if (!CanAttack) return;
            Model?.SetInvincible(false);
        }

        // ── 저스트 회피 훅 ────────────────────────────────────────
        protected override void OnJustDodge(Vector2 direction)
        {
            var waveManager = ServiceLocator.Get<WaveManager>();
            _skillTarget  = waveManager?.GetNearestEnemy(transform.position);
            _skillPending = _skillTarget != null;

            if (_skillPending)
                Debug.Log($"[RapierPresenter] 스킬 대기: {_skillTarget.name}");
        }

        // ── 스킬 발동 훅 ──────────────────────────────────────────
        protected override void OnSkillRelease(bool fullyCharged, bool justDodgeReady)
        {
            if (justDodgeReady && _skillTarget != null && _skillTarget.IsAlive)
            {
                _skillPending = false;
                StartCoroutine(DashSkillRoutine(_skillTarget));
            }
            else if (fullyCharged)
            {
                _skillPending = false;
                Model.SetInvincible(false);
                FreeMovement();
                ExecuteChargeSkill();
            }
            else
            {
                _skillPending = false;
                Model.SetInvincible(false);
                FreeMovement();
            }

            _skillTarget = null;
        }

        // ── 고유 스킬: 대시 → 범위 공격 + 표식 → DodgeDest 복귀 ─
        private IEnumerator DashSkillRoutine(EnemyPresenter target)
        {
            CanAttack = false;  // 일반 공격 차단 시작

            // 1. 타겟 위치로 skillDashSpeed 이동
            yield return StartCoroutine(DashTo((Vector2)target.transform.position, _statData.skillDashSpeed));

            // 2. 범위 공격 + 표식 + 빨간 인디케이터
            ShowSkillRangeIndicator();
            PerformSkillAttack();
            yield return new WaitForSeconds(0.15f);  // 인디케이터 잠깐 표시
            HideSkillRangeIndicator();

            // 3. DodgeDest로 복귀
            yield return StartCoroutine(DashTo(DodgeDest, _statData.skillReturnSpeed));

            // 4. 완료 → 무적 해제 + Free + 공격 재허용
            CanAttack = true;
            Model.SetInvincible(false);
            FreeMovement();
        }

        /// <summary>
        /// 스킬 공격 — 레이피어 전용 사각형 범위(skillAttackWidth/Height/Offset) 내 모든 적에게
        /// attackPower 데미지 + 표식 1중첩 부여.
        /// 일반 공격(attackWidth/Height/Offset)과 완전히 독립.
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

        /// <summary>
        /// 대시 이동 코루틴.
        /// Time.unscaledDeltaTime 사용 — 슬로우모션에 영향받지 않음.
        /// </summary>
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

        // ── 스킬 범위 인디케이터 (빨간, 레이피어 전용) ────────────
        private void ShowSkillRangeIndicator()
        {
            if (_skillRangeIndicator == null) CreateSkillRangeIndicator();

            var nearest = ServiceLocator.Get<WaveManager>()?.GetNearestEnemy(transform.position);
            var dir     = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            var   boxCenter = (Vector2)transform.position + dir * _statData.skillAttackOffset;
            float angle     = Vector2.SignedAngle(Vector2.up, dir);

            _skillRangeIndicator.transform.position   = new Vector3(boxCenter.x, boxCenter.y, 0f);
            _skillRangeIndicator.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
            _skillRangeIndicator.transform.localScale = new Vector3(
                _statData.skillAttackWidth, _statData.skillAttackHeight, 1f);
            _skillRangeIndicator.SetActive(true);
        }

        private void HideSkillRangeIndicator()
        {
            if (_skillRangeIndicator != null)
                _skillRangeIndicator.SetActive(false);
        }

        private void CreateSkillRangeIndicator()
        {
            _skillRangeIndicator       = new GameObject("SkillRangeIndicator");
            _skillRangeSr              = _skillRangeIndicator.AddComponent<SpriteRenderer>();
            _skillRangeSr.sprite       = CreateSquareSprite();
            _skillRangeSr.color        = new Color(1f, 0.1f, 0.1f, 0.35f);  // 빨간
            _skillRangeSr.sortingOrder = 10;
            _skillRangeIndicator.SetActive(false);
        }

        private Sprite CreateSquareSprite()
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
