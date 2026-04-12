using UnityEngine;
using Game.Characters;

namespace Game.Data.Characters
{
    /// <summary>
    /// 어새신 캐릭터 전용 스탯 데이터.
    /// <see cref="CharacterStatData"/> 를 상속하여 잔상(Phantom) 및 차지 스킬 고유 필드를 추가한다.
    /// 경로: Assets/_Project/ScriptableObjects/Characters/
    /// </summary>
    [CreateAssetMenu(
        fileName = "AssassinStatData",
        menuName  = "Game/Data/Characters/AssassinStatData")]
    public class AssassinStatData : CharacterStatData
    {
        [Header("잔상 (Phantom)")]
        [Tooltip("잔상 지속 시간 (초). 이 시간 이후 페이드 아웃 소멸.")]
        [Min(0.5f)]
        [SerializeField] private float _phantomDuration = 5.0f;

        [Tooltip("잔상 공격 데미지 배율 (%). 50 = ATK×0.5. COMBAT.md §4 참조")]
        [Min(1)]
        [SerializeField] private int _phantomDamagePercent = 50;

        [Tooltip("동시 활성 잔상 최대 수. 초과 시 가장 오래된 잔상 제거.")]
        [Min(1)]
        [SerializeField] private int _maxPhantoms = 3;

        [Header("차지 스킬 — 360도 광역 베기")]
        [Tooltip("360도 광역 스킬 피해 배율 (%). 150 = ATK×1.5×SkillDmgMult")]
        [Min(1)]
        [SerializeField] private int _aoeSkillDamagePercent = 150;

        [Tooltip("360도 광역 스킬 범위 반지름 (월드 단위)")]
        [Min(0.5f)]
        [SerializeField] private float _aoeSkillRadius = 3.5f;

        // ── 읽기 전용 프로퍼티 ──────────────────────────────────────────────

        /// <summary>잔상 지속 시간 (초)</summary>
        public float PhantomDuration => _phantomDuration;

        /// <summary>잔상 공격 데미지 배율 (ATK 대비 %)</summary>
        public int PhantomDamagePercent => _phantomDamagePercent;

        /// <summary>동시 최대 잔상 수</summary>
        public int MaxPhantoms => _maxPhantoms;

        /// <summary>차지 스킬 데미지 배율 (%)</summary>
        public int AoeSkillDamagePercent => _aoeSkillDamagePercent;

        /// <summary>차지 스킬 범위 반지름 (월드 단위)</summary>
        public float AoeSkillRadius => _aoeSkillRadius;
    }
}
