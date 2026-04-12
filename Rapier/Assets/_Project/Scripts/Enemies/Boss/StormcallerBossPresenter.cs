using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 스톰콜러 보스 (3페이즈).
    /// 페이즈 시퀀스/전환은 EnemyStatData.phases 에서 정의.
    /// Presenter는 보스 고유 연출만 담당한다.
    /// </summary>
    public class StormcallerBossPresenter : BossPresenterBase
    {
        protected override void OnPhaseTransition(int phaseIndex)
        {
            Debug.Log($"[StormcallerBoss] Phase {phaseIndex + 1} 진입!");
        }
    }
}
