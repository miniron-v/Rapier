using Game.Characters;
using Game.Core;
using Game.Core.Utils;
using Game.Data.Equipment;
using Game.Data.MetaStats;

namespace Game.Data.Stats
{
    /// <summary>
    /// 캐릭터 현재 스탯의 정적 스냅샷 (POCO).
    /// 장비 장착 상태를 반영한 최종 수치를 필드로 보유하며, 런타임 SO 생성 없이 표시에만 사용한다.
    /// </summary>
    public sealed class CharacterStatSnapshot
    {
        // ── 스탯 필드 ────────────────────────────────────────────────

        /// <summary>최종 HP (RoundHalfUp 정수화)</summary>
        public int Hp;

        /// <summary>최종 공격력 (RoundHalfUp 정수화)</summary>
        public int Atk;

        /// <summary>최종 이동속도</summary>
        public float MoveSpeed;

        /// <summary>치명타 확률 (%)</summary>
        public float CritChancePercent;

        /// <summary>치명타 피해 (%)</summary>
        public float CritDamagePercent;

        /// <summary>스킬 피해 (%)</summary>
        public float SkillDamagePercent;

        /// <summary>회피 쿨타임 감소율 (%) = (1 − DodgeCdrMultiplier) × 100</summary>
        public float DodgeCdrReductionPercent;

        /// <summary>차지 시간 감소율 (%) = (1 − ChargeTimeMultiplier) × 100</summary>
        public float ChargeTimeReductionPercent;

        /// <summary>무적 시간 감소율 (%) = (1 − InvincMultiplier) × 100</summary>
        public float InvincReductionPercent;

        // ── 팩토리 ──────────────────────────────────────────────────

        /// <summary>
        /// characterId 의 장비 장착 상태를 반영해 스냅샷을 빌드한다.
        /// EquipmentManager 를 ServiceLocator 에서 TryGet 한다.
        /// </summary>
        /// <param name="characterId">캐릭터 식별자 ("Rapier" / "Assassin" 등)</param>
        /// <param name="baseData">캐릭터 기본 스탯 SO (null 이면 기본값 0 사용)</param>
        public static CharacterStatSnapshot Build(string characterId, CharacterStatData baseData)
        {
            var snap = new CharacterStatSnapshot();

            var em = ServiceLocator.TryGet<EquipmentManager>();
            MetaStatContainer container;
            if (em != null)
                container = new EquipmentMetaStatProvider(em).BuildContainer(characterId);
            else
                container = new MetaStatContainer();

            float baseHp  = baseData?.maxHp       ?? 0f;
            float baseAtk = baseData?.attackPower  ?? 0f;
            float baseMs  = baseData?.moveSpeed    ?? 0f;

            snap.Hp                      = (int)MathUtils.RoundHalfUp(container.ComputeHp(baseHp));
            snap.Atk                     = (int)MathUtils.RoundHalfUp(container.ComputeAtk(baseAtk));
            snap.MoveSpeed               = container.ComputeMs(baseMs);
            snap.CritChancePercent       = container.CritChancePercent;
            snap.CritDamagePercent       = container.CritDamagePercent;
            snap.SkillDamagePercent      = container.SkillDamagePercent;
            snap.DodgeCdrReductionPercent    = (1f - container.DodgeCdrMultiplier)   * 100f;
            snap.ChargeTimeReductionPercent  = (1f - container.ChargeTimeMultiplier) * 100f;
            snap.InvincReductionPercent      = (1f - container.InvincMultiplier)     * 100f;

            return snap;
        }
    }
}
