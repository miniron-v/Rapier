namespace Game.Data.Missions
{
    /// <summary>미션 주기 구분.</summary>
    public enum MissionType
    {
        Daily,
        Weekly,
    }

    /// <summary>
    /// 미션 추적 이벤트 종류. PROGRESSION.md §6 기준.
    /// </summary>
    public enum MissionEvent
    {
        OnStageCleared,
        OnBossKilled,
        OnJustDodgeTriggered,
        OnChargeSkillUsed,
        OnDailyMissionCompleted,
        OnDailyAllCompleted,
        OnStageRecordUpdated,
    }
}
