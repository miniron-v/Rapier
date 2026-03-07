using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 기본 스탯 데이터.
    /// 적 종류마다 다른 SO 에셋을 생성해 참조한다.
    /// 경로: Assets/_Project/30_ScriptableObjects/Enemies/
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnemyStatData",
        menuName  = "Rapier/Enemies/EnemyStatData")]
    public class EnemyStatData : ScriptableObject
    {
        [Header("기본 정보")]
        public string enemyName = "Enemy";
        [Tooltip("시각적으로 표시할 스프라이트. SO에서 할당.")]
        public UnityEngine.Sprite sprite;


        [Header("전투 스탯")]
        [Min(1)] public float maxHp       = 250f;
        [Min(0)] public float attackPower = 50f;
        [Min(0)] public float moveSpeed   = 2.5f;

        [Header("공격")]
        [Tooltip("공격 범위 반경 (월드 단위)")]
        [Min(0)] public float attackRange  = 1.2f;
        [Tooltip("공격 주기 (초)")]
        [Min(0)] public float attackCooldown = 1.5f;
        [Tooltip("공격 히트박스 활성 시간 (초) — 저스트 회피 윈도우")]
        [Min(0)] public float attackHitDuration = 0.3f;

        [Header("AI")]
        [Tooltip("플레이어 접근 시 랜덤 오프셋 각도 범위 (도)")]
        [Range(0f, 90f)] public float approachAngleVariance = 30f;
    }
}
