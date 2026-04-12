using System;
using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 광역(AoE) 즉시 히트 공격.
    /// Windup 후 hitRange 내 모든 플레이어에게 데미지.
    /// 현재는 단일 플레이어 대상이지만 다중 타겟 확장 가능.
    /// </summary>
    [Serializable]
    public class AoeAttackAction : EnemyAttackAction
    {
        [Tooltip("광역 히트 판정 반경 (월드 단위)")]
        public float hitRange = 3f;
        [Tooltip("데미지 배율 (%). 100 = ×1.0, 200 = ×2.0. COMBAT.md §4 참조")]
        public int damagePercent = 100;

        public override IEnumerator Execute(EnemyAttackContext ctx, Action onComplete)
        {
            if (ctx.PlayerTransform != null &&
                Vector2.Distance(ctx.SelfTransform.position, ctx.PlayerTransform.position) <= hitRange)
            {
                ctx.PlayerDamageable?.TakeDamage(
                    ctx.GetAttackPower() * (damagePercent / 100f),
                    ctx.GetForward());
            }
            onComplete?.Invoke();
            yield break;
        }
    }
}
