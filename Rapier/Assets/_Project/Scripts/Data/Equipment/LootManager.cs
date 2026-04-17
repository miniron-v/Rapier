using System.Collections.Generic;
using UnityEngine;
using Game.Data.Stage;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 드롭 판정 순수 C# 클래스. EquipmentManager 미참조 — 결과 반환만 담당.
    /// 호출자가 반환된 EquipmentInstance 목록을 인벤토리에 추가할 책임을 진다.
    ///
    /// [알고리즘 — BALANCE §4]
    ///   1. 개수 N 가중 롤: 1개 20% / 2개 60% / 3개 20%
    ///   2. 각 드랍 슬롯마다 독립적으로 등급 롤 (stageGradeDropRates 우선, 없으면 DropTable 기본)
    ///   3. 확정 등급의 보스 전담 풀에서 1개 랜덤 선택
    /// </summary>
    public class LootManager
    {
        /// <summary>
        /// 드롭 테이블을 기반으로 드롭 판정을 수행하고 결과 인스턴스 목록을 반환한다.
        /// <para>
        /// - dropTable이 null이면 빈 리스트를 반환한다.<br/>
        /// - 개수 가중 롤: 1개 20% / 2개 60% / 3개 20% (BALANCE §4-1).<br/>
        /// - 등급 롤: stageGradeDropRates가 지정되면 해당 등급 확률을 우선 사용하고, 없으면 DropTable 기본값 사용 (BALANCE §3-2).<br/>
        /// - 확정 등급의 풀이 비어있거나 null인 경우 해당 슬롯은 스킵.<br/>
        /// - Rate가 모두 0인 경우 빈 리스트 반환.
        /// </para>
        /// </summary>
        /// <param name="dropTable">보스의 드롭 테이블.</param>
        /// <param name="stageGradeDropRates">
        /// 스테이지 공통 등급별 드롭률 오버라이드. null 또는 빈 목록이면 DropTable 기본값 사용.
        /// 합산 후 정규화하여 가중 롤에 사용한다.
        /// </param>
        public List<EquipmentInstance> RollDrop(DropTableData dropTable, IReadOnlyList<GradeDropRate> stageGradeDropRates = null)
        {
            if (dropTable == null)
                return new List<EquipmentInstance>();

            var entries = dropTable.Entries;
            if (entries == null || entries.Count == 0)
                return new List<EquipmentInstance>();

            // 1. 개수 N 가중 롤
            int count = RollDropCount();

            var result = new List<EquipmentInstance>(count);

            for (int i = 0; i < count; i++)
            {
                // 2. 등급 롤
                EquipmentGrade? grade = RollGrade(dropTable, stageGradeDropRates);
                if (!grade.HasValue)
                    continue;

                // 3. 해당 등급 보스 전담 풀에서 1개 선택
                var entry = FindEntry(dropTable, grade.Value);
                if (entry == null || entry.Pool == null || entry.Pool.Length == 0)
                    continue;

                // 유효 아이템만 추림
                var validPool = new List<EquipmentItemData>();
                foreach (var item in entry.Pool)
                {
                    if (item != null)
                        validPool.Add(item);
                }

                if (validPool.Count == 0)
                    continue;

                var selected = validPool[Random.Range(0, validPool.Count)];
                result.Add(new EquipmentInstance(selected));
            }

            return result;
        }

        /// <summary>
        /// 드랍 개수를 가중 롤로 결정한다.
        /// 1개: 20% / 2개: 60% / 3개: 20% (BALANCE §4-1).
        /// </summary>
        public static int RollDropCount()
        {
            float r = Random.value;
            if (r < 0.20f) return 1;
            if (r < 0.80f) return 2;  // 누적 20% + 60% = 80%
            return 3;                  // 나머지 20%
        }

        /// <summary>
        /// 가중 롤로 드랍 등급을 결정한다.
        /// stageRates가 있으면 해당 dropRate를 가중치로 사용하고, 없으면 DropTable.entries의 DropRate를 사용한다.
        /// 가중치 합이 0이거나 유효한 소스가 없으면 null을 반환한다.
        /// </summary>
        private static EquipmentGrade? RollGrade(DropTableData dropTable, IReadOnlyList<GradeDropRate> stageRates)
        {
            // 등급 수 = 4 (Normal=0, Rare=1, Epic=2, Unique=3)
            const int GRADE_COUNT = 4;
            var weights = new float[GRADE_COUNT];
            bool hasSource = false;

            if (stageRates != null && stageRates.Count > 0)
            {
                foreach (var r in stageRates)
                {
                    int idx = (int)r.grade;
                    if (idx >= 0 && idx < GRADE_COUNT)
                    {
                        weights[idx] = Mathf.Max(0f, r.dropRate);
                        hasSource = true;
                    }
                }
            }
            else
            {
                foreach (var e in dropTable.Entries)
                {
                    if (e == null) continue;
                    int idx = (int)e.Grade;
                    if (idx >= 0 && idx < GRADE_COUNT)
                    {
                        weights[idx] = Mathf.Max(0f, e.DropRate);
                        hasSource = true;
                    }
                }
            }

            if (!hasSource) return null;

            float total = 0f;
            for (int i = 0; i < GRADE_COUNT; i++)
                total += weights[i];

            if (total <= 0f) return null;

            float roll  = Random.value * total;
            float accum = 0f;
            for (int g = 0; g < GRADE_COUNT; g++)
            {
                accum += weights[g];
                if (roll <= accum)
                    return (EquipmentGrade)g;
            }

            // 부동소수점 오차 안전 폴백 — 최고 등급 반환
            return (EquipmentGrade)(GRADE_COUNT - 1);
        }

        /// <summary>
        /// 드랍 테이블에서 지정 등급의 DropEntry를 반환한다. 없으면 null.
        /// </summary>
        private static DropEntry FindEntry(DropTableData dropTable, EquipmentGrade grade)
        {
            foreach (var e in dropTable.Entries)
            {
                if (e != null && e.Grade == grade)
                    return e;
            }
            return null;
        }
    }
}
