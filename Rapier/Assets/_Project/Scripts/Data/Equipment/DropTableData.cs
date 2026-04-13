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

        /// <summary>드롭 항목 목록 (읽기 전용).</summary>
        public IReadOnlyList<DropEntry> Entries => _entries;
    }
}
