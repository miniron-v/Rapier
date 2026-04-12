using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적/보스 공통 페이즈 정의.
    /// <see cref="EnemyStatData.phases"/> 리스트의 각 항목.
    /// hpThreshold 내림차순(1.0 → 0.5 → 0.25 …)으로 정렬해 사용한다.
    /// </summary>
    [Serializable]
    public class PhaseEntry
    {
        [Tooltip("이 페이즈로 전환되는 HP 비율 임계치 (1.0=시작, 0.5=HP 50%, 0.25=HP 25%)")]
        public float hpThreshold = 1f;

        [Tooltip("페이즈 색상 (SpriteRenderer / View 에 적용)")]
        public Color color = Color.white;

        [Tooltip("이동속도 배율 (1.0=기본)")]
        [Min(1f)] public float speedMultiplier = 1f;

        [Tooltip("공격력 배율 (1.0=기본)")]
        [Min(1f)] public float attackMultiplier = 1f;

        [Tooltip("이 페이즈의 공격 시퀀스. 비어있으면 이전 페이즈 시퀀스를 유지한다.")]
        [SerializeReference]
        public List<EnemyAttackAction> sequence = new List<EnemyAttackAction>();
    }
}
