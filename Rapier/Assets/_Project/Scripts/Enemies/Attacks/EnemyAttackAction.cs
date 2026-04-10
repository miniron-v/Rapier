using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 공격 액션 베이스.
    ///
    /// [흐름]
    ///   1. EnterWindupPhase() → PrepareWindup(ctx) 호출
    ///      : 인디케이터 데이터 확정 (가변 범위 계산 등)
    ///      : 반환된 indicators 리스트로 AttackIndicator.Play() 호출
    ///   2. EnterHitPhase()   → Execute(ctx, onComplete) 호출
    ///      : 실제 공격 로직 수행
    ///      : 완료 시 onComplete() 반드시 호출
    ///
    /// [기본 동작]
    ///   PrepareWindup() 기본 구현은 indicators 그대로 반환.
    ///   가변 범위가 필요한 액션(ChargeAttackAction 등)만 override.
    /// </summary>
    [Serializable]
    public abstract class EnemyAttackAction
    {
        [Tooltip("Windup 예고 시간 (초)")]
        public float windupDuration = 0.5f;

        [Tooltip("Windup 중 인디케이터 방향 고정 여부")]
        public bool lockIndicatorDirection = false;

        [Tooltip("이 공격에서 표시할 인디케이터 목록")]
        public List<AttackIndicatorEntry> indicators = new List<AttackIndicatorEntry>();

        /// <summary>
        /// Windup 진입 시 호출. 인디케이터 표시에 사용할 최종 엔트리 목록을 반환한다.
        /// 기본 구현은 indicators를 그대로 반환.
        /// 가변 범위(돌진 벽 거리 등)가 필요한 액션은 override해서 수정된 목록을 반환한다.
        /// </summary>
        public virtual List<AttackIndicatorEntry> PrepareWindup(EnemyAttackContext ctx)
        {
            return indicators;
        }

        /// <summary>
        /// Hit 단계 진입 시 호출. 실제 공격 로직 수행.
        /// onComplete: 액션이 완전히 끝났을 때 반드시 호출해야 한다.
        /// </summary>
        public abstract IEnumerator Execute(EnemyAttackContext ctx, Action onComplete);
    }
}
