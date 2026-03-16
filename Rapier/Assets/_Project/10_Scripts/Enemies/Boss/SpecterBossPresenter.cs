using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 스펙터 보스.
    ///
    /// [1페이즈] attackSequence (Melee) 사용. 빠른 이동속도로 압박.
    /// [2페이즈] phase2Sequence (TeleportAttackAction + Melee) 로 교체.
    ///           BossPresenterBase.PhaseTransitionRoutine() 이 자동으로 시퀀서를 교체한다.
    ///
    /// 순간이동 로직은 TeleportAttackAction 에 위임.
    /// 이 클래스는 보스 고유 설정(Inspector SerializeField)만 보유한다.
    /// </summary>
    public class SpecterBossPresenter : BossPresenterBase
    {
        // 현재 추가 로직 없음.
        // 향후 스펙터 전용 연출이 필요하면 OnEnterPhase2() override로 추가.
    }
}
