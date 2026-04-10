using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 전용 스탯 데이터. EnemyStatData를 상속.
    /// 경로: Assets/_Project/ScriptableObjects/Enemies/Boss/
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

        [Header("다중 스폰")]
        [Tooltip("이 보스를 동시에 스폰할 개체 수. 기본 1.")]
        [SerializeField] private int _spawnCount = 1;

        [Tooltip("각 인스턴스의 스폰 오프셋 배열. spawnCount와 길이를 맞출 것.\n" +
                 "단일 스폰 보스는 (0,0) 1개면 충분.")]
        [SerializeField] private Vector2[] _spawnOffsets = new Vector2[] { Vector2.zero };

        /// <summary>스폰할 보스 개체 수. 최소 1 보장.</summary>
        public int SpawnCount => Mathf.Max(1, _spawnCount);

        /// <summary>각 인스턴스의 스폰 오프셋 목록 (읽기 전용).</summary>
        public IReadOnlyList<Vector2> SpawnOffsets => _spawnOffsets;
    }
}
