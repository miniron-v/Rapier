using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 레이피어 캐릭터 전용 스탯 데이터.
    /// CharacterStatData를 상속하여 레이피어 고유 필드를 추가한다.
    ///
    /// [스킬 공격 범위]
    ///   skillAttackWidth / Height / Offset 은 레이피어 전용 필드.
    ///   일반 공격(attackWidth/Height/Offset)과 완전히 독립되어,
    ///   어느 쪽을 수정해도 서로 영향을 주지 않는다.
    ///   현재 기본값은 일반 공격과 동일하게 설정되어 있으며 추후 자유롭게 조정 가능.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RapierStatData",
        menuName  = "Rapier/Characters/RapierStatData")]
    public class RapierStatData : CharacterStatData
    {
        [Header("표식 (Mark)")]
        [Tooltip("스킬 공격 데미지 배율 (%). 70 = ATK×0.7. COMBAT.md §4 참조")]
        public int   markDamagePercent = 70;
        [Tooltip("표식 최대 중첩 수")]
        public int   markMaxStack      = 5;

        [Header("고유 스킬 이동 속도")]
        [Tooltip("스킬 대시 속도 (적에게 접근)")]
        public float skillDashSpeed   = 20f;
        [Tooltip("스킬 복귀 속도 (DodgeDest로 귀환)")]
        public float skillReturnSpeed = 20f;

        [Header("차지 스킬")]
        [Tooltip("차지 스킬 데미지 배율 (%). 100 = ATK×1.0×stacks×SkillDmgMult. COMBAT.md §4 참조")]
        public int chargeSkillPercent = 100;

        [Header("스킬 공격 범위 — 레이피어 전용 (일반 공격과 독립)")]
        [Tooltip("스킬 공격 범위 가로 (월드 단위)")]
        [Min(0)] public float skillAttackWidth  = 2.0f;
        [Tooltip("스킬 공격 범위 세로 (월드 단위)")]
        [Min(0)] public float skillAttackHeight = 1.5f;
        [Tooltip("스킬 공격 범위 중심을 앞으로 밀 거리 (월드 단위)")]
        [Min(0)] public float skillAttackOffset = 1.0f;
    }
}
