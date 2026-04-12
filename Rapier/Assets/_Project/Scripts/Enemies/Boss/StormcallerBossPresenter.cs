using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 스톰콜러 보스 (3페이즈).
    ///
    /// [1페이즈] MultiDirectional (4방향) → Projectile → 반복
    /// [2페이즈] MultiDirectional (4방향) → Projectile(유도) → MultiDirectional → 반복
    ///           HP 50% 이하 진입 시 BossPresenterBase 가 phase2Sequence 로 자동 교체.
    /// [3페이즈] 8방향 방사 MultiDirectional → Projectile(고속 유도) → 반복
    ///           HP 25% 이하 진입 시 이 클래스에서 phase3Sequence 로 직접 교체.
    ///
    /// [3페이즈 추가 데이터]
    ///   StormcallerBossStatData 를 사용하거나, BossStatData 를 직접 확장하는 대신
    ///   이 클래스가 [SerializeField] 로 phase3Sequence 를 보유한다 (OCP 준수).
    ///   BossPresenterBase 의 phase2Sequence 필드는 건드리지 않는다.
    ///
    /// [코루틴/구독 짝]
    ///   - TakeDamage 에서 Phase3 임계치 감지 → StartCoroutine(Phase3TransitionRoutine())
    ///   - Phase3TransitionRoutine 은 1회만 실행 (isPhase3Active 가드)
    /// </summary>
    public class StormcallerBossPresenter : BossPresenterBase
    {
        [Header("3페이즈 공격 시퀀스 (HP 25% 이하)")]
        [Tooltip("3페이즈 진입 시 교체될 공격 시퀀스.")]
        [SerializeReference]
        public List<EnemyAttackAction> phase3Sequence = new List<EnemyAttackAction>();

        [Header("3페이즈 색상")]
        public Color phase3Color = new Color(0.4f, 0.8f, 1f);

        // 3페이즈 전환 중복 방지
        [System.NonSerialized]
        private bool _isPhase3Active = false;

        // ── TakeDamage override: Phase3 임계 감지 추가 ────────────
        public override void TakeDamage(float amount, Vector2 knockbackDir)
        {
            base.TakeDamage(amount, knockbackDir);

            // Phase2 상태 + 생존 + HP 25% 이하가 되면 Phase3 전환
            if (!_isPhase3Active &&
                IsAlive &&
                CurrentPhase == BossPhase.Phase2 &&
                _model != null)
            {
                float ratio = _model.CurrentHp / _model.StatData.maxHp;
                if (ratio <= 0.25f)
                    StartCoroutine(Phase3TransitionRoutine());
            }
        }

        // ── Phase3 전환 코루틴 ────────────────────────────────────
        private IEnumerator Phase3TransitionRoutine()
        {
            _isPhase3Active = true;

            // 시각 전환 (0.5초)
            if (_sr != null)
            {
                float elapsed = 0f;
                float dur     = 0.5f;
                Color start   = _sr.color;
                while (elapsed < dur)
                {
                    elapsed  += Time.deltaTime;
                    _sr.color = Color.Lerp(start, phase3Color, elapsed / dur);
                    yield return null;
                }
                _sr.color = phase3Color;
                _view.ResetVisual(phase3Color);
            }

            // 시퀀스 교체
            if (phase3Sequence != null && phase3Sequence.Count > 0)
                SetSequence(phase3Sequence);

            SetPhase(BossPhase.Phase3);
            Debug.Log("[StormcallerBoss] ★★ Phase 3 진입! 8방향 방사 활성화.");
        }

        protected override void OnEnterPhase2()
        {
            Debug.Log("[StormcallerBoss] ★ Phase 2 진입 — 유도 투사체 활성화.");
        }

        // ── Spawn: 상태 초기화 ────────────────────────────────────
        public override void Spawn(EnemyStatData statData, Vector2 position)
        {
            _isPhase3Active = false;
            base.Spawn(statData, position);
        }
    }
}
