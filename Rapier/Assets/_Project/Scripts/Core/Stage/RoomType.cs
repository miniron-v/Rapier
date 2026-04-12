namespace Game.Core.Stage
{
    /// <summary>
    /// 스테이지 내 방의 종류.
    /// DesignDoc §2-1: [인터미션] → [보스1] → [인터미션] → [보스2] → ... → [보스4] → [클리어]
    /// </summary>
    public enum RoomType
    {
        /// <summary>보스 전투 방.</summary>
        BossRoom,

        /// <summary>HP 회복 + 스탯 선택 인터미션 방.</summary>
        IntermissionRoom,
    }
}
