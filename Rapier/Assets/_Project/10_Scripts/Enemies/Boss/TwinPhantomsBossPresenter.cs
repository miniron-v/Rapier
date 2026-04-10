using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 트윈 팬텀 보스 (1페이즈, 2체 동시).
    ///
    /// [구조]
    ///   - 두 개의 TwinPhantomsBossPresenter 인스턴스가 씬에 배치된다.
    ///   - 각 인스턴스는 독립적으로 Melee 시퀀스를 수행한다.
    ///   - Partner 필드로 서로를 참조한다.
    ///
    /// [1체 사망 시 생존자 강화]
    ///   한 쪽이 사망하면 partner.OnPartnerDeath() 를 호출한다.
    ///   생존자는 survivorSpeedMultiplier / survivorAttackMultiplier 로 스탯이 강화된다.
    ///
    /// [페이즈]
    ///   Twin Phantoms 는 BossPresenterBase 의 페이즈 전환(HP 50%)을 사용하지 않는다.
    ///   대신 partner 사망이 전환 트리거다.
    ///   TakeDamage override 에서 페이즈 전환을 방지하고 자체 로직을 사용한다.
    ///
    /// [클리어 조건]
    ///   두 체 모두 사망 시 클리어. BossRushManager 는 OnDeath 이벤트로 추적한다.
    ///
    /// [코루틴/구독 짝]
    ///   partner.OnDeath += HandlePartnerDeath  (Spawn에서 구독)
    ///   partner.OnDeath -= HandlePartnerDeath  (OnDestroy에서 해제)
    /// </summary>
    public class TwinPhantomsBossPresenter : BossPresenterBase
    {
        [Header("파트너 참조")]
        [Tooltip("씬에 배치된 다른 TwinPhantomsBossPresenter. 둘이 서로를 연결.")]
        public TwinPhantomsBossPresenter partner;

        [Header("생존자 강화 배율")]
        [Tooltip("파트너 사망 시 이동속도 배율")]
        [Min(1f)] public float survivorSpeedMultiplier  = 1.6f;
        [Tooltip("파트너 사망 시 공격력 배율")]
        [Min(1f)] public float survivorAttackMultiplier = 1.5f;

        // 생존자 강화 활성 여부
        [System.NonSerialized]
        private bool _isSurvivorEnhanced = false;

        // ── Spawn: 파트너 구독 ────────────────────────────────────
        public override void Spawn(EnemyStatData statData, Vector2 position)
        {
            _isSurvivorEnhanced = false;
            base.Spawn(statData, position);

            if (partner != null)
                partner.OnDeath += HandlePartnerDeath;
        }

        // ── OnDestroy: 구독 해제 ──────────────────────────────────
        private void OnDestroy()
        {
            if (partner != null)
                partner.OnDeath -= HandlePartnerDeath;
        }

        // ── BossPresenterBase HP 50% 페이즈 전환 비활성화 ─────────
        /// <summary>
        /// Twin Phantoms 는 HP 50% 페이즈 전환 대신 파트너 사망으로 강화한다.
        /// TakeDamage 를 override 하여 Phase 전환 로직이 실행되지 않도록 한다.
        /// BossPresenterBase.TakeDamage 의 페이즈 전환 검사를 건너뛰고,
        /// EnemyPresenterBase.TakeDamage 만 호출한다.
        /// </summary>
        public override void TakeDamage(float amount, Vector2 knockbackDir)
        {
            // BossPresenterBase 의 페이즈 전환 검사를 건너뛰고
            // EnemyPresenterBase.TakeDamage 만 호출한다.
            if (!IsAlive) return;
            _model.TakeDamage(amount);
            _view.PlayHit();
        }

        protected override void OnEnterPhase2() { /* 미사용 — 파트너 사망으로만 강화 */ }

        // ── 스탯 override: 생존자 강화 적용 ──────────────────────
        protected override float GetMoveSpeed()
        {
            float base_ = base.GetMoveSpeed();
            return _isSurvivorEnhanced ? base_ * survivorSpeedMultiplier : base_;
        }

        protected override float GetAttackPower()
        {
            float base_ = base.GetAttackPower();
            return _isSurvivorEnhanced ? base_ * survivorAttackMultiplier : base_;
        }

        // ── 파트너 사망 핸들러 ────────────────────────────────────
        private void HandlePartnerDeath()
        {
            if (!IsAlive || _isSurvivorEnhanced) return;

            _isSurvivorEnhanced = true;
            Debug.Log($"[TwinPhantoms:{name}] 파트너 사망 — 생존자 강화 발동! " +
                      $"Speed×{survivorSpeedMultiplier} ATK×{survivorAttackMultiplier}");

            // 색상 전환으로 강화 상태 시각화
            StartCoroutine(SurvivorEnhanceRoutine());
        }

        private System.Collections.IEnumerator SurvivorEnhanceRoutine()
        {
            if (_sr == null) yield break;

            var enhancedColor = new Color(0.8f, 0.2f, 1f); // 보라색
            float elapsed = 0f;
            Color start   = _sr.color;
            while (elapsed < 0.5f)
            {
                elapsed  += Time.deltaTime;
                _sr.color = Color.Lerp(start, enhancedColor, elapsed / 0.5f);
                yield return null;
            }
            _sr.color = enhancedColor;
            _view.ResetVisual(enhancedColor);
        }
    }
}
