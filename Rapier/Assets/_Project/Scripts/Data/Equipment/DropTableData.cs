using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 보스별 드롭 테이블 ScriptableObject.
    /// 각 DropEntry는 등급별 드롭 확률과 아이템 풀을 정의한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Equipment/DropTableData", fileName = "DropTableData")]
    public class DropTableData : ScriptableObject
    {
        [SerializeField] private List<DropEntry> _entries = new List<DropEntry>();

        /// <summary>
        /// [Deprecated] 구 알고리즘(등급 독립 판정)에서 결과 개수 상한으로 사용하던 필드.
        /// 신규 알고리즘(BALANCE §4 개수 가중 롤 — 1:20% / 2:60% / 3:20%)에서는 미사용.
        /// 외부 참조 호환성을 위해 필드·프로퍼티는 유지하되, LootManager.RollDrop 내에서는 참조하지 않는다.
        /// </summary>
        [SerializeField] private int _maxDrops = 5;

        /// <summary>드롭 항목 목록 (읽기 전용).</summary>
        public IReadOnlyList<DropEntry> Entries => _entries;

        /// <summary>
        /// [Deprecated] 구 알고리즘 드롭 개수 상한. 신규 알고리즘(개수 가중 롤)에서는 미사용.
        /// </summary>
        public int MaxDrops => _maxDrops;
    }
}
