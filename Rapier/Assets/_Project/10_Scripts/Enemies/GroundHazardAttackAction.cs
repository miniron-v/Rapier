using System;
using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 지면 장판 공격 (Pyromancer 전용).
    ///
    /// Windup 후 지정 위치(현재 플레이어 위치)에 데미지 존을 생성한다.
    /// 장판은 일정 시간 동안 유지되며, 틱마다 플레이어에게 데미지를 입힌다.
    /// 보스 본체 사망 시 장판도 즉시 제거된다 (GravekeeperBossPresenter 참조 구조 동일).
    ///
    /// [종료 경로]
    ///   1. duration 만료 → 자동 Destroy
    ///   2. 보스 사망 → PyromancerBossPresenter.OnDeath()에서 CleanupHazards() 호출
    /// </summary>
    [Serializable]
    public class GroundHazardAttackAction : EnemyAttackAction
    {
        [Tooltip("장판 지속 시간 (초)")]
        public float duration       = 4f;
        [Tooltip("틱 간격 (초)")]
        public float tickInterval   = 0.5f;
        [Tooltip("틱당 데미지 배율 (GetAttackPower 기준)")]
        public float tickDamage     = 0.3f;
        [Tooltip("장판 반경 (히트 판정)")]
        public float hazardRadius   = 1.5f;

        // 현재 활성 장판 참조 — PyromancerBossPresenter 에서 CleanupHazards() 시 사용
        [NonSerialized]
        public GameObject ActiveHazard;

        public override IEnumerator Execute(EnemyAttackContext ctx, Action onComplete)
        {
            if (ctx.PlayerTransform == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            // ── 장판 생성 ─────────────────────────────────────────
            var hazard   = new GameObject("GroundHazard_Pyro");
            ActiveHazard = hazard;

            var sr    = hazard.AddComponent<SpriteRenderer>();
            sr.color  = new Color(1f, 0.3f, 0f, 0.5f);
            hazard.transform.localScale = Vector3.one * hazardRadius * 2f;
            hazard.transform.position   = ctx.PlayerTransform.position;

            // ── 지속 데미지 루프 ──────────────────────────────────
            float elapsed  = 0f;
            float tickAcc  = 0f;
            float dmg      = ctx.GetAttackPower() * tickDamage;
            Vector2 hazardPos = hazard.transform.position;

            while (elapsed < duration && hazard != null)
            {
                elapsed  += Time.deltaTime;
                tickAcc  += Time.deltaTime;

                if (tickAcc >= tickInterval)
                {
                    tickAcc -= tickInterval;
                    if (ctx.PlayerTransform != null &&
                        Vector2.Distance(ctx.PlayerTransform.position, hazardPos) <= hazardRadius)
                    {
                        ctx.PlayerDamageable?.TakeDamage(dmg, Vector2.zero);
                    }
                }
                yield return null;
            }

            // ── 정리 ─────────────────────────────────────────────
            if (hazard != null)
                UnityEngine.Object.Destroy(hazard);
            ActiveHazard = null;

            onComplete?.Invoke();
        }
    }
}
