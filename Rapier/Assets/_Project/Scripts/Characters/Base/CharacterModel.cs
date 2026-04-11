using Game.Data.MetaStats;
using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 캐릭터 런타임 상태 데이터.
    /// MonoBehaviour 미사용. Presenter가 생성하고 소유한다.
    ///
    /// Phase 13-B: <see cref="MetaStatContainer"/> 를 생성자에 주입하면
    /// MaxHp / MaxAtk / MaxMs 가 장비 스탯이 반영된 최종값으로 계산된다.
    /// SO 원본(StatData) 은 불변 유지.
    /// </summary>
    public class CharacterModel
    {
        // ── 스탯 참조 ──────────────────────────────────────────────
        /// <summary>SO 원본 스탯. 런타임 불변.</summary>
        public CharacterStatData StatData { get; }

        // ── 최종 스탯 (MetaStat 반영, [NonSerialized] 캐싱) ────────
        /// <summary>장비 MetaStat 반영 최종 최대 HP. MetaStat 없으면 StatData.maxHp 와 동일.</summary>
        [System.NonSerialized] private float _finalMaxHp;
        /// <summary>장비 MetaStat 반영 최종 공격력.</summary>
        [System.NonSerialized] private float _finalAttackPower;
        /// <summary>장비 MetaStat 반영 최종 이동속도.</summary>
        [System.NonSerialized] private float _finalMoveSpeed;

        /// <summary>최종 최대 HP (장비 스탯 포함).</summary>
        public float MaxHp          => _finalMaxHp;
        /// <summary>최종 공격력 (장비 스탯 포함).</summary>
        public float AttackPower    => _finalAttackPower;
        /// <summary>최종 이동속도 (장비 스탯 포함).</summary>
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
        /// 기본 생성자. MetaStat 없이 SO 원본값으로 초기화한다.
        /// </summary>
        public CharacterModel(CharacterStatData statData)
            : this(statData, null) { }

        /// <summary>
        /// MetaStat 주입 생성자. 장비 스탯이 반영된 최종값으로 초기화한다.
        /// </summary>
        /// <param name="statData">SO 원본 스탯 (불변).</param>
        /// <param name="meta">장비/룬 MetaStat 컨테이너. null 이면 원본값 그대로 사용.</param>
        public CharacterModel(CharacterStatData statData, MetaStatContainer meta)
        {
            StatData = statData;

            // 최종 스탯 계산: (base + metaFlat) × (1 + metaPercent)
            _finalMaxHp       = meta?.ComputeHp(statData.maxHp)       ?? statData.maxHp;
            _finalAttackPower = meta?.ComputeAtk(statData.attackPower) ?? statData.attackPower;
            _finalMoveSpeed   = meta?.ComputeMs(statData.moveSpeed)    ?? statData.moveSpeed;

            CurrentHp          = _finalMaxHp;
            DodgeCooldownRatio = 1f; // 시작 시 즉시 사용 가능
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
