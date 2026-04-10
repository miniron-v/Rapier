using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 버서커 보스.
    ///
    /// [1페이즈] Melee → Melee → Melee → 반복 (3연타 콤보)
    /// [2페이즈] Melee → Melee → Melee → Melee → Charge → 반복 (광폭화: HP 50% 이하)
    ///           BossPresenterBase 가 HP 50% 이하 감지 시 phase2Sequence 로 자동 교체.
    ///
    /// [광폭화 특성]
    ///   - Phase2 진입 시 이동속도·공격력 배율이 BossStatData 값으로 자동 상승.
    ///     (phase2SpeedMultiplier = 1.8, phase2AttackMultiplier = 1.4 권장)
    ///   - OnEnterPhase2() 에서 시각 효과 전환 (색상: BossStatData.phase2Color 로 자동 처리됨).
    ///
    /// [콤보 사이 회피 Window]
    ///   MeleeAttackAction 의 windupDuration 이 짧아야 압박감이 있음.
    ///   postAttackDelay 를 짧게 설정하여 콤보 연결감을 연출한다.
    ///   권장: windupDuration = 0.35초, postAttackDelay = 0.2초 (SO Inspector 설정).
    /// </summary>
    public class BerserkerBossPresenter : BossPresenterBase
    {
        protected override void OnEnterPhase2()
        {
            Debug.Log("[BerserkerBoss] 광폭화 — Phase2 진입! 이동속도·공격력 상승.");
            // 색상 전환은 BossPresenterBase.PhaseTransitionRoutine() 에서 자동 처리.
        }
    }
}
