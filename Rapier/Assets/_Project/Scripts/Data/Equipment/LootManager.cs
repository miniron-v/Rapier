using System.Collections.Generic;
using UnityEngine;

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
        /// - 최대 MaxDrops 개 제한 (기본 5): 상위 등급(Unique → Epic → Rare → Normal) 우선, 초과분 버림.<br/>
        /// - pool이 비어있거나 null인 항목은 스킵.
        /// </para>
        /// </summary>
        public List<EquipmentInstance> RollDrop(DropTableData dropTable)
        {
            var result = new List<EquipmentInstance>();

            if (dropTable == null)
                return result;

            var entries = dropTable.Entries;
            if (entries == null || entries.Count == 0)
                return result;

            // 상위 등급 우선 정렬을 위해 내부 버킷 사용 (Unique=3 → Normal=0 순)
            // DropEntry 리스트를 등급 내림차순으로 처리
            var candidates = new List<EquipmentInstance>();

            // 등급 높은 순으로 순회하기 위해 내림차순 정렬된 복사본 활용
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

                // 독립 확률 판정
                if (Random.value <= entry.DropRate)
                {
                    // pool에서 랜덤 1개 선택
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
