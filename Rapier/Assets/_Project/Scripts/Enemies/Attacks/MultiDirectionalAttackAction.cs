using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 다방향 동시 공격.
    ///
    /// indicators 리스트의 angleOffset 을 활용해 여러 방향의 인디케이터를 동시에 표시하고,
    /// Execute 시 모든 방향의 히트 판정을 순차로 검사한다.
    ///
    /// [Pyromancer] Phase2: 전방 + 좌우 45°, 총 3방향
    /// [Stormcaller] Phase1: 4방향(+, ×) / Phase2: 8방향(방사형)
    ///
    /// [설계 원칙]
    ///   - 인디케이터는 Sector 모양 사용 권장.
    ///   - angleOffset 은 플레이어 방향 기준 상대각이므로, Inspector에서 방향 배치만 설정하면 된다.
    ///   - 히트 판정은 MeleeAttackAction.CheckIndicatorHit 와 동일 로직.
    /// </summary>
    [Serializable]
    public class MultiDirectionalAttackAction : EnemyAttackAction
    {
        [Tooltip("인디케이터 없을 때 폴백 히트 반경")]
        public float hitRange = 1.5f;
        [Tooltip("데미지 배율 (%). 100 = ×1.0. COMBAT.md §4 참조")]
        public int damagePercent = 100;

        public override IEnumerator Execute(EnemyAttackContext ctx, Action onComplete)
        {
            if (ctx.PlayerTransform != null)
            {
                Vector2 selfPos  = (Vector2)ctx.SelfTransform.position;
                Vector2 toPlayer = (Vector2)ctx.PlayerTransform.position - selfPos;
                bool    hit      = false;

                if (indicators != null && indicators.Count > 0)
                {
                    foreach (var entry in indicators)
                    {
                        if (CheckIndicatorHit(entry, toPlayer, ctx.LockedForward))
                        {
                            hit = true;
                            break;
                        }
                    }
                }
                else
                {
                    hit = toPlayer.magnitude <= hitRange;
                }

                if (hit)
                    ctx.PlayerDamageable?.TakeDamage(
                        ctx.GetAttackPower() * (damagePercent / 100f),
                        ctx.LockedForward);
            }

            onComplete?.Invoke();
            yield break;
        }

        // ── 내부 히트 검사 (MeleeAttackAction 동일 로직) ──────────
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
