using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 캐릭터 런타임 상태 데이터.
    /// MonoBehaviour 미사용. Presenter가 생성하고 소유한다.
    /// </summary>
    public class CharacterModel
    {
        // ── 스탯 참조 ──────────────────────────────────────────────
        public CharacterStatData StatData { get; }

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

        public CharacterModel(CharacterStatData statData)
        {
            StatData          = statData;
            CurrentHp         = statData.maxHp;
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
            CurrentHp = Mathf.Min(StatData.maxHp, CurrentHp + amount);
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
