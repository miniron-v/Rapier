using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 타이탄 보스.
    ///
    /// [1페이즈] attackSequence (Melee x3) 사용.
    /// [2페이즈] phase2Sequence (Melee x3 + ChargeAttackAction) 로 교체.
    ///           BossPresenterBase.PhaseTransitionRoutine() 이 자동으로 시퀀서를 교체한다.
    ///
    /// 공격 로직은 MeleeAttackAction / ChargeAttackAction 에 위임.
    /// 이 클래스는 보스 고유 설정(Inspector SerializeField)만 보유한다.
    /// </summary>
    public class TitanBossPresenter : BossPresenterBase
    {
        // 현재 추가 로직 없음.
        // 향후 타이탄 전용 연출(카메라 흔들림 등)이 필요하면 OnEnterPhase2() override로 추가.
    }
}
