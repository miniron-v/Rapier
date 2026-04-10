namespace Game.Data.RunStats
{
    /// <summary>
    /// MetaStat 주입 인터페이스 스텁.
    /// Phase 12-D에서는 구현체 없이 null 또는 기본값 처리.
    /// Phase 12-E에서 실제 MetaStatData와 연결한다.
    /// </summary>
    public interface IMetaStatProvider
    {
        /// <summary>MetaStat HP 깡합 (장비 등 영구 수치).</summary>
        float GetFlatHp();

        /// <summary>MetaStat HP % 합 (0.20 = +20%).</summary>
        float GetHpPercent();

        /// <summary>MetaStat ATK 깡합.</summary>
        float GetFlatAtk();

        /// <summary>MetaStat ATK % 합.</summary>
        float GetAtkPercent();
    }
}
