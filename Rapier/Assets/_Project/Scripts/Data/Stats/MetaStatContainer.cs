using System;
using Game.Data.Equipment;

namespace Game.Data.MetaStats
{
    /// <summary>
    /// MetaStat 깡합 + % 합 + 감소율 곱셈 누적 컨테이너.
    /// STATS.md §3 계산식:
    ///   가산형 최종 = (기본값 + MetaStat 깡합) × (1 + MetaStat % 합)
    ///   감소율형 최종 = 기본값 × Π_i(1 − metaP_i)  (소스별 독립 곱연산)
    /// MonoBehaviour 미사용. Presenter가 생성 후 CharacterModel에 주입.
    /// </summary>
    public class MetaStatContainer
    {
        // ── 깡(Flat) 누적 ───────────────────────────────────────────
        private float _flatHp;
        private float _flatAtk;
        private float _flatMs;

        // ── %(Percent) 가산형 누적 ──────────────────────────────────
        private float _percentHp;
        private float _percentAtk;
        private float _percentMs;
        private float _percentInvincBonus;
        private float _percentCritChance;
        private float _percentCritDamage;
        private float _percentSkillDamage;

        // ── 감소율형 — 소스별 독립 곱연산 (STATS.md §3-2) ───────────
        // 초기값 1f. Apply 시 *= (1 − p), Remove 시 /= (1 − p).
        private float _dodgeCdrMultiplier     = 1f;
        private float _chargeTimeMultiplier   = 1f;

        /// <summary>MetaStat 변경 시 발행. 구독자(HUD 등)는 재계산 후 갱신.</summary>
        public event Action OnStatChanged;

        // ── 최종 능력치 계산 (§3-1) ────────────────────────────────

        /// <summary>최종 HP = (기본값 + MetaStat 깡합) × (1 + MetaStat % 합)</summary>
        public float ComputeHp(float baseHp)
            => (baseHp + _flatHp) * (1f + _percentHp);

        /// <summary>최종 ATK.</summary>
        public float ComputeAtk(float baseAtk)
            => (baseAtk + _flatAtk) * (1f + _percentAtk);

        /// <summary>최종 이동속도.</summary>
        public float ComputeMs(float baseMs)
            => (baseMs + _flatMs) * (1f + _percentMs);

        /// <summary>
        /// 회피 쿨다운 감소 누적 곱 multiplier.
        /// 기본값 1f. 0.8 이면 20% 감소. CharacterModel 에서 base × multiplier 로 적용.
        /// </summary>
        public float DodgeCdrMultiplier     => _dodgeCdrMultiplier;

        /// <summary>
        /// 차지 시간 감소 누적 곱 multiplier.
        /// 기본값 1f. 0.95 이면 5% 감소. CharacterModel 에서 base × multiplier 로 적용.
        /// </summary>
        public float ChargeTimeMultiplier   => _chargeTimeMultiplier;

        /// <summary>무적 시간 증가율 합산 (%).</summary>
        public float InvincBonusPercent     => _percentInvincBonus;
        /// <summary>크리티컬 확률 합산 (%).</summary>
        public float CritChancePercent      => _percentCritChance;
        /// <summary>크리티컬 데미지 합산 (%).</summary>
        public float CritDamagePercent      => _percentCritDamage;
        /// <summary>스킬 데미지 증가 합산 (%).</summary>
        public float SkillDamagePercent     => _percentSkillDamage;

        // ── 증분 적용 ──────────────────────────────────────────────

        /// <summary>
        /// SO 한 장의 MetaStatData를 컨테이너에 누산한다.
        /// 캐릭터 레벨업, 장비 장착 시 호출.
        /// </summary>
        public void Apply(MetaStatData data)
        {
            _flatHp              += data.FlatHp;
            _flatAtk             += data.FlatAtk;
            _flatMs              += data.FlatMs;
            _percentHp           += data.PercentHp;
            _percentAtk          += data.PercentAtk;
            _percentMs           += data.PercentMs;
            // 감소율형 — 소스별 독립 곱연산
            if (data.PercentDodgeCdr < 1f)
                _dodgeCdrMultiplier   *= (1f - data.PercentDodgeCdr);
            if (data.PercentChargeTimeRed < 1f)
                _chargeTimeMultiplier *= (1f - data.PercentChargeTimeRed);
            _percentInvincBonus  += data.PercentInvincBonus;
            _percentCritChance   += data.PercentCritChance;
            _percentCritDamage   += data.PercentCritDamage;
            _percentSkillDamage  += data.PercentSkillDamage;

            OnStatChanged?.Invoke();
        }

