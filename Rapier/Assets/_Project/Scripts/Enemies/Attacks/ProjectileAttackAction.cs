using System;
using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 투사체 발사 공격.
    /// Windup 후 플레이어 방향(또는 고정 방향)으로 투사체를 발사한다.
    /// 투사체는 일정 속도로 직선 이동하며, 플레이어에 닿으면 데미지를 주고 소멸한다.
    /// GroundHazard와 달리 즉발성 히트 판정이 목적이다.
    ///
    /// [Pyromancer] Phase1: 단일 직선 발사 / Phase2: 고속 발사
    /// [Stormcaller] 유도 투사체(homingStrength > 0) 사용
    /// </summary>
    [Serializable]
    public class ProjectileAttackAction : EnemyAttackAction
    {
        [Tooltip("투사체 이동 속도")]
        public float projectileSpeed    = 8f;
        [Tooltip("투사체 최대 사거리")]
        public float maxRange           = 15f;
        [Tooltip("투사체 히트 반경")]
        public float hitRadius          = 0.4f;
        [Tooltip("데미지 배율 (%). 100 = ×1.0. COMBAT.md §4 참조")]
        public int damagePercent = 100;
        [Tooltip("유도 강도 (0 = 직선, 양수 = 플레이어 방향으로 회전 보정 deg/s)")]
        public float homingStrength     = 0f;

        public override IEnumerator Execute(EnemyAttackContext ctx, Action onComplete)
        {
            if (ctx.PlayerTransform == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            // ── 투사체 생성 (빈 GameObject 로 시뮬레이션) ──────────
            var proj    = new GameObject("Projectile_Pyro");
            var projSr  = proj.AddComponent<SpriteRenderer>();
            projSr.color = new Color(1f, 0.5f, 0.1f);

            // 크기: 시각 표현용 작은 원
            proj.transform.localScale = Vector3.one * 0.3f;
            proj.transform.position   = ctx.SelfTransform.position;

            // Windup 시 고정된 방향 사용
            Vector2 dir      = ctx.LockedForward.normalized;
            float   traveled = 0f;
            bool    hit      = false;

            while (traveled < maxRange)
            {
                if (proj == null) break;

                // 유도: 현재 방향을 플레이어 방향으로 회전 보정
                if (homingStrength > 0f && ctx.PlayerTransform != null)
                {
                    Vector2 toPlayer = ((Vector2)ctx.PlayerTransform.position
                                        - (Vector2)proj.transform.position).normalized;
                    float rot = homingStrength * Time.deltaTime;
                    dir       = Vector2.Lerp(dir, toPlayer, rot / 90f).normalized;
                }

                float step = projectileSpeed * Time.deltaTime;
                step = Mathf.Min(step, maxRange - traveled);
                proj.transform.position = (Vector2)proj.transform.position + dir * step;
                traveled += step;

                // 히트 판정
                if (ctx.PlayerTransform != null &&
                    Vector2.Distance(proj.transform.position, ctx.PlayerTransform.position) <= hitRadius)
                {
                    hit = true;
                    break;
                }
                yield return null;
            }

            if (hit && ctx.PlayerDamageable != null)
                ctx.PlayerDamageable.TakeDamage(ctx.GetAttackPower() * (damagePercent / 100f), dir);

            if (proj != null)
                UnityEngine.Object.Destroy(proj);

            onComplete?.Invoke();
        }
    }
}
