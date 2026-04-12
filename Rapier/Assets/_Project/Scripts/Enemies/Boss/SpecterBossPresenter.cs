using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 스펙터 보스.
    /// 페이즈 시퀀스/전환은 EnemyStatData.phases 에서 정의.
    /// 순간이동 로직은 TeleportAttackAction 에 위임.
    /// Presenter는 보스 고유 연출만 담당한다.
    /// </summary>
    public class SpecterBossPresenter : BossPresenterBase
    {
        // 현재 추가 로직 없음.
        // 향후 스펙터 전용 연출이 필요하면 OnPhaseTransition(int) override로 추가.
    }
}
