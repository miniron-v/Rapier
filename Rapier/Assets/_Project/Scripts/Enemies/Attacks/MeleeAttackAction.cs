using System;
using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    [Serializable]
    public class MeleeAttackAction : EnemyAttackAction
    {
        [Tooltip("인디케이터가 없을 때 사용하는 폴백 히트 판정 반경")]
        public float hitRange = 1.2f;
        [Tooltip("데미지 배율 (%). 100 = ×1.0, 200 = ×2.0. COMBAT.md §4 참조")]
        public int damagePercent = 100;

        public override IEnumerator Execute(EnemyAttackContext ctx, Action onComplete)
        {
            if (ctx.PlayerTransform != null)
            {
                Vector2 selfPos  = (Vector2)ctx.SelfTransform.position;
                Vector2 toPlayer = (Vector2)ctx.PlayerTransform.position - selfPos;
                float   dist     = toPlayer.magnitude;

                float baseAngleDeg = Mathf.Atan2(ctx.LockedForward.y, ctx.LockedForward.x) * Mathf.Rad2Deg;
                float angleDeg     = baseAngleDeg + (indicators != null && indicators.Count > 0 ? indicators[0].angleOffset : 0f);
                float rad          = angleDeg * Mathf.Deg2Rad;
                var   forward      = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float angleToPlayer = Vector2.Angle(forward, toPlayer);

                // 디버그: 실제 판정에 쓰이는 값 출력
                if (indicators != null && indicators.Count > 0)
                {
                    var entry = indicators[0];
                    Debug.Log($"[MeleeHit] dist={dist:F2} range={entry.sectorData.range:F2} | " +
                              $"angleToPlayer={angleToPlayer:F1} halfAngle={entry.sectorData.angle * 0.5f:F1} | " +
                              $"forward={forward} toPlayer={toPlayer.normalized} | " +
                              $"lockedForward={ctx.LockedForward}");
                }
                else
                {
                    Debug.Log($"[MeleeHit] fallback dist={dist:F2} hitRange={hitRange:F2}");
                }

                bool hit = CheckHit(ctx);
                Debug.Log($"[MeleeHit] hit={hit}");

                if (hit)
                {
                    ctx.PlayerDamageable?.TakeDamage(ctx.GetAttackPower() * (damagePercent / 100f), ctx.LockedForward);
                }
            }
            onComplete?.Invoke();
            yield break;
        }

        private bool CheckHit(EnemyAttackContext ctx)
        {
            Vector2 selfPos  = (Vector2)ctx.SelfTransform.position;
            Vector2 toPlayer = (Vector2)ctx.PlayerTransform.position - selfPos;

            if (indicators != null && indicators.Count > 0)
            {
                foreach (var entry in indicators)
                {
                    if (CheckIndicatorHit(entry, toPlayer, ctx.LockedForward))
                        return true;
                }
                return false;
            }
            return toPlayer.magnitude <= hitRange;
        }

        private bool CheckIndicatorHit(AttackIndicatorEntry entry, Vector2 toPlayer, Vector2 lockedForward)
        {
            float baseAngleDeg = Mathf.Atan2(lockedForward.y, lockedForward.x) * Mathf.Rad2Deg;
            float angleDeg     = baseAngleDeg + entry.angleOffset;
            float rad          = angleDeg * Mathf.Deg2Rad;
            var   forward      = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            switch (entry.shape)
            {
                case AttackIndicatorShape.Sector:
                {
                    float dist          = toPlayer.magnitude;
                    float angleToPlayer = Vector2.Angle(forward, toPlayer);
                    return dist <= entry.sectorData.range
                        && angleToPlayer <= entry.sectorData.angle * 0.5f;
                }
                case AttackIndicatorShape.Rectangle:
                {
                    var   right  = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
                    float localF = Vector2.Dot(toPlayer, forward);
                    float localR = Vector2.Dot(toPlayer, right);
                    return localF >= 0f
                        && localF <= entry.rectData.range
                        && Mathf.Abs(localR) <= entry.rectData.width * 0.5f;
                }
                default:
                    return toPlayer.magnitude <= hitRange;
            }
        }
    }
}
