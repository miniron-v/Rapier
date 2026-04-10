using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 파이로맨서 보스.
    ///
    /// [1페이즈] Projectile → GroundHazard → 반복
    /// [2페이즈] Projectile → GroundHazard → MultiDirectional → 반복
    ///           HP 50% 이하 진입 시 BossPresenterBase가 자동으로 phase2Sequence 로 교체.
    ///
    /// [특수 처리 — GroundHazard 사망 정리]
    ///   GroundHazardAttackAction.ActiveHazard 를 폴링하여 보스 사망 시 장판을 즉시 제거.
    ///
    /// [종료 경로 — GroundHazard]
    ///   1. duration 만료 → GroundHazardAttackAction.Execute 내부에서 자동 Destroy
    ///   2. 보스 사망 → HandleModelDeath() → CleanupHazards() → 활성 장판 Destroy
    /// </summary>
    public class PyromancerBossPresenter : BossPresenterBase
    {
        // ── 사망 override ────────────────────────────────────────
        protected override void HandleModelDeath()
        {
            CleanupHazards();
            base.HandleModelDeath();
        }

        // ── Phase2 진입 연출 ──────────────────────────────────────
        protected override void OnEnterPhase2()
        {
            Debug.Log("[PyromancerBoss] Phase2 진입 — 다방향 공격 시퀀스 활성화");
        }

        // ── 장판 일괄 제거 ────────────────────────────────────────
        private void CleanupHazards()
        {
            DestroyHazardsInSequence(_statData?.attackSequence);
            DestroyHazardsInSequence(BossData?.phase2Sequence);
        }

        private void DestroyHazardsInSequence(List<EnemyAttackAction> seq)
        {
            if (seq == null) return;
            foreach (var action in seq)
            {
                if (action is GroundHazardAttackAction hazard && hazard.ActiveHazard != null)
                {
                    Destroy(hazard.ActiveHazard);
                    hazard.ActiveHazard = null;
                }
            }
        }
    }
}
