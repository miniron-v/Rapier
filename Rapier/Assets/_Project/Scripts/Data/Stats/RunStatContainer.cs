using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.RunStats
{
    /// <summary>
    /// 런 중 누적되는 일회성 스탯 컨테이너.
    /// 메모리 only — 직렬화/저장 금지 (STATS.md §1, §4).
    /// 스테이지 클리어 또는 로비 복귀 시 Reset()으로 소멸.
    /// 감소율형(DodgeCDR / ChargeTimeReduction)은 소스별 독립 곱연산 (STATS.md §3-2).
    /// </summary>
    public class RunStatContainer
    {
        // ── 5종 RunStat % 가산형 누적 (STATS.md §2) ────────────────
        [NonSerialized] private float _hpPercent;
        [NonSerialized] private float _atkPercent;
        [NonSerialized] private float _msPercent;
        [NonSerialized] private float _critChancePercent;

        // ── 감소율형 — 소스별 독립 곱연산 (STATS.md §3-2) ───────────
        // 초기값 1f. Apply 시 *= (1 − value) or *= (1 + value), Reset 시 = 1f.
        [NonSerialized] private float _dodgeCdrMultiplier    = 1f;
        [NonSerialized] private float _chargeTimeMultiplier  = 1f;
        // InvincibilityBonus: 감소율형 — DodgeCDR/ChargeTimeReduction과 동일 패턴.
        // Apply 시 *= (1 - value), Reset 시 = 1f.
        [NonSerialized] private float _invincMultiplier      = 1f;

        // ── 읽기 전용 프로퍼티 (외부 노출) ──────────────────────────
        /// <summary>최대 HP에 곱할 RunStat % 합 (예: 0.25 = +25%).</summary>
        public float HpPercent                    => _hpPercent;
        /// <summary>공격력에 곱할 RunStat % 합.</summary>
        public float AtkPercent                   => _atkPercent;
        /// <summary>이동속도에 곱할 RunStat % 합.</summary>
        public float MsPercent                    => _msPercent;

        /// <summary>
        /// 회피 쿨다운 감소 누적 곱 multiplier.
        /// 기본값 1f. 0.8 이면 20% 감소. CharacterModel 에서 base × multiplier 로 적용.
        /// </summary>
        public float DodgeCdrMultiplier           => _dodgeCdrMultiplier;

        /// <summary>
        /// 차지 시간 감소 누적 곱 multiplier.
        /// 기본값 1f. 0.512 이면 20% 감소 3회 누적 상태. CharacterModel 에서 base × multiplier 로 적용.
        /// </summary>
        public float ChargeTimeMultiplier         => _chargeTimeMultiplier;

        /// <summary>
        /// InvincibilityBonus 누적 곱 multiplier. DodgeCDR/ChargeTimeReduction과 동일 감소율 패턴.
        /// 기본값 1f. 0.64 이면 20% 2회 적용 상태(0.8×0.8). CharacterModel 에서 base × multiplier 로 적용.
        /// STATS.md §3-2 소스별 독립 곱연산.
        /// </summary>
        public float InvincMultiplier             => _invincMultiplier;
        /// <summary>크리티컬 확률 합 (0~1 범위, 합산 후 클램프 권장).</summary>
        public float CritChancePercent            => _critChancePercent;

        /// <summary>스탯 누적 변경 시 발행. HUD 등이 구독.</summary>
        public event Action OnStatChanged;

        /// <summary>
        /// 선택된 RunStat 종류에 따라 해당 수치를 누적한다.
        /// 감소율형(DodgeCdrPercent / ChargeTimeReductionPercent)은 곱연산.
        /// </summary>
        public void Apply(RunStatType type, float value)
        {
            switch (type)
            {
                case RunStatType.HpPercent:                  _hpPercent          += value; break;
                case RunStatType.AtkPercent:                 _atkPercent         += value; break;
                case RunStatType.MsPercent:                  _msPercent          += value; break;
                case RunStatType.DodgeCdrPercent:            _dodgeCdrMultiplier   *= (1f - value); break;
                case RunStatType.ChargeTimeReductionPercent: _chargeTimeMultiplier *= (1f - value); break;
                // InvincibilityBonus: 감소율형 — DodgeCDR/ChargeTimeReduction과 동일 패턴 (STATS.md §3-2)
                case RunStatType.InvincibilityPercent:       _invincMultiplier     *= (1f - value); break;
                case RunStatType.CritChancePercent:          _critChancePercent  += value; break;
            }
            OnStatChanged?.Invoke();
            Debug.Log($"[RunStatContainer] {type} +{value:P0} 누적. 현재: {GetValue(type):F4}");
        }

        /// <summary>
        /// 스테이지 종료(클리어/로비 복귀) 시 모든 RunStat 초기화.
        /// </summary>
        public void Reset()
        {
            _hpPercent            = 0f;
            _atkPercent           = 0f;
            _msPercent            = 0f;
            _dodgeCdrMultiplier   = 1f;
            _chargeTimeMultiplier = 1f;
            _invincMultiplier     = 1f;
            _critChancePercent    = 0f;
            OnStatChanged?.Invoke();
            Debug.Log("[RunStatContainer] 모든 RunStat 초기화.");
        }

        /// <summary>
        /// 특정 종류의 현재 누적값을 반환한다.
        /// 감소율형은 multiplier 값을 그대로 반환한다 (1f = 감소 없음, 0.512 = 0.8^3).
        /// </summary>
        public float GetValue(RunStatType type)
        {
            return type switch
            {
                RunStatType.HpPercent                  => _hpPercent,
                RunStatType.AtkPercent                 => _atkPercent,
                RunStatType.MsPercent                  => _msPercent,
                RunStatType.DodgeCdrPercent            => _dodgeCdrMultiplier,   // multiplier 값
                RunStatType.ChargeTimeReductionPercent => _chargeTimeMultiplier, // multiplier 값
                RunStatType.InvincibilityPercent       => _invincMultiplier,     // multiplier 값
                RunStatType.CritChancePercent          => _critChancePercent,
                _                                       => 0f,
            };
        }

        /// <summary>
        /// 현재 누적 RunStat 전체를 한 줄 문자열로 반환한다. 디버그 전용.
        /// 감소율형은 multiplier 값을 출력 (1.0 = 감소 없음).
        /// </summary>
        public string GetSummaryLog()
        {
            return $"HP+{_hpPercent:P0} | ATK+{_atkPercent:P0} | MS+{_msPercent:P0} | " +
                   $"회피CDR multiplier={_dodgeCdrMultiplier:F4} | 차지시간 multiplier={_chargeTimeMultiplier:F4} | " +
                   $"무적 multiplier={_invincMultiplier:F4} | 크리+{_critChancePercent:P0}";
        }

        /// <summary>
        /// 현재 누적 RunStat 기준 최종 HP를 계산한다.
        /// DesignDoc §6-3: 최종 = (기본 + MetaStat 깡합) × (1 + MetaStat%) × (1 + RunStat%) + RunStat 깡합
        /// Phase 12-D에서는 MetaStat를 0으로 처리 — 12-E에서 IMetaStatProvider로 주입.
        /// </summary>
        public float CalculateFinalHp(float baseHp, IMetaStatProvider meta = null)
        {
            float metaFlat    = meta?.GetFlatHp()    ?? 0f;
            float metaPercent = meta?.GetHpPercent() ?? 0f;
            return (baseHp + metaFlat) * (1f + metaPercent) * (1f + _hpPercent);
        }

        /// <summary>
        /// 현재 누적 RunStat 기준 최종 ATK를 계산한다.
        /// </summary>
        public float CalculateFinalAtk(float baseAtk, IMetaStatProvider meta = null)
        {
            float metaFlat    = meta?.GetFlatAtk()    ?? 0f;
            float metaPercent = meta?.GetAtkPercent() ?? 0f;
            return (baseAtk + metaFlat) * (1f + metaPercent) * (1f + _atkPercent);
        }
    }

    // ── RunStat 종류 Enum (DesignDoc §8-2) ─────────────────────────
    /// <summary>인터미션 방에서 선택 가능한 RunStat 7종.</summary>
    public enum RunStatType
    {
        HpPercent,
        AtkPercent,
        MsPercent,
        DodgeCdrPercent,
        ChargeTimeReductionPercent,
        InvincibilityPercent,
        CritChancePercent,
    }
}
