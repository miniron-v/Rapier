using UnityEngine;
using Game.Characters;

namespace Game.Data.Characters
{
    /// <summary>
    /// 워리어 캐릭터 전용 스탯 데이터.
    /// <see cref="CharacterStatData"/> 를 상속하여 방패 방어/패링 및 대지 분쇄 고유 필드를 추가한다.
    /// 경로: Assets/_Project/ScriptableObjects/Characters/
    /// SO 에셋 인스턴스는 Phase 26-D 에서 생성한다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WarriorStatData",
        menuName  = "Game/Data/Characters/Warrior Stat Data")]
    public class WarriorStatData : CharacterStatData
    {
        [Header("대지 분쇄 — 차지 Full 후 Release")]
        [Tooltip("대지 분쇄 데미지 배율 (%). 350 = ATK×3.5×SkillDmgMult")]
        [Min(1)]
        [SerializeField] private int _chargeDamagePercent = 350;

        [Header("방패 휘두르기 — 차지 Full 후 Swipe")]
        [Tooltip("방패 휘두르기 데미지 배율 (%). 150 = ATK×1.5")]
        [Min(1)]
        [SerializeField] private int _shieldSwingDamagePercent = 150;

        [Tooltip("방패 휘두르기 히트 시 넉백 거리 (월드 단위)")]
        [Min(0f)]
        [SerializeField] private float _shieldSwingKnockback = 2.0f;

        [Header("차지 중 피격 데미지 감소")]
        [Tooltip("Hold 차지 중 피격 데미지 감소율 (%). 50 = 피격 데미지 절반.")]
        [Range(0, 100)]
        [SerializeField] private int _damageReductionPercent = 50;

        [Header("방어 범위")]
        [Tooltip("방향성 방어 반각 (도). 60 = 전체 120° 방어.")]
        [Range(1f, 90f)]
        [SerializeField] private float _shieldGuardHalfAngle = 60f;

        // ── 읽기 전용 프로퍼티 ──────────────────────────────────────────────

        /// <summary>대지 분쇄 데미지 배율 (%)</summary>
        public int ChargeDamagePercent => _chargeDamagePercent;

        /// <summary>방패 휘두르기 데미지 배율 (%)</summary>
        public int ShieldSwingDamagePercent => _shieldSwingDamagePercent;

        /// <summary>방패 휘두르기 넉백 거리 (월드 단위)</summary>
        public float ShieldSwingKnockback => _shieldSwingKnockback;

        /// <summary>차지 중 피격 데미지 감소율 (%). 0~100.</summary>
        public int DamageReductionPercent => _damageReductionPercent;

        /// <summary>방향성 방어 반각 (도). 60 = 전체 120° 방어.</summary>
        public float ShieldGuardHalfAngle => _shieldGuardHalfAngle;
    }
}
