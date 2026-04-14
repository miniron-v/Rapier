using System;
using Game.Core.Utils;
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
        /// <summary>
        /// 회피 쿨다운 최종값 = StatData.dodgeCooldown × meta.DodgeCdrMultiplier × run.DodgeCdrMultiplier.
        /// STATS.md §3-2 소스별 독립 곱연산.
        /// </summary>
        [System.NonSerialized] private float _finalDodgeCooldown;
        /// <summary>
        /// 차지 시간 최종값 = StatData.chargeRequiredTime × meta.ChargeTimeMultiplier × run.ChargeTimeMultiplier.
        /// STATS.md §3-2 소스별 독립 곱연산.
        /// </summary>
        [System.NonSerialized] private float _finalChargeRequiredTime;
        /// <summary>스킬 데미지 배수 = 1 + meta.SkillDamagePercent (가산형 누적). RunStat 미포함.</summary>
        [System.NonSerialized] private float _skillDamageMultiplier;
        /// <summary>
        /// 회피 무적 시간 최종값 = StatData.dodgeInvincibleDuration × meta.InvincMultiplier × run.InvincMultiplier.
        /// STATS.md §3-2 소스별 독립 곱연산.
        /// </summary>
        [System.NonSerialized] private float _finalDodgeInvincibleDuration;

        /// <summary>최종 최대 HP (MetaStat + RunStat 포함).</summary>
        public float MaxHp                  => _finalMaxHp;
        /// <summary>최종 공격력 (MetaStat + RunStat 포함).</summary>
        public float AttackPower            => _finalAttackPower;
        /// <summary>최종 이동속도 (MetaStat + RunStat 포함).</summary>
        public float MoveSpeed              => _finalMoveSpeed;
        /// <summary>최종 회피 쿨다운 (감소율 적용 후). STATS.md §3-2.</summary>
        public float DodgeCooldown          => _finalDodgeCooldown;
        /// <summary>최종 차지 요구 시간 (감소율 적용 후). STATS.md §3-2.</summary>
        public float ChargeRequiredTime     => _finalChargeRequiredTime;
        /// <summary>스킬 데미지 배수 (1.0 = 보너스 없음). 차지 스킬 데미지 계산 시 곱한다.</summary>
        public float SkillDamageMultiplier  => _skillDamageMultiplier;
        /// <summary>
        /// 회피 무적 시간 최종값 (InvincibilityBonus 감소율 적용 후). STATS.md §3-2.
        /// InvincibilityBonus 20% 2회 = StatData.dodgeInvincibleDuration × 0.8 × 0.8 = ×0.64.
        /// CharacterPresenterBase 가 DodgeInvincibleRoutine 타이머에 사용한다.
        /// </summary>
        public float DodgeInvincibleDuration => _finalDodgeInvincibleDuration;

        // ── 런타임 상태 ────────────────────────────────────────────
        public float CurrentHp          { get; private set; }
        public bool  IsAlive            => CurrentHp > 0f;
        public bool  IsInvincible       { get; private set; }
        public float ChargeRatio        { get; private set; } // 0~1
        public bool  IsJustDodgeReady   { get; private set; }
        public float DodgeCooldownRatio { get; private set; } // 0=쿨다운 중, 1=사용 가능

        // ── 방향성 방어 (Warrior 전용) ─────────────────────────────
        [System.NonSerialized] private bool    _directionalGuardActive;
        [System.NonSerialized] private Vector2 _shieldNormal;
        [System.NonSerialized] private float   _shieldHalfAngleDeg;
        [System.NonSerialized] private Action  _onParryCallback;

        /// <summary>방향성 방어 활성 여부.</summary>
        public bool IsDirectionalGuardActive => _directionalGuardActive;

        // ── 데미지 배수 (Warrior 차지 중 감소에 사용) ──────────────
        [System.NonSerialized] private float _damageMultiplier = 1f;

        /// <summary>피격 데미지 배수. 1.0 = 변화 없음, 0.5 = 50% 감소.</summary>
        public float DamageMultiplier => _damageMultiplier;

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
        /// STATS.md §3 계산식으로 모든 final 필드를 갱신한다.
        /// HP 처리 없이 순수 계산만 수행. 생성자 및 RecomputeFinalStats 에서 호출.
        /// RunStat 깡합은 현재 프로토타입에서 0 이므로 생략 (STATS.md §3 참조).
        /// </summary>
        private void ComputeFinalsOnly()
        {
            // 가산형 §3-1
            // 계산식: metaFinal = (base + metaFlat) × (1 + metaPercent)
            //         final     = metaFinal × (1 + runPercent)
            float metaHp  = _meta?.ComputeHp(StatData.maxHp)       ?? StatData.maxHp;
            float metaAtk = _meta?.ComputeAtk(StatData.attackPower) ?? StatData.attackPower;
            float metaMs  = _meta?.ComputeMs(StatData.moveSpeed)    ?? StatData.moveSpeed;

            // HP/ATK는 정수 반올림 (사사오입) — 소수 데미지 방지
            _finalMaxHp       = MathUtils.RoundHalfUp(metaHp  * (1f + (_runStat?.HpPercent  ?? 0f)));
            _finalAttackPower = MathUtils.RoundHalfUp(metaAtk * (1f + (_runStat?.AtkPercent ?? 0f)));
            _finalMoveSpeed   = metaMs  * (1f + (_runStat?.MsPercent  ?? 0f));

            // 감소율형 §3-2 — base × Π_i(1 − metaP_i) × Π_j(1 − runP_j)
            // MetaStatContainer / RunStatContainer 가 각각 multiplier 를 누적 보관한다.
            _finalDodgeCooldown = StatData.dodgeCooldown
                * (_meta?.DodgeCdrMultiplier    ?? 1f)
                * (_runStat?.DodgeCdrMultiplier ?? 1f);

            _finalChargeRequiredTime = StatData.chargeRequiredTime
                * (_meta?.ChargeTimeMultiplier    ?? 1f)
                * (_runStat?.ChargeTimeMultiplier ?? 1f);

            // InvincibilityBonus §3-2 — 감소율형, DodgeCDR/ChargeTimeReduction과 동일 패턴.
            // base × Π_i(1 − metaP_i) × Π_j(1 − runP_j). 20% 2회 = 0.2s × 0.8 × 0.8 = 0.128s.
            // MetaStatContainer / RunStatContainer 가 각각 _invincMultiplier 를 누적 보관한다.
            _finalDodgeInvincibleDuration = StatData.dodgeInvincibleDuration
                * (_meta?.InvincMultiplier    ?? 1f)
                * (_runStat?.InvincMultiplier ?? 1f);

            // 스킬 데미지 배수 (가산형 누적, RunStat 미포함 — STATS.md §2 표). SkillDamagePercent 단위: 0~100
            _skillDamageMultiplier = 1f + (_meta?.SkillDamagePercent ?? 0f) / 100f;
        }

        // ── HP ────────────────────────────────────────────────────
        /// <summary>
        /// 데미지를 받는다.
        /// IsInvincible 또는 방향성 방어 판정을 통과하면 데미지가 차단된다.
        /// 방향성 방어 패링 성립 시 onParry 콜백 1회 호출 후 방어 해제.
        /// </summary>
        /// <param name="amount">원래 데미지 (DamageMultiplier 적용 전).</param>
        /// <param name="attackDir">공격이 날아오는 방향 (정규화 권장). 방향 무관이면 Vector2.zero.</param>
        public void TakeDamage(float amount, Vector2 attackDir)
        {
            if (!IsAlive) return;

            // 방향성 방어 판정 (IsInvincible 과 독립)
            if (_directionalGuardActive && attackDir != Vector2.zero)
            {
                // dot(-attackDir, shieldNormal) >= cos(halfAngle) 이면 방어
                float dot      = Vector2.Dot(-attackDir.normalized, _shieldNormal.normalized);
                float cosAngle = Mathf.Cos(_shieldHalfAngleDeg * Mathf.Deg2Rad);
                if (dot >= cosAngle)
                {
                    // 패링 성립: 방어 먼저 해제, 콜백 호출
                    var parry = _onParryCallback;
                    ClearDirectionalGuard();
                    parry?.Invoke();
                    return;
                }
            }

            if (IsInvincible) return;

            // DamageMultiplier 적용 후 사사오입
            float actual = MathUtils.RoundHalfUp(amount * _damageMultiplier);
            CurrentHp = Mathf.Max(0f, CurrentHp - actual);
            OnHpChanged?.Invoke(CurrentHp);
            if (!IsAlive) OnDeath?.Invoke();
        }

        /// <summary>
        /// 공격 방향 없는 데미지 (Vector2.zero 전달). 방향성 방어를 발동하지 않는다.
        /// 하위 호환성 유지용 — 기존 호출처 중 방향 정보가 없는 경우에 사용.
        /// </summary>
        public void TakeDamage(float amount) => TakeDamage(amount, Vector2.zero);

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

        // ── 방향성 방어 ───────────────────────────────────────────
        /// <summary>
        /// 방향성 방어를 활성화한다.
        /// TakeDamage 에서 dot(-attackDir, shieldNormal) &gt;= cos(halfAngleDeg) 면 데미지 무효 + onParry 1회 발동.
        /// </summary>
        /// <param name="shieldNormal">방패가 바라보는 방향 (정규화 권장).</param>
        /// <param name="halfAngleDeg">방어 범위 반각 (도). 60 = 전체 120° 방어.</param>
        /// <param name="onParry">패링 성립 시 1회 호출될 콜백.</param>
        public void SetDirectionalGuard(Vector2 shieldNormal, float halfAngleDeg, Action onParry)
        {
            _shieldNormal          = shieldNormal;
            _shieldHalfAngleDeg   = halfAngleDeg;
            _onParryCallback       = onParry;
            _directionalGuardActive = true;
        }

        /// <summary>방향성 방어를 해제한다.</summary>
        public void ClearDirectionalGuard()
        {
            _directionalGuardActive = false;
            _shieldNormal           = Vector2.zero;
            _shieldHalfAngleDeg    = 0f;
            _onParryCallback        = null;
        }

        // ── 데미지 배수 ───────────────────────────────────────────
        /// <summary>
        /// 피격 데미지 배수를 세팅한다. 1.0f = 기본, 0.5f = 50% 감소.
        /// Warrior 차지 중 호출한다.
        /// </summary>
        /// <param name="multiplier">배수 (0보다 커야 함).</param>
        public void SetDamageMultiplier(float multiplier)
        {
            _damageMultiplier = Mathf.Max(0f, multiplier);
        }

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
