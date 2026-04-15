using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Enemies;

namespace Game.Characters
{
    /// <summary>
    /// 근접 캐릭터 공통 베이스.
    /// CharacterPresenterBase 를 상속하여 근접 박스 공격 + 인디케이터를 구현한다.
    ///
    /// [OnNormalAttack 구현]
    ///   HandleTap → OnNormalAttack → PerformMeleeAttack (박스 히트 + 인디케이터)
    ///   → OnPerformAttack 훅 (Assassin 잔상 동참 공격 등)
    ///
    /// [상속 캐릭터]
    ///   RapierPresenter, WarriorPresenter, AssassinPresenter
    /// </summary>
    public abstract class MeleePresenterBase : CharacterPresenterBase
    {
        // ── 상수 ──────────────────────────────────────────────────
        private const float ATTACK_INDICATOR_DURATION = 0.4f;
        private const float GizmoDuration             = 0.5f;

        // ── 인디케이터 ────────────────────────────────────────────
        private GameObject     _attackRangeIndicator;
        private SpriteRenderer _attackRangeSr;

        // ── Gizmo ─────────────────────────────────────────────────
        private Vector2 _lastAttackCenter;
        private Vector2 _lastAttackSize;
        private float   _lastAttackAngle;
        private bool    _showAttackGizmo;
        private float   _gizmoTimer;

        // ── OnNormalAttack 구현 ───────────────────────────────────
        /// <summary>
        /// 평상시 Tap → 근접 박스 공격 실행.
        /// </summary>
        protected override void OnNormalAttack(Vector2 screenPos)
        {
            StartCoroutine(MeleeAttackRoutine());
        }

        private IEnumerator MeleeAttackRoutine()
        {
            ShowAttackRangeIndicator();
            PerformMeleeAttack();
            yield return new WaitForSecondsRealtime(ATTACK_INDICATOR_DURATION);
            HideAttackRangeIndicator();
        }

        // ── 근접 공격 실행 ────────────────────────────────────────
        /// <summary>
        /// 근접 박스 히트 실행. normalAttackPercent 배율 적용.
        /// OnPerformAttack 훅을 먼저 호출해 잔상 동참 공격 등을 허용한다.
        /// </summary>
        protected void PerformMeleeAttack()
        {
            OnPerformAttack();

            var stat = Model.StatData;
            var nearest = FindNearestEnemy(30f);
            var dir = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
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
                damageable.TakeDamage(Model.AttackPower * (stat.normalAttackPercent / 100f), dir);
                OnHitDamageable(damageable);
                hitCount++;
            }
            Debug.Log($"[MeleeAttack] 히트: {hitCount}명");
        }

        // ── Update Gizmo 타이머 ───────────────────────────────────
        protected override void Update()
        {
            base.Update();
            if (_showAttackGizmo)
            {
                _gizmoTimer -= Time.deltaTime;
                if (_gizmoTimer <= 0f) _showAttackGizmo = false;
            }
        }

        // ── 인디케이터 유틸 ──────────────────────────────────────
        private void ShowAttackRangeIndicator()
        {
            if (_attackRangeIndicator == null) CreateAttackRangeIndicator();

            var stat    = Model.StatData;
            var nearest = FindNearestEnemy(30f);
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
            _attackRangeIndicator               = new GameObject("AttackRangeIndicator");
            _attackRangeSr                      = _attackRangeIndicator.AddComponent<SpriteRenderer>();
            _attackRangeSr.sprite               = CreateSquareSprite();
            _attackRangeSr.color                = new Color(1f, 1f, 0f, 0.25f);
            _attackRangeSr.sortingOrder         = 10;
            _attackRangeIndicator.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_showAttackGizmo) return;
            Gizmos.color  = new Color(1f, 1f, 0f, 0.4f);
            var rot       = Quaternion.Euler(0f, 0f, _lastAttackAngle);
            var oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(
                new Vector3(_lastAttackCenter.x, _lastAttackCenter.y, 0f), rot, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, new Vector3(_lastAttackSize.x, _lastAttackSize.y, 0.1f));
            Gizmos.color  = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_lastAttackSize.x, _lastAttackSize.y, 0.1f));
            Gizmos.matrix = oldMatrix;
            Gizmos.color  = Color.red;
            Gizmos.DrawSphere(new Vector3(_lastAttackCenter.x, _lastAttackCenter.y, 0f), 0.1f);
        }
#endif
    }
}
