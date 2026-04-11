namespace Game.Data.Equipment
{
    /// <summary>
    /// 장비/룬에서 사용하는 능력치 종류. §6-1 기준.
    /// 값 8 은 영구 예약 (deprecated CDR). SkillDamage 는 9 로 명시 할당.
    /// </summary>
    public enum StatType
    {
        HP,
        ATK,
        MoveSpeed,
        DodgeCDR,
        ChargeTimeReduction,
        InvincibilityBonus,
        CritChance,
        CritDamage,
        // 8: reserved (deprecated CDR)
        SkillDamage = 9
    }
}
