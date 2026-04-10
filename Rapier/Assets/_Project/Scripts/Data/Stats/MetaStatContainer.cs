using System;

namespace Game.Data.MetaStats
{
    /// <summary>
    /// MetaStat 깡합 + % 합 관리 컨테이너.
    /// §6-3 계산식:
    ///   최종 = (기본값 + MetaStat 깡합) × (1 + MetaStat % 합)
    ///   (RunStat 적용은 12-D 영역)
    /// MonoBehaviour 미사용. Presenter가 생성 후 CharacterModel에 주입.
    /// </summary>
    public class MetaStatContainer
    {
        // ── 깡(Flat) 누적 ───────────────────────────────────────────
        private float _flatHp;
        private float _flatAtk;
        private float _flatMs;

        // ── %(Percent) 누적 ─────────────────────────────────────────
        private float _percentHp;
        private float _percentAtk;
        private float _percentMs;
        private float _percentDodgeCdr;
        private float _percentChargeTimeRed;
        private float _percentInvincBonus;
        private float _percentCritChance;
        private float _percentCritDamage;
        private float _percentCdr;
        private float _percentSkillDamage;

        /// <summary>MetaStat 변경 시 발행. 구독자(HUD 등)는 재계산 후 갱신.</summary>
        public event Action OnStatChanged;

        // ── 최종 능력치 계산 (§6-3) ────────────────────────────────

        /// <summary>최종 HP = (기본값 + MetaStat 깡합) × (1 + MetaStat % 합)</summary>
        public float ComputeHp(float baseHp)
            => (baseHp + _flatHp) * (1f + _percentHp);

        /// <summary>최종 ATK.</summary>
        public float ComputeAtk(float baseAtk)
            => (baseAtk + _flatAtk) * (1f + _percentAtk);

        /// <summary>최종 이동속도.</summary>
        public float ComputeMs(float baseMs)
            => (baseMs + _flatMs) * (1f + _percentMs);

        /// <summary>회피 쿨다운 감소율 합산 (%).</summary>
        public float DodgeCdrPercent        => _percentDodgeCdr;
        /// <summary>차지 시간 단축율 합산 (%).</summary>
        public float ChargeTimeRedPercent   => _percentChargeTimeRed;
        /// <summary>무적 시간 증가율 합산 (%).</summary>
        public float InvincBonusPercent     => _percentInvincBonus;
        /// <summary>크리티컬 확률 합산 (%).</summary>
        public float CritChancePercent      => _percentCritChance;
        /// <summary>크리티컬 데미지 합산 (%).</summary>
        public float CritDamagePercent      => _percentCritDamage;
        /// <summary>쿨타임 감소 합산 (%).</summary>
        public float CdrPercent             => _percentCdr;
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
            _percentDodgeCdr     += data.PercentDodgeCdr;
            _percentChargeTimeRed+= data.PercentChargeTimeRed;
            _percentInvincBonus  += data.PercentInvincBonus;
            _percentCritChance   += data.PercentCritChance;
            _percentCritDamage   += data.PercentCritDamage;
            _percentCdr          += data.PercentCdr;
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
            _percentDodgeCdr     -= data.PercentDodgeCdr;
            _percentChargeTimeRed-= data.PercentChargeTimeRed;
            _percentInvincBonus  -= data.PercentInvincBonus;
            _percentCritChance   -= data.PercentCritChance;
            _percentCritDamage   -= data.PercentCritDamage;
            _percentCdr          -= data.PercentCdr;
            _percentSkillDamage  -= data.PercentSkillDamage;

            OnStatChanged?.Invoke();
        }

        /// <summary>전체 초기화.</summary>
        public void Clear()
        {
            _flatHp = _flatAtk = _flatMs = 0f;
            _percentHp = _percentAtk = _percentMs = 0f;
            _percentDodgeCdr = _percentChargeTimeRed = 0f;
            _percentInvincBonus = _percentCritChance = 0f;
            _percentCritDamage = _percentCdr = _percentSkillDamage = 0f;
            OnStatChanged?.Invoke();
        }
    }
}
