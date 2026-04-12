using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 타이탄 보스.
    /// 페이즈 시퀀스/전환은 EnemyStatData.phases 에서 정의.
    /// 공격 로직은 MeleeAttackAction / ChargeAttackAction 에 위임.
    /// Presenter는 보스 고유 연출만 담당한다.
    /// </summary>
    public class TitanBossPresenter : BossPresenterBase
    {
        // 현재 추가 로직 없음.
        // 향후 타이탄 전용 연출(카메라 흔들림 등)이 필요하면 OnPhaseTransition(int) override로 추가.
    }
}
