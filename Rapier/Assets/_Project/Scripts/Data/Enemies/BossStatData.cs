using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 전용 스탯 데이터. EnemyStatData를 상속.
    /// 페이즈 시퀀스/색상/배율은 부모의 <see cref="EnemyStatData.phases"/> 로 통합.
    /// 이 클래스는 보스 고유 필드(스케일, 전환 연출, 다중 스폰)만 보유한다.
    /// 경로: Assets/_Project/ScriptableObjects/Enemies/Boss/
    /// </summary>
    [CreateAssetMenu(
        fileName = "BossStatData",
        menuName  = "Rapier/Enemies/BossStatData")]
    public class BossStatData : EnemyStatData
    {
        [Header("보스 외형")]
        public float bossScale = 2.5f;

        [Header("페이즈 전환 연출")]
        [Tooltip("페이즈 전환 색상 Lerp 시간 (초)")]
        [Min(0f)] public float phaseTransitionDuration = 1.0f;

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
