using System;
using UnityEngine;

namespace Game.Data.MetaStats
{
    /// <summary>
    /// 캐릭터 ID별 영구 능력치 세트 (MetaStat).
    /// §6-1, §6-2 기준. SO 값은 런타임 불변. 가변값은 MetaStatContainer가 관리.
    /// 경로: Assets/_Project/ScriptableObjects/Stats/
    /// </summary>
    [CreateAssetMenu(
        fileName = "MetaStatData",
        menuName  = "Game/Data/Stats/MetaStatData")]
    public class MetaStatData : ScriptableObject
    {
        [Header("대상 캐릭터")]
        [Tooltip("캐릭터 식별 ID (예: Rapier). PascalCase 리터럴 정책 준수.")]
        [SerializeField] private string _characterId = "Rapier";

        [Header("깡(Flat) 보너스")]
        [SerializeField] private float _flatHp              = 0f;
        [SerializeField] private float _flatAtk             = 0f;
        [SerializeField] private float _flatMs              = 0f;

        [Header("% 보너스 (0.1 = 10%)")]
        [SerializeField] private float _percentHp              = 0f;
        [SerializeField] private float _percentAtk             = 0f;
        [SerializeField] private float _percentMs              = 0f;
        [SerializeField] private float _percentDodgeCdr        = 0f;
        [SerializeField] private float _percentChargeTimeRed   = 0f;
        [SerializeField] private float _percentInvincBonus     = 0f;
        [SerializeField] private float _percentCritChance      = 0f;
        [SerializeField] private float _percentCritDamage      = 0f;
        [SerializeField] private float _percentSkillDamage     = 0f;

        // ── 읽기 전용 프로퍼티 ─────────────────────────────────────
        public string CharacterId           => _characterId;
        public float  FlatHp                => _flatHp;
        public float  FlatAtk               => _flatAtk;
        public float  FlatMs                => _flatMs;
        public float  PercentHp             => _percentHp;
        public float  PercentAtk            => _percentAtk;
        public float  PercentMs             => _percentMs;
        public float  PercentDodgeCdr       => _percentDodgeCdr;
        public float  PercentChargeTimeRed  => _percentChargeTimeRed;
        public float  PercentInvincBonus    => _percentInvincBonus;
        public float  PercentCritChance     => _percentCritChance;
        public float  PercentCritDamage     => _percentCritDamage;
        public float  PercentSkillDamage    => _percentSkillDamage;
    }
}
