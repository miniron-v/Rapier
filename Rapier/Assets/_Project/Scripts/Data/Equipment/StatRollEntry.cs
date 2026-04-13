using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 등급별 롤 범위. min~max 사이에서 Random.Range 로 추첨된다.
    /// </summary>
    [Serializable]
    public struct StatRollRange
    {
        public float min;
        public float max;
    }

    /// <summary>
    /// 서브/메인 스탯 풀의 단일 엔트리. 등급별 범위 + 가중치를 보유한다.
    /// </summary>
    [Serializable]
    public class StatRollEntry
    {
        [Tooltip("능력치 종류")]
        public StatType statType;

        [Tooltip("true 면 percentValue 에 값을 넣는다. false 면 flatValue 에 넣는다.")]
        public bool usePercent = false;

        [Tooltip("풀 내 상대 가중치 (합산 후 정규화). 기본값 1.0.")]
        public float weight = 1f;

        [Header("등급별 범위")]
        public StatRollRange normal;
        public StatRollRange rare;
        public StatRollRange epic;
        public StatRollRange unique;

        /// <summary>
        /// 지정 등급의 [min, max] 범위에서 Random.Range 로 값을 뽑아 반환한다.
        /// </summary>
        public float Roll(EquipmentGrade grade)
        {
            StatRollRange range = grade switch
            {
                EquipmentGrade.Rare   => rare,
                EquipmentGrade.Epic   => epic,
                EquipmentGrade.Unique => unique,
                _                     => normal,
            };
            return UnityEngine.Random.Range(range.min, range.max);
        }

        /// <summary>
        /// 가중치 추첨. entries 에서 중복 타입 제외 후 weight 기반으로 선택한다.
        /// excludeTypes 에 포함된 StatType 은 무시한다.
        /// 모든 항목이 제외된 경우 null 반환.
        /// </summary>
        public static StatRollEntry PickWeighted(
            IReadOnlyList<StatRollEntry> entries,
            HashSet<StatType> excludeTypes)
        {
            if (entries == null || entries.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (excludeTypes != null && excludeTypes.Contains(e.statType)) continue;
                totalWeight += e.weight;
            }

            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accumulated = 0f;
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (excludeTypes != null && excludeTypes.Contains(e.statType)) continue;
                accumulated += e.weight;
                if (roll <= accumulated)
                    return e;
            }

            // 부동소수점 오차 방어: 마지막 유효 항목 반환
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e == null) continue;
                if (excludeTypes != null && excludeTypes.Contains(e.statType)) continue;
                return e;
            }
            return null;
        }
    }
}
