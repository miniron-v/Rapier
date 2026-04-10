using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.RunStats
{
    /// <summary>
    /// 런 중 누적되는 일회성 스탯 컨테이너.
    /// 메모리 only — 직렬화/저장 금지 (STATS.md §1, §4).
    /// 스테이지 클리어 또는 로비 복귀 시 Reset()으로 소멸.
    /// </summary>
    public class RunStatContainer
    {
        // ── 7종 RunStat % 누적 (STATS.md §2, DesignDoc §8-2) ──────
        [NonSerialized] private float _hpPercent;
        [NonSerialized] private float _atkPercent;
        [NonSerialized] private float _msPercent;
        [NonSerialized] private float _dodgeCdrPercent;
        [NonSerialized] private float _chargeTimeReductionPercent;
        [NonSerialized] private float _invincibilityPercent;
        [NonSerialized] private float _critChancePercent;

        // ── 읽기 전용 프로퍼티 (외부 노출) ──────────────────────────
        /// <summary>최대 HP에 곱할 RunStat % 합 (예: 0.25 = +25%).</summary>
        public float HpPercent                    => _hpPercent;
        /// <summary>공격력에 곱할 RunStat % 합.</summary>
        public float AtkPercent                   => _atkPercent;
        /// <summary>이동속도에 곱할 RunStat % 합.</summary>
        public float MsPercent                    => _msPercent;
        /// <summary>회피 쿨다운 감소율 합 (양수 = 쿨다운 단축).</summary>
        public float DodgeCdrPercent              => _dodgeCdrPercent;
        /// <summary>차지 시간 단축율 합.</summary>
        public float ChargeTimeReductionPercent   => _chargeTimeReductionPercent;
        /// <summary>무적 시간 증가율 합.</summary>
        public float InvincibilityPercent         => _invincibilityPercent;
        /// <summary>크리티컬 확률 합 (0~1 범위, 합산 후 클램프 권장).</summary>
        public float CritChancePercent            => _critChancePercent;

        /// <summary>스탯 누적 변경 시 발행. HUD 등이 구독.</summary>
        public event Action OnStatChanged;

        /// <summary>
        /// 선택된 RunStat 종류에 따라 해당 수치를 누적한다.
        /// </summary>
        public void Apply(RunStatType type, float value)
        {
            switch (type)
            {
                case RunStatType.HpPercent:                  _hpPercent                   += value; break;
                case RunStatType.AtkPercent:                 _atkPercent                  += value; break;
                case RunStatType.MsPercent:                  _msPercent                   += value; break;
                case RunStatType.DodgeCdrPercent:            _dodgeCdrPercent             += value; break;
                case RunStatType.ChargeTimeReductionPercent: _chargeTimeReductionPercent  += value; break;
                case RunStatType.InvincibilityPercent:       _invincibilityPercent        += value; break;
                case RunStatType.CritChancePercent:          _critChancePercent           += value; break;
            }
            OnStatChanged?.Invoke();
            Debug.Log($"[RunStatContainer] {type} +{value:P0} 누적. 현재: {GetValue(type):P0}");
        }

        /// <summary>
        /// 스테이지 종료(클리어/로비 복귀) 시 모든 RunStat 초기화.
        /// </summary>
        public void Reset()
        {
            _hpPercent                  = 0f;
            _atkPercent                 = 0f;
            _msPercent                  = 0f;
            _dodgeCdrPercent            = 0f;
            _chargeTimeReductionPercent = 0f;
            _invincibilityPercent       = 0f;
            _critChancePercent          = 0f;
            OnStatChanged?.Invoke();
            Debug.Log("[RunStatContainer] 모든 RunStat 초기화.");
        }

        /// <summary>
        /// 특정 종류의 현재 누적값을 반환한다.
        /// </summary>
        public float GetValue(RunStatType type)
        {
            return type switch
            {
                RunStatType.HpPercent                  => _hpPercent,
                RunStatType.AtkPercent                 => _atkPercent,
                RunStatType.MsPercent                  => _msPercent,
                RunStatType.DodgeCdrPercent            => _dodgeCdrPercent,
                RunStatType.ChargeTimeReductionPercent => _chargeTimeReductionPercent,
                RunStatType.InvincibilityPercent       => _invincibilityPercent,
                RunStatType.CritChancePercent          => _critChancePercent,
                _                                       => 0f,
            };
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
