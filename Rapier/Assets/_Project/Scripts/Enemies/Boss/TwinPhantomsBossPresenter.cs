using System.Collections;
using System.Collections.Generic;
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
    ///   Twin Phantoms 는 HP 기반 페이즈 전환을 사용하지 않는다.
    ///   SO의 phases를 1개만 설정하여 자동 전환이 발생하지 않게 한다.
    ///   대신 partner 사망이 전환 트리거다.
    ///
    /// [클리어 조건]
    ///   두 체 모두 사망 시 클리어. BossRushManager 는 OnDeath 이벤트로 추적한다.
    ///
    /// [코루틴/구독 짝]
    ///   partner.OnDeath += HandlePartnerDeath  (Spawn에서 구독)
    ///   partner.OnDeath -= HandlePartnerDeath  (OnDestroy에서 해제)
    ///
    /// [다중 스폰 지원]
    ///   IMultiBossSibling 구현 — BossRushManager가 스폰 직후 SetSiblings()를 호출해
    ///   partner 참조를 런타임 주입한다. Inspector 수동 연결과 양립 가능(에디터 테스트용).
    /// </summary>
    public class TwinPhantomsBossPresenter : BossPresenterBase, IMultiBossSibling
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

        // ── IMultiBossSibling 구현: 런타임 partner 주입 ───────────
        /// <summary>
        /// BossRushManager가 스폰 직후 호출. 형제 리스트에서 자신을 제외한
        /// 첫 번째 TwinPhantomsBossPresenter를 partner로 설정한다.
        /// Inspector에서 이미 연결된 경우 런타임 주입이 덮어쓴다.
        /// </summary>
        public void SetSiblings(IReadOnlyList<BossPresenterBase> siblings)
        {
            // 기존 partner 구독이 있으면 먼저 해제 (중복 방지)
            if (partner != null)
                partner.OnDeath -= HandlePartnerDeath;

            partner = null;
            foreach (var s in siblings)
            {
                if (s != null && s != this && s is TwinPhantomsBossPresenter other)
                {
                    partner = other;
                    break;
                }
            }

            // 새 partner 구독
            if (partner != null)
                partner.OnDeath += HandlePartnerDeath;
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
