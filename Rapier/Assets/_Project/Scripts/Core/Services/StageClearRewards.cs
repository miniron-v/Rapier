namespace Game.Core.Services
{
    /// <summary>
    /// 스테이지 클리어 시 지급되는 Crystal 보상 테이블.
    /// BALANCE.md §7-2 기준.
    /// </summary>
    public static class StageClearRewards
    {
        /// <summary>
        /// stageIndex 에 해당하는 Crystal 보상량을 반환한다.
        /// </summary>
        public static int GetCrystal(int stageIndex)
        {
            if (stageIndex <= 8)  return 150;
            if (stageIndex <= 16) return 200;
            if (stageIndex <= 24) return 250;
            if (stageIndex <= 32) return 300;
            if (stageIndex <= 40) return 360;
            if (stageIndex <= 48) return 420;
            if (stageIndex <= 64) return 480;
            if (stageIndex <= 80) return 540;
            return 600;
        }
    }
}
