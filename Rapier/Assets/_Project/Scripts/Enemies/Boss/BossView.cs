namespace Game.Enemies
{
    /// <summary>
    /// 보스 전용 뷰. EnemyView를 상속하되 PlayDeath()는 no-op.
    /// 보스 사망 시각은 BossDeathSequencer가 담당한다 (SlowMo 후 SR 페이드).
    /// </summary>
    public class BossView : EnemyView
    {
        public override void PlayDeath()
        {
            StopWindup();  // 인디케이터 즉시 제거
            // SR 페이드 및 GO 비활성화는 BossDeathSequencer가 담당
        }
    }
}
