using System;
using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// AttackAction.Execute() 에 전달되는 런타임 컨텍스트.
    /// </summary>
    public class EnemyAttackContext
    {
        public Transform      SelfTransform;
        public Transform      PlayerTransform;
        public IDamageable    PlayerDamageable;
        public SpriteRenderer SpriteRenderer;
        public StageBuilder   Stage;
        public Func<float>    GetAttackPower;
        public Func<Vector2>  GetForward;

        /// <summary>Windup 시작 시 확정된 방향. ChargeAttackAction 등에서 사용.</summary>
        public Vector2 LockedForward;
    }
}
