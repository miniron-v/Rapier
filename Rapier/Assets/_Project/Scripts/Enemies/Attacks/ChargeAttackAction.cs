using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// 직선 돌진 공격.
    ///
    /// [PrepareWindup]
    ///   Windup 시작 시 wallDist를 미리 계산하고,
    ///   인디케이터 rectData.range를 실제 돌진 거리로 확정한다.
    ///   → 인디케이터가 실제 돌진 거리와 일치.
    ///
    /// [Execute]
    ///   PrepareWindup에서 확정된 _resolvedWallDist로 돌진.
    ///   ctx.LockedForward 방향 사용 (인디케이터와 동일).
    /// </summary>
    [Serializable]
    public class ChargeAttackAction : EnemyAttackAction
    {
        [Tooltip("돌진 이동 속도")]
        public float chargeSpeed            = 14f;
        [Tooltip("돌진 히트 판정 반경")]
        public float chargeHitRange         = 1.8f;
        [Tooltip("돌진 데미지 배율 (%). 100 = ×1.0, 200 = ×2.0. COMBAT.md §4 참조")]
        public int damagePercent = 200;
        [Tooltip("돌진 최대 이동 거리 (벽이 없을 경우 한계)")]
        public float chargeMaxDistance      = 20f;
        [Tooltip("그로기 지속 시간 (초)")]
        public float grogyDuration          = 2.5f;

        // PrepareWindup에서 계산된 실제 돌진 거리
        private float _resolvedWallDist;

        public override List<AttackIndicatorEntry> PrepareWindup(EnemyAttackContext ctx)
        {
            // 실제 벽까지 거리 계산
            var chargeDir = ctx.LockedForward;
            float wallDist = ctx.Stage != null
                ? ctx.Stage.RaycastToWall(ctx.SelfTransform.position, chargeDir, chargeMaxDistance)
                : chargeMaxDistance;
            _resolvedWallDist = Mathf.Max(0f, wallDist - 0.5f);

            // 인디케이터 range를 실제 돌진 거리로 확정한 복사본 반환
            var result = new List<AttackIndicatorEntry>();
            foreach (var entry in indicators)
            {
                var modified = entry;
                if (entry.shape == AttackIndicatorShape.Rectangle)
                {
                    modified.rectData = new RectIndicatorData
                    {
                        range = _resolvedWallDist,
                        width = entry.rectData.width
                    };
                }
                result.Add(modified);
            }
            return result;
        }

        public override IEnumerator Execute(EnemyAttackContext ctx, Action onComplete)
        {
            if (ctx.PlayerTransform == null) { onComplete?.Invoke(); yield break; }

            var   chargeDir = ctx.LockedForward;
            float wallDist  = _resolvedWallDist;

            // ── 직선 돌진 ────────────────────────────────────────
            float traveled  = 0f;
            bool  hitLanded = false;

            while (traveled < wallDist)
            {
                float step      = chargeSpeed * Time.deltaTime;
                float remaining = wallDist - traveled;
                step = Mathf.Min(step, remaining);

                ctx.SelfTransform.position =
                    (Vector2)ctx.SelfTransform.position + chargeDir * step;
                traveled += step;

                if (ctx.PlayerTransform != null &&
                    Vector2.Distance(ctx.SelfTransform.position, ctx.PlayerTransform.position)
                    <= chargeHitRange)
                {
                    hitLanded = true;
                    break;
                }
                yield return null;
            }

            // ── 히트 데미지 ──────────────────────────────────────
            if (hitLanded && ctx.PlayerTransform != null)
            {
                ctx.PlayerDamageable?.TakeDamage(
                    ctx.GetAttackPower() * (damagePercent / 100f),
                    chargeDir);
            }

            // ── 그로기 ───────────────────────────────────────────
            yield return new WaitForSeconds(grogyDuration);

            onComplete?.Invoke();
        }
    }
}
