using System;
using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 공통 베이스. EnemyPresenterBase를 상속하고 2페이즈 시스템을 추가한다.
    ///
    /// [페이즈 시스템]
    ///   Phase 1: 일반 AI
    ///   Phase 2: HP 50% 이하 진입 → OnEnterPhase2() 호출 → 색상 변화 + 스탯 배율 적용
    ///
    /// [자식 override 포인트]
    ///   OnEnterPhase2() : 2페이즈 고유 행동 시작 (돌진, 텔레포트 등)
    ///   GetMoveSpeed()  : base 구현이 페이즈별 배율을 자동 적용
    ///   GetAttackPower(): base 구현이 페이즈별 배율을 자동 적용
    /// </summary>
    public abstract class BossPresenterBase : EnemyPresenterBase
    {
        // ── 페이즈 ────────────────────────────────────────────────
        public enum BossPhase { Phase1, Phase2 }
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

            // 색상 플래시 연출
            if (BossData != null && _sr != null)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    // 흰색 → phase2Color 로 전환
                    _sr.color = Color.Lerp(Color.white, BossData.phase2Color, t);
                    yield return null;
                }
                _sr.color = BossData.phase2Color;
                _view.ResetVisual(BossData.phase2Color);
                if (_sr != null) _sr.color = BossData.phase2Color;
            }

            Debug.Log($"[{name}] ★ Phase 2 진입!");
            OnPhaseChanged?.Invoke(BossPhase.Phase2);
            OnEnterPhase2();
        }

        // ── 자식 override 포인트 ──────────────────────────────────
        protected virtual void OnEnterPhase2() { }

        // ── 스탯 배율 (페이즈별 자동 적용) ───────────────────────
        protected override float GetMoveSpeed()
        {
            float base_ = base.GetMoveSpeed();
            if (CurrentPhase == BossPhase.Phase2 && BossData != null)
                return base_ * BossData.phase2SpeedMultiplier;
            return base_;
        }

        protected override float GetAttackPower()
        {
            float base_ = base.GetAttackPower();
            if (CurrentPhase == BossPhase.Phase2 && BossData != null)
                return base_ * BossData.phase2AttackMultiplier;
            return base_;
        }
    }
}
