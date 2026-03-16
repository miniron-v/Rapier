using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 공격 시퀀스 관리자.
    /// 순서대로 액션을 꺼내고, 끝에 도달하면 처음으로 루프한다.
    /// 페이즈 전환 시 SetSequence()로 새 시퀀스로 교체한다.
    /// </summary>
    public class EnemyAttackSequencer
    {
        private List<EnemyAttackAction> _sequence;
        private int                     _index;

        public bool HasSequence => _sequence != null && _sequence.Count > 0;

        /// <summary>시퀀스를 교체하고 인덱스를 0으로 리셋한다.</summary>
        public void SetSequence(List<EnemyAttackAction> sequence)
        {
            _sequence = sequence;
            _index    = 0;
        }

        /// <summary>다음 액션을 반환하고 인덱스를 진행한다.</summary>
        public EnemyAttackAction Next()
        {
            if (!HasSequence)
            {
                Debug.LogWarning("[EnemyAttackSequencer] 시퀀스가 비어있음.");
                return null;
            }
            var action = _sequence[_index];
            _index = (_index + 1) % _sequence.Count;
            return action;
        }
    }
}
