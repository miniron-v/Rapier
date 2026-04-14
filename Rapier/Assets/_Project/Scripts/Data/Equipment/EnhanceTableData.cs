using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 강화 테이블 ScriptableObject. 강화 단계별 성공률(%)과 가루 비용을 보유한다.
    /// length = 15, index 0 = +1 강화.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Equipment/EnhanceTableData")]
    public class EnhanceTableData : ScriptableObject
    {
        [Tooltip("강화 단계별 성공률(%). length=15, index 0 = +1")]
        [SerializeField] private int[] _successPercent;

        [Tooltip("강화 단계별 가루 비용. length=15, index 0 = +1")]
        [SerializeField] private int[] _dustCost;

        /// <summary>
        /// targetLevel(1-based)에 대응하는 성공률(%)을 반환한다.
        /// 범위 밖이면 clamp.
        /// </summary>
        public int GetSuccessPercent(int targetLevel)
        {
            if (_successPercent == null || _successPercent.Length == 0) return 0;
            int idx = Mathf.Clamp(targetLevel - 1, 0, _successPercent.Length - 1);
            return _successPercent[idx];
        }

        /// <summary>
        /// targetLevel(1-based)에 대응하는 가루 비용을 반환한다.
        /// 범위 밖이면 clamp.
        /// </summary>
        public int GetDustCost(int targetLevel)
        {
            if (_dustCost == null || _dustCost.Length == 0) return int.MaxValue;
            int idx = Mathf.Clamp(targetLevel - 1, 0, _dustCost.Length - 1);
            return _dustCost[idx];
        }
    }
}
