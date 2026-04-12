using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 공통 베이스. EnemyPresenterBase를 상속하고 보스 고유 기능을 추가한다.
    ///
    /// [페이즈 시스템]
    ///   EnemyPresenterBase가 EnemyStatData.phases 기반으로 N페이즈 자동 전환을 처리한다.
    ///   이 클래스는 보스 전용 기능(스케일, 전환 연출 시간)만 담당한다.
    ///
    /// [자식 override 포인트]
    ///   OnPhaseTransition(int) : 페이즈 전환 시 추가 처리 (Base에서 상속)
    /// </summary>
    public abstract class BossPresenterBase : EnemyPresenterBase
    {
        protected BossStatData BossData => _statData as BossStatData;

        // ── Spawn override: 보스 스케일 적용 ──────────────────────
        public override void Spawn(EnemyStatData statData, Vector2 position)
        {
            base.Spawn(statData, position);

            if (BossData != null)
                transform.localScale = Vector3.one * BossData.bossScale;
        }

        // ── 페이즈 전환 연출 시간: BossStatData에서 읽기 ─────────
        protected override float GetPhaseTransitionDuration()
        {
            return BossData != null ? BossData.phaseTransitionDuration : 1f;
        }
    }
}
