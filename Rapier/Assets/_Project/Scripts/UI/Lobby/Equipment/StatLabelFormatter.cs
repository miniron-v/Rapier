using Game.Data.Equipment;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// StatType → 한글 레이블 변환 유틸리티.
    /// ItemDetailPopupView / EnhanceModalView / CharacterStatsModalView 에서 공통 사용한다.
    /// </summary>
    public static class StatLabelFormatter
    {
        /// <summary>StatType 을 한글 레이블로 변환한다. 매핑 없는 경우 enum 이름 반환.</summary>
        public static string GetLabel(StatType type)
        {
            return type switch
            {
                StatType.HP                  => "HP",
                StatType.ATK                 => "공격력",
                StatType.MoveSpeed           => "이동속도",
                StatType.DodgeCDR            => "회피 쿨타임",
                StatType.ChargeTimeReduction => "차지 시간",
                StatType.InvincibilityBonus  => "무적 시간",
                StatType.CritChance          => "치명타 확률",
                StatType.CritDamage          => "치명타 피해",
                StatType.SkillDamage         => "스킬 피해",
                _                            => type.ToString()
            };
        }
    }
}
