using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 기본 스탯 데이터.
    /// 경로: Assets/_Project/30_ScriptableObjects/Enemies/
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnemyStatData",
        menuName  = "Rapier/Enemies/EnemyStatData")]
    public class EnemyStatData : ScriptableObject
    {
        [Header("기본 정보")]
        public string enemyName = "Enemy";
        public Sprite sprite;

        [Header("전투 스탯")]
        [Min(1)] public float maxHp       = 250f;
        [Min(0)] public float attackPower = 50f;
        [Min(0)] public float moveSpeed   = 2.5f;

        [Header("공격")]
        [Tooltip("Chase → Windup 진입 거리 (월드 단위)")]
        [Min(0)] public float attackRange = 1.2f;
        [Tooltip("공격 후 정지 딜레이 (초)")]
        [Min(0)] public float postAttackDelay = 0.3f;

        [Header("공격 시퀀스")]
        [Tooltip("공격 패턴 목록. 순서대로 루프 실행.\n" +
                 "우클릭 → Add → 원하는 AttackAction 파생 클래스 선택.")]
        [SerializeReference]
        public List<EnemyAttackAction> attackSequence = new List<EnemyAttackAction>();

        [Header("AI")]
        [Tooltip("플레이어 접근 시 랜덤 오프셋 각도 범위 (도)")]
        [Range(0f, 90f)] public float approachAngleVariance = 30f;
    }
}
