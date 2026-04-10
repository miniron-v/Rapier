using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.RunStats
{
    /// <summary>
    /// 인터미션 방에서 표시할 RunStat 2개를 무작위 추출하는 풀.
    /// 같은 종류가 동시에 나오지 않음을 보장 (DesignDoc §8-2).
    /// </summary>
    public static class StatPickPool
    {
        /// <summary>
        /// 7종 풀에서 서로 다른 2개의 RunStatEntry를 무작위 추출한다.
        /// </summary>
        public static (RunStatEntry first, RunStatEntry second) PickTwo()
        {
            var pool  = BuildPool();
            int total = pool.Count;

            if (total < 2)
                throw new InvalidOperationException("[StatPickPool] 풀 크기가 2 미만입니다.");

            int idxA = UnityEngine.Random.Range(0, total);
            int idxB;
            do { idxB = UnityEngine.Random.Range(0, total); }
            while (idxB == idxA);

            return (pool[idxA], pool[idxB]);
        }

        /// <summary>
        /// 7종 RunStat 풀을 생성한다. 효과 강도는 DesignDoc §8-2 기준.
        /// </summary>
        private static List<RunStatEntry> BuildPool()
        {
            return new List<RunStatEntry>
            {
                new RunStatEntry(RunStatType.HpPercent,                  0.25f, "체력 증가",       "HP +25%"),
                new RunStatEntry(RunStatType.AtkPercent,                 0.20f, "공격력 증가",      "ATK +20%"),
                new RunStatEntry(RunStatType.MsPercent,                  0.15f, "이동 속도",        "MS +15%"),
                new RunStatEntry(RunStatType.DodgeCdrPercent,            0.20f, "회피 쿨다운 감소", "DodgeCDR -20%"),
                new RunStatEntry(RunStatType.ChargeTimeReductionPercent, 0.15f, "차지 시간 단축",   "ChargeTime -15%"),
                new RunStatEntry(RunStatType.InvincibilityPercent,       0.30f, "무적 시간 증가",   "Invincibility +30%"),
                new RunStatEntry(RunStatType.CritChancePercent,          0.15f, "크리티컬 확률",    "CritChance +15%"),
            };
        }
    }

    /// <summary>
    /// 인터미션 스탯 카드 한 항목. 이름·설명·적용값을 담는다.
    /// </summary>
    public readonly struct RunStatEntry
    {
        /// <summary>적용할 RunStat 종류.</summary>
        public readonly RunStatType Type;

        /// <summary>1회 선택 시 누적되는 수치.</summary>
        public readonly float Value;

        /// <summary>카드 제목 (예: "체력 증가").</summary>
        public readonly string DisplayName;

        /// <summary>카드 부제 설명 (예: "HP +25%").</summary>
        public readonly string Description;

        public RunStatEntry(RunStatType type, float value, string displayName, string description)
        {
            Type        = type;
            Value       = value;
            DisplayName = displayName;
            Description = description;
        }
    }
}