        /// <summary>
        /// SO 한 장의 MetaStatData를 컨테이너에서 제거한다.
        /// 장비 해제 시 호출.
        /// </summary>
        public void Remove(MetaStatData data)
        {
            _flatHp              -= data.FlatHp;
            _flatAtk             -= data.FlatAtk;
            _flatMs              -= data.FlatMs;
            _percentHp           -= data.PercentHp;
            _percentAtk          -= data.PercentAtk;
            _percentMs           -= data.PercentMs;
            // 감소율형 — div-by-zero 방어: p >= 1이면 나눗셈 스킵
            if (data.PercentDodgeCdr < 1f)
                _dodgeCdrMultiplier   /= (1f - data.PercentDodgeCdr);
            if (data.PercentChargeTimeRed < 1f)
                _chargeTimeMultiplier /= (1f - data.PercentChargeTimeRed);
            _percentInvincBonus  -= data.PercentInvincBonus;
            _percentCritChance   -= data.PercentCritChance;
            _percentCritDamage   -= data.PercentCritDamage;
            _percentSkillDamage  -= data.PercentSkillDamage;

            OnStatChanged?.Invoke();
        }

        /// <summary>전체 초기화.</summary>
        public void Clear()
        {
            _flatHp = _flatAtk = _flatMs = 0f;
            _percentHp = _percentAtk = _percentMs = 0f;
            _dodgeCdrMultiplier   = 1f;
            _chargeTimeMultiplier = 1f;
            _percentInvincBonus = _percentCritChance = 0f;
            _percentCritDamage = _percentSkillDamage = 0f;
            OnStatChanged?.Invoke();
        }

        // ── StatEntry 직접 입력 경로 (Phase 13-B 장비 파이프라인) ──

        /// <summary>
        /// <see cref="StatEntry"/> (장비/룬 스탯) 를 컨테이너에 누산한다.
        /// <see cref="Apply(MetaStatData)"/> 와 별도 경로로, 기존 SO 경로를 건드리지 않는다.
        /// </summary>
        public void Apply(StatEntry entry)
        {
            ApplyStat(entry.statType, entry.flatValue, entry.percentValue);
            OnStatChanged?.Invoke();
        }

        /// <summary>
        /// 스탯 종류 + 깡값 + % 값으로 직접 누산한다.
        /// </summary>
        public void Apply(StatType statType, float flat, float percent)
        {
            ApplyStat(statType, flat, percent);
            OnStatChanged?.Invoke();
        }

        private void ApplyStat(StatType statType, float flat, float percent)
        {
            switch (statType)
            {
                case StatType.HP:
                    _flatHp      += flat;
                    _percentHp   += percent;
                    break;
                case StatType.ATK:
                    _flatAtk     += flat;
                    _percentAtk  += percent;
                    break;
                case StatType.MoveSpeed:
                    _flatMs      += flat;
                    _percentMs   += percent;
                    break;
                case StatType.DodgeCDR:
                    // 감소율형 — 소스별 독립 곱연산
                    if (percent < 1f) _dodgeCdrMultiplier   *= (1f - percent);
                    break;
                case StatType.ChargeTimeReduction:
                    // 감소율형 — 소스별 독립 곱연산
                    if (percent < 1f) _chargeTimeMultiplier *= (1f - percent);
                    break;
                case StatType.InvincibilityBonus:
                    _percentInvincBonus  += percent;
                    break;
                case StatType.CritChance:
                    _percentCritChance   += percent;
                    break;
                case StatType.CritDamage:
                    _percentCritDamage   += percent;
                    break;
                case StatType.SkillDamage:
                    _percentSkillDamage  += percent;
                    break;
            }
        }
    }
}
