using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 파이로맨서 보스.
    ///
    /// 페이즈 시퀀스/전환은 EnemyStatData.phases 에서 정의.
    /// Presenter는 보스 고유 연출 + GroundHazard 사망 정리만 담당한다.
    ///
    /// [특수 처리 — GroundHazard 사망 정리]
    ///   모든 페이즈의 시퀀스를 순회하여 GroundHazardAttackAction.ActiveHazard 를 즉시 제거.
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

        protected override void OnPhaseTransition(int phaseIndex)
        {
            Debug.Log($"[PyromancerBoss] Phase {phaseIndex + 1} 진입!");
        }

        // ── 장판 일괄 제거 ────────────────────────────────────────
        private void CleanupHazards()
        {
            if (_statData?.phases == null) return;
            foreach (var phase in _statData.phases)
                DestroyHazardsInSequence(phase.sequence);
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
