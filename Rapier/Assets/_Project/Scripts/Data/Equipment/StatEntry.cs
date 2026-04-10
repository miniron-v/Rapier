using System;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 단일 능력치 항목 (깡 또는 % 형태 모두 지원).
    /// </summary>
    [Serializable]
    public struct StatEntry
    {
        [Tooltip("능력치 종류")]
        public StatType statType;

        [Tooltip("깡 수치 (예: HP +200)")]
        public float flatValue;

        [Tooltip("비율 수치 (예: HP +20%). 0.2 = 20%")]
        public float percentValue;
    }
}
