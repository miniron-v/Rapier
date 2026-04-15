using UnityEngine;
using Game.Characters;

namespace Game.Characters.Ranger
{
    /// <summary>
    /// 레인저 캐릭터 전용 스탯 데이터.
    /// <see cref="CharacterStatData"/> 를 상속하여 투사체/지뢰/차지 조준 고유 필드를 추가한다.
    /// 경로: Assets/_Project/ScriptableObjects/Characters/
    /// SO 에셋 인스턴스는 Phase 26-D 에서 생성.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RangerStatData",
        menuName  = "Game/Data/Characters/Ranger Stat Data")]
    public class RangerStatData : CharacterStatData
    {
        // ── Tap 사격 ────────────────────────────────────────────────────────────
        [Header("Tap 사격 (원거리 투사체)")]
        [Tooltip("Tap 사격 데미지 배율 (%). 100 = ATK×1.0")]
        [Min(1)]
        [SerializeField] private int _tapDamagePercent = 100;

        [Tooltip("Tap 투사체 이동 속도 (unit/s)")]
        [Min(1f)]
        [SerializeField] private float _tapProjectileSpeed = 25f;

        [Tooltip("Tap 투사체 최대 사거리 (unit)")]
        [Min(1f)]
        [SerializeField] private float _tapProjectileRange = 8f;

        [Tooltip("Tap 투사체 히트박스 폭 (unit). 기준값, 에셋 맞춰 조정.")]
        [Min(0.1f)]
        [SerializeField] private float _tapProjectileWidth = 0.4f;

        // ── 저스트 회피 후 강화 화살 ─────────────────────────────────────────
        [Header("저스트 회피 강화 화살")]
        [Tooltip("저스트 회피 후 발사되는 강화 화살 데미지 배율 (%). 300 = ATK×3.0")]
        [Min(1)]
        [SerializeField] private int _justDodgeArrowDamagePercent = 300;

        [Tooltip("강화 화살 사거리 (unit)")]
        [Min(1f)]
        [SerializeField] private float _justDodgeArrowRange = 10f;

        [Tooltip("강화 화살 너비 = Tap 너비 × 배수")]
        [Min(1f)]
        [SerializeField] private float _justDodgeArrowWidthMult = 3f;

        // ── 차지 화살 ────────────────────────────────────────────────────────
        [Header("차지 화살 (바루스 Q 스타일)")]
        [Tooltip("차지 0 시점 데미지 배율 (%). 100 = ATK×1.0")]
        [Min(1)]
        [SerializeField] private int _chargeArrowMinDamagePercent = 100;

        [Tooltip("차지 Full 시점 데미지 배율 (%). 300 = ATK×3.0")]
        [Min(1)]
        [SerializeField] private int _chargeArrowMaxDamagePercent = 300;

        [Tooltip("차지 0 사거리 (unit)")]
        [Min(1f)]
        [SerializeField] private float _chargeArrowMinRange = 4f;

        [Tooltip("차지 Full 사거리 (unit)")]
        [Min(1f)]
        [SerializeField] private float _chargeArrowMaxRange = 14f;

        [Tooltip("차지 Full 너비 배수 (Tap 너비 기준). 차지 0 배수는 1.0f 고정")]
        [Min(1f)]
        [SerializeField] private float _chargeArrowMaxWidthMult = 3f;

        // ── 지뢰 ─────────────────────────────────────────────────────────────
        [Header("지뢰 (RangerMine)")]
        [Tooltip("회피 종료 시 지뢰 자동 설치 여부")]
        [SerializeField] private bool _minePlaceOnDodge = true;

        [Tooltip("지뢰 폭발 데미지 배율 (%). 80 = ATK×0.8")]
        [Min(1)]
        [SerializeField] private int _mineDamagePercent = 80;

        [Tooltip("지뢰 폭발 반경 (unit)")]
        [Min(0.1f)]
        [SerializeField] private float _mineExplosionRadius = 1.5f;

        [Tooltip("지뢰 수명 (초). 이후 자폭")]
        [Min(0.5f)]
        [SerializeField] private float _mineLifetime = 5f;

        [Tooltip("동시 존재 최대 지뢰 수. 초과 시 최고참 제거")]
        [Min(1)]
        [SerializeField] private int _maxActiveMines = 6;

        [Tooltip("지뢰 투척 속도 (unit/s). 높을수록 빠르게 날아감")]
        [Min(1f)]
        [SerializeField] private float _mineThrowSpeed = 12f;

        // ── 읽기 전용 프로퍼티 ───────────────────────────────────────────────

        /// <summary>Tap 사격 데미지 배율 (%)</summary>
        public int TapDamagePercent => _tapDamagePercent;

        /// <summary>Tap 투사체 속도 (unit/s)</summary>
        public float TapProjectileSpeed => _tapProjectileSpeed;

        /// <summary>Tap 투사체 사거리 (unit)</summary>
        public float TapProjectileRange => _tapProjectileRange;

        /// <summary>Tap 투사체 히트박스 폭 (unit)</summary>
        public float TapProjectileWidth => _tapProjectileWidth;

        /// <summary>저스트 회피 강화 화살 데미지 배율 (%)</summary>
        public int JustDodgeArrowDamagePercent => _justDodgeArrowDamagePercent;

        /// <summary>강화 화살 사거리 (unit)</summary>
        public float JustDodgeArrowRange => _justDodgeArrowRange;

        /// <summary>강화 화살 너비 배수</summary>
        public float JustDodgeArrowWidthMult => _justDodgeArrowWidthMult;

        /// <summary>차지 최소 데미지 배율 (%)</summary>
        public int ChargeArrowMinDamagePercent => _chargeArrowMinDamagePercent;

        /// <summary>차지 최대 데미지 배율 (%)</summary>
        public int ChargeArrowMaxDamagePercent => _chargeArrowMaxDamagePercent;

        /// <summary>차지 최소 사거리 (unit)</summary>
        public float ChargeArrowMinRange => _chargeArrowMinRange;

        /// <summary>차지 최대 사거리 (unit)</summary>
        public float ChargeArrowMaxRange => _chargeArrowMaxRange;

        /// <summary>차지 최대 너비 배수</summary>
        public float ChargeArrowMaxWidthMult => _chargeArrowMaxWidthMult;

        /// <summary>회피 시 지뢰 자동 설치 여부</summary>
        public bool MinePlaceOnDodge => _minePlaceOnDodge;

        /// <summary>지뢰 폭발 데미지 배율 (%)</summary>
        public int MineDamagePercent => _mineDamagePercent;

        /// <summary>지뢰 폭발 반경 (unit)</summary>
        public float MineExplosionRadius => _mineExplosionRadius;

        /// <summary>지뢰 수명 (초)</summary>
        public float MineLifetime => _mineLifetime;

        /// <summary>동시 최대 지뢰 수</summary>
        public int MaxActiveMines => _maxActiveMines;

        /// <summary>지뢰 투척 속도 (unit/s)</summary>
        public float MineThrowSpeed => _mineThrowSpeed;
    }
}
