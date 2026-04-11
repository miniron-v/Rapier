using Game.Data.MetaStats;
using Game.Data.RunStats;
using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 캐릭터 런타임 상태 데이터.
    /// MonoBehaviour 미사용. Presenter가 생성하고 소유한다.
    ///
    /// STATS.md §3 계산식:
    ///   최종 = (기본값 + MetaStat 깡합) × (1 + MetaStat % 합) × (1 + RunStat % 합) + RunStat 깡합
    /// MetaStat 은 <see cref="MetaStatContainer.ComputeHp"/> 등으로, RunStat 은 <see cref="RunStatContainer"/> 로 주입.
    /// </summary>
    public class CharacterModel
    {
        // ── 스탯 참조 ──────────────────────────────────────────────
        /// <summary>SO 원본 스탯. 런타임 불변.</summary>
        public CharacterStatData StatData { get; }

        // ── 주입된 컨테이너 ────────────────────────────────────────
        private readonly MetaStatContainer _meta;
        private          RunStatContainer  _runStat;

        // ── 최종 스탯 (MetaStat + RunStat 반영, [NonSerialized] 캐싱) ─
        /// <summary>MetaStat + RunStat 반영 최종 최대 HP.</summary>
        [System.NonSerialized] private float _finalMaxHp;
        /// <summary>MetaStat + RunStat 반영 최종 공격력.</summary>
        [System.NonSerialized] private float _finalAttackPower;
        /// <summary>MetaStat + RunStat 반영 최종 이동속도.</summary>
        [System.NonSerialized] private float _finalMoveSpeed;

        /// <summary>최종 최대 HP (MetaStat + RunStat 포함).</summary>
        public float MaxHp          => _finalMaxHp;
        /// <summary>최종 공격력 (MetaStat + RunStat 포함).</summary>
        public float AttackPower    => _finalAttackPower;
        /// <summary>최종 이동속도 (MetaStat + RunStat 포함).</summary>
        public float MoveSpeed      => _finalMoveSpeed;

        // ── 런타임 상태 ────────────────────────────────────────────
        public float CurrentHp          { get; private set; }
        public bool  IsAlive            => CurrentHp > 0f;
        public bool  IsInvincible       { get; private set; }
        public float ChargeRatio        { get; private set; } // 0~1
        public bool  IsJustDodgeReady   { get; private set; }
        public float DodgeCooldownRatio { get; private set; } // 0=쿨다운 중, 1=사용 가능

        // ── 이벤트 ────────────────────────────────────────────────
        public event System.Action<float> OnHpChanged;           // 현재 HP
        public event System.Action        OnDeath;
        public event System.Action<float> OnChargeChanged;        // 0~1
        public event System.Action<float> OnDodgeCooldownChanged; // 0~1

        /// <summary>
        /// MetaStat + RunStat 주입 생성자.
        /// 두 컨테이너를 모두 주입하면 STATS.md §3 계산식이 적용된 최종값으로 초기화된다.
        /// </summary>
        /// <param name="statData">SO 원본 스탯 (불변).</param>
        /// <param name="meta">장비/룬 MetaStat 컨테이너. null 이면 MetaStat 미반영.</param>
        /// <param name="runStat">인터미션 RunStat 컨테이너. null 이면 RunStat 미반영 (로비 등).</param>
        public CharacterModel(CharacterStatData statData, MetaStatContainer meta, RunStatContainer runStat)
        {
            StatData = statData;
            _meta    = meta;
            _runStat = runStat;

            // 초기 최종 스탯 계산 후 CurrentHp = _finalMaxHp (full HP 시작)
            ComputeFinalsOnly();
            CurrentHp          = _finalMaxHp;
            DodgeCooldownRatio = 1f; // 시작 시 즉시 사용 가능
        }

        // ── RunStat 지연 주입 ─────────────────────────────────────
        /// <summary>
        /// RunStatContainer 를 지연 주입한다 (Awake 순서 비결정성 우회용).
        /// <see cref="CharacterPresenterBase.Start"/> 에서 <see cref="Game.Core.Stage.StageManager"/>
        /// 조회가 확정된 뒤 호출. 주입 즉시 <see cref="RecomputeFinalStats"/> 로 스탯을 갱신한다.
        /// </summary>
        public void SetRunStat(RunStatContainer runStat)
        {
            _runStat = runStat;
            RecomputeFinalStats();
        }

        // ── RunStat 갱신 ──────────────────────────────────────────
        /// <summary>
        /// RunStat 픽 시 캐릭터 스탯 재계산. HP 증가분은 Heal, 감소분은 Clamp.
        /// <see cref="CharacterPresenterBase"/> 가 <see cref="RunStatContainer.OnStatChanged"/> 구독 후 호출.
        /// </summary>
        public void RecomputeFinalStats()
        {
            float oldMaxHp = _finalMaxHp;
            ComputeFinalsOnly();

            float delta = _finalMaxHp - oldMaxHp;
            if (delta > 0f)
            {
                // HP 증가 → 증가분만큼 Heal (사망 상태에서는 자동 부활 금지)
                if (IsAlive)
                {
                    CurrentHp = Mathf.Min(CurrentHp + delta, _finalMaxHp);
                    OnHpChanged?.Invoke(CurrentHp);
                }
            }
            else if (delta < 0f)
            {
                // HP 감소 → CurrentHp 를 새 MaxHp 로 Clamp
                float clamped = Mathf.Min(CurrentHp, _finalMaxHp);
                if (clamped != CurrentHp)
                {
                    CurrentHp = clamped;
                    OnHpChanged?.Invoke(CurrentHp);
                }
            }
            // delta == 0 이면 HP 이벤트 미발행
        }

        /// <summary>
        /// STATS.md §3 계산식으로 _finalMaxHp / _finalAttackPower / _finalMoveSpeed 를 갱신한다.
        /// HP 처리 없이 순수 계산만 수행. 생성자 및 RecomputeFinalStats 에서 호출.
        /// RunStat 깡합은 현재 프로토타입에서 0 이므로 생략 (STATS.md §3 참조).
        /// </summary>
        private void ComputeFinalsOnly()
        {
            // 계산식: metaFinal = (base + metaFlat) × (1 + metaPercent) [MetaStatContainer.ComputeXxx 구현]
            //         final = metaFinal × (1 + runPercent)
            float metaHp  = _meta?.ComputeHp(StatData.maxHp)       ?? StatData.maxHp;
            float metaAtk = _meta?.ComputeAtk(StatData.attackPower) ?? StatData.attackPower;
            float metaMs  = _meta?.ComputeMs(StatData.moveSpeed)    ?? StatData.moveSpeed;

            _finalMaxHp       = metaHp  * (1f + (_runStat?.HpPercent  ?? 0f));
            _finalAttackPower = metaAtk * (1f + (_runStat?.AtkPercent ?? 0f));
            _finalMoveSpeed   = metaMs  * (1f + (_runStat?.MsPercent  ?? 0f));
        }

        // ── HP ────────────────────────────────────────────────────
        public void TakeDamage(float amount)
        {
            if (!IsAlive || IsInvincible) return;
            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
            OnHpChanged?.Invoke(CurrentHp);
            if (!IsAlive) OnDeath?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            CurrentHp = Mathf.Min(_finalMaxHp, CurrentHp + amount);
            OnHpChanged?.Invoke(CurrentHp);
        }

        /// <summary>IsAlive 여부에 관계없이 HP를 직접 설정한다. 이어하기 부활 전용.</summary>
        public void Revive(float hp)
        {
            CurrentHp = Mathf.Clamp(hp, 0f, _finalMaxHp);
            OnHpChanged?.Invoke(CurrentHp);
        }

        // ── 무적 ──────────────────────────────────────────────────
        public void SetInvincible(bool value) => IsInvincible = value;

        // ── 차지 ──────────────────────────────────────────────────
        public void SetChargeRatio(float ratio)
        {
            ChargeRatio = Mathf.Clamp01(ratio);
            OnChargeChanged?.Invoke(ChargeRatio);
        }

        // ── 저스트 회피 슬로우 상태 ───────────────────────────────
        public void SetJustDodgeReady(bool value) => IsJustDodgeReady = value;

        // ── 회피 쿨다운 ───────────────────────────────────────────
        public void SetDodgeCooldownRatio(float ratio)
        {
            DodgeCooldownRatio = Mathf.Clamp01(ratio);
            OnDodgeCooldownChanged?.Invoke(DodgeCooldownRatio);
        }
    }
}
