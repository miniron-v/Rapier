using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 전용 스탯 데이터. EnemyStatData를 상속.
    /// 경로: Assets/_Project/30_ScriptableObjects/Enemies/Boss/
    /// </summary>
    [CreateAssetMenu(
        fileName = "BossStatData",
        menuName  = "Rapier/Enemies/BossStatData")]
    public class BossStatData : EnemyStatData
    {
        [Header("보스 외형")]
        public float bossScale   = 2.5f;
        public Color phase1Color = new Color(0.85f, 0.2f, 0.2f);
        public Color phase2Color = new Color(1f, 0.5f, 0f);

        [Header("2페이즈 스탯 (HP 50% 이하)")]
        [Min(1f)] public float phase2SpeedMultiplier   = 1.5f;
        [Min(1f)] public float phase2AttackMultiplier  = 1.3f;
        [Min(0f)] public float phaseTransitionDuration = 1.0f;

        [Header("2페이즈 공격 시퀀스")]
        [Tooltip("2페이즈 진입 시 교체될 공격 시퀀스.\n" +
                 "비워두면 1페이즈 attackSequence를 계속 사용.")]
        [SerializeReference]
        public List<EnemyAttackAction> phase2Sequence = new List<EnemyAttackAction>();
    }
}
