using System;
using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 공통 베이스. EnemyPresenterBase를 상속하고 페이즈 시스템을 추가한다.
    ///
    /// [페이즈 시스템]
    ///   Phase 1: attackSequence 사용
    ///   Phase 2: HP 50% 이하 진입 → phase2Sequence로 교체 (비어있으면 유지)
    ///   Phase 3: 자식이 SetPhase(Phase3)로 직접 전환 (예: Stormcaller HP 25%)
    ///
    /// [자식 override 포인트]
    ///   OnEnterPhase2() : 2페이즈 진입 시 추가 처리
    ///   SetPhase()      : 자식이 Phase3 등 추가 전환을 수행할 때 사용
    ///   GetMoveSpeed()  : 페이즈별 배율 자동 적용
    ///   GetAttackPower(): 페이즈별 배율 자동 적용
    /// </summary>
    public abstract class BossPresenterBase : EnemyPresenterBase
    {
        public enum BossPhase { Phase1, Phase2, Phase3 }
        public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;

        public event Action<BossPhase> OnPhaseChanged;

        protected BossStatData BossData => _statData as BossStatData;

        // ── Spawn override ────────────────────────────────────────
        public override void Spawn(EnemyStatData statData, Vector2 position)
        {
            base.Spawn(statData, position);

            CurrentPhase = BossPhase.Phase1;

            if (BossData != null)
            {
                transform.localScale = Vector3.one * BossData.bossScale;
                _view.ResetVisual(BossData.phase1Color);
                if (_sr != null) _sr.color = BossData.phase1Color;
            }
        }

        // ── TakeDamage override: 페이즈 전환 체크 ────────────────
        public override void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;
            base.TakeDamage(amount, knockbackDir);

            if (CurrentPhase == BossPhase.Phase1 && BossData != null)
            {
                float ratio = _model.CurrentHp / _model.StatData.maxHp;
                if (ratio <= 0.5f)
                    StartCoroutine(PhaseTransitionRoutine());
            }
        }

        // ── 페이즈 전환 ───────────────────────────────────────────
        private IEnumerator PhaseTransitionRoutine()
        {
            CurrentPhase = BossPhase.Phase2;

            float duration = BossData != null ? BossData.phaseTransitionDuration : 1f;

            if (BossData != null && _sr != null)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    _sr.color = Color.Lerp(Color.white, BossData.phase2Color, elapsed / duration);
                    yield return null;
                }
                _sr.color = BossData.phase2Color;
                _view.ResetVisual(BossData.phase2Color);
                if (_sr != null) _sr.color = BossData.phase2Color;
            }

            // phase2Sequence가 있으면 시퀀서 교체
            if (BossData != null &&
                BossData.phase2Sequence != null &&
                BossData.phase2Sequence.Count > 0)
            {
                SetSequence(BossData.phase2Sequence);
            }

            Debug.Log($"[{name}] ★ Phase 2 진입!");
            OnPhaseChanged?.Invoke(BossPhase.Phase2);
            OnEnterPhase2();
        }

        // ── 자식 override 포인트 ──────────────────────────────────
        protected virtual void OnEnterPhase2() { }

        /// <summary>
        /// 자식이 추가 페이즈 전환(Phase3 등)을 수행할 때 사용한다.
        /// CurrentPhase 갱신 + OnPhaseChanged 발행을 1곳에서 보장한다.
        /// </summary>
        protected void SetPhase(BossPhase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        // ── 스탯 배율 (페이즈별 자동 적용) ───────────────────────
        protected override float GetMoveSpeed()
        {
            float base_ = base.GetMoveSpeed();
            return (CurrentPhase >= BossPhase.Phase2 && BossData != null)
                ? base_ * BossData.phase2SpeedMultiplier
                : base_;
        }

        protected override float GetAttackPower()
        {
            float base_ = base.GetAttackPower();
            return (CurrentPhase >= BossPhase.Phase2 && BossData != null)
                ? base_ * BossData.phase2AttackMultiplier
                : base_;
        }
    }
}
