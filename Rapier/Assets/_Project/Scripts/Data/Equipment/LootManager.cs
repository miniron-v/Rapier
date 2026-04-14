using System.Collections.Generic;
using UnityEngine;
using Game.Data.Stage;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 드롭 판정 순수 C# 클래스. EquipmentManager 미참조 — 결과 반환만 담당.
    /// 호출자가 반환된 EquipmentInstance 목록을 인벤토리에 추가할 책임을 진다.
    /// </summary>
    public class LootManager
    {
        /// <summary>
        /// 드롭 테이블을 기반으로 드롭 판정을 수행하고 결과 인스턴스 목록을 반환한다.
        /// <para>
        /// - dropTable이 null이면 빈 리스트를 반환한다.<br/>
        /// - 등급별 독립 판정: 각 DropEntry의 DropRate를 Random.value와 비교.<br/>
        /// - stageDropRates가 지정되면 해당 등급의 드롭률을 오버라이드한다.<br/>
        /// - 최대 MaxDrops 개 제한 (기본 5): 상위 등급(Unique → Epic → Rare → Normal) 우선, 초과분 버림.<br/>
        /// - pool이 비어있거나 null인 항목은 스킵.
        /// </para>
        /// </summary>
        /// <param name="dropTable">보스의 드롭 테이블.</param>
        /// <param name="stageDropRates">스테이지 공통 등급별 드롭률 오버라이드. null 또는 빈 배열이면 DropTable 기본값 사용.</param>
        public List<EquipmentInstance> RollDrop(DropTableData dropTable, GradeDropRate[] stageDropRates = null)
        {
            var result = new List<EquipmentInstance>();

            if (dropTable == null)
                return result;

            var entries = dropTable.Entries;
            if (entries == null || entries.Count == 0)
                return result;

            // 상위 등급 우선 정렬을 위해 내부 버킷 사용 (Unique=3 → Normal=0 순)
            var candidates = new List<EquipmentInstance>();

            var sortedEntries = new List<DropEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                    sortedEntries.Add(entries[i]);
            }
            sortedEntries.Sort((a, b) => b.Grade.CompareTo(a.Grade));

            foreach (var entry in sortedEntries)
            {
                if (entry.Pool == null || entry.Pool.Length == 0)
                    continue;

                // 스테이지 오버라이드 드롭률 적용 (없으면 DropEntry 기본값)
                float rate = entry.DropRate;
                if (stageDropRates != null)
                {
                    foreach (var sr in stageDropRates)
                    {
                        if (sr.grade == entry.Grade)
                        {
                            rate = sr.dropRate;
                            break;
                        }
                    }
                }

                // 독립 확률 판정
                if (Random.value <= rate)
                {
                    var validPool = new List<EquipmentItemData>();
                    foreach (var item in entry.Pool)
                    {
                        if (item != null)
                            validPool.Add(item);
                    }

                    if (validPool.Count == 0)
                        continue;

                    var selected = validPool[Random.Range(0, validPool.Count)];
                    candidates.Add(new EquipmentInstance(selected));
                }
            }

            // 최대 MaxDrops 개 제한 (기본 5, 이미 상위 등급 순으로 정렬되어 있음)
            int take = Mathf.Min(candidates.Count, dropTable.MaxDrops);
            for (int i = 0; i < take; i++)
                result.Add(candidates[i]);

            return result;
        }
    }
}
