using System;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 드롭 테이블의 개별 항목. 등급 + 드롭 확률 + 아이템 풀을 보유한다.
    /// </summary>
    [Serializable]
    public class DropEntry
    {
        [Tooltip("드롭 등급")]
        public EquipmentGrade Grade;

        [Tooltip("드롭 확률 (0~1). 예: 0.8 = 80%")]
        [Range(0f, 1f)]
        public float DropRate;

        [Tooltip("이 등급에서 드롭될 수 있는 아이템 풀")]
        public EquipmentItemData[] Pool;
    }
}
