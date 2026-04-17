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
            if (stageIndex <= 8)  return 30;
            if (stageIndex <= 16) return 45;
            if (stageIndex <= 24) return 60;
            if (stageIndex <= 32) return 75;
            if (stageIndex <= 40) return 90;
            if (stageIndex <= 48) return 110;
            if (stageIndex <= 64) return 130;
            if (stageIndex <= 80) return 150;
            return 180;
        }
    }
}
