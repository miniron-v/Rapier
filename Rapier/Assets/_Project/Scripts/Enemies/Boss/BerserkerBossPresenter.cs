using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 버서커 보스.
    ///
    /// 페이즈 시퀀스/전환은 EnemyStatData.phases 에서 정의.
    /// Presenter는 보스 고유 연출만 담당한다.
    ///
    /// [콤보 사이 회피 Window]
    ///   MeleeAttackAction 의 windupDuration 이 짧아야 압박감이 있음.
    ///   postAttackDelay 를 짧게 설정하여 콤보 연결감을 연출한다.
    ///   권장: windupDuration = 0.35초, postAttackDelay = 0.2초 (SO Inspector 설정).
    /// </summary>
    public class BerserkerBossPresenter : BossPresenterBase
    {
        protected override void OnPhaseTransition(int phaseIndex)
        {
            Debug.Log($"[BerserkerBoss] Phase {phaseIndex + 1} 진입! 이동속도·공격력 상승.");
            // 색상/배율 전환은 EnemyPresenterBase 의 PhaseTransitionRoutine 에서 자동 처리.
        }
    }
}
