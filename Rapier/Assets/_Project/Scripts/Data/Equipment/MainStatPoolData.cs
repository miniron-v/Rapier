using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 장신구(Necklace/Ring) 메인스탯 풀 ScriptableObject.
    /// 드롭 시 이 풀에서 1회 추첨하여 RolledMainStat 을 결정한다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MainStatPoolData",
        menuName  = "Game/Data/Equipment/MainStatPoolData")]
    public class MainStatPoolData : ScriptableObject
    {
        [SerializeField] private List<StatRollEntry> _entries = new();

        /// <summary>풀 엔트리 목록 (읽기 전용)</summary>
        public IReadOnlyList<StatRollEntry> Entries => _entries;
    }
}
