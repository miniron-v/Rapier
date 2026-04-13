using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 슬롯별 서브스탯 풀 ScriptableObject.
    /// 드롭 시 이 풀에서 중복 없이 N개를 추첨한다 (N = (int)grade).
    /// </summary>
    [CreateAssetMenu(
        fileName = "SubStatPoolData",
        menuName  = "Game/Data/Equipment/SubStatPoolData")]
    public class SubStatPoolData : ScriptableObject
    {
        [SerializeField] private EquipmentSlotType _slot;
        [SerializeField] private List<StatRollEntry> _entries = new();

        /// <summary>이 풀이 대응하는 장비 슬롯</summary>
        public EquipmentSlotType Slot => _slot;

        /// <summary>풀 엔트리 목록 (읽기 전용)</summary>
        public IReadOnlyList<StatRollEntry> Entries => _entries;
    }
}
