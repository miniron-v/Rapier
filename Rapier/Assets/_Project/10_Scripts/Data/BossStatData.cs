using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 전용 스탯 데이터. EnemyStatData를 상속하고 2페이즈 스탯 및 보스 전용 필드를 추가한다.
    /// 경로: Assets/_Project/30_ScriptableObjects/Enemies/Boss/
    /// </summary>
    [CreateAssetMenu(
        fileName = "BossStatData",
        menuName  = "Rapier/Enemies/BossStatData")]
    public class BossStatData : EnemyStatData
    {
        [Header("보스 외형")]
        [Tooltip("월드 스케일. 일반 적보다 크게 설정.")]
        public float bossScale = 2.5f;
        [Tooltip("1페이즈 색상")]
        public Color phase1Color = new Color(0.85f, 0.2f, 0.2f);
        [Tooltip("2페이즈 색상 (광폭화)")]
        public Color phase2Color = new Color(1f, 0.5f, 0f);

        [Header("2페이즈 스탯 (HP 50% 이하 진입)")]
        [Tooltip("2페이즈 이동속도 배율")]
        [Min(1f)] public float phase2SpeedMultiplier   = 1.5f;
        [Tooltip("2페이즈 공격력 배율")]
        [Min(1f)] public float phase2AttackMultiplier  = 1.3f;
        [Tooltip("2페이즈 진입 시 연출 시간 (초)")]
        [Min(0f)] public float phaseTransitionDuration = 1.0f;
    }
}
