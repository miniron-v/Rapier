using UnityEngine;

namespace Game.Data.Stage
{
    /// <summary>
    /// 전체 스테이지 목록 SO 레지스트리.
    ///
    /// Resources/StageDatabase.asset 에 배치하고
    /// <see cref="StageBuilder"/> 가 <c>Resources.Load&lt;StageDatabase&gt;("StageDatabase")</c> 로 접근한다.
    ///
    /// [API]
    ///   GetStage(index) : 1-based 스테이지 인덱스로 StageData 반환. 범위 초과 시 null.
    ///   StageCount      : 등록된 스테이지 수.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Stage/StageDatabase", fileName = "StageDatabase")]
    public class StageDatabase : ScriptableObject
    {
        [SerializeField] private StageData[] _stages;

        /// <summary>등록된 스테이지 수.</summary>
        public int StageCount => _stages != null ? _stages.Length : 0;

        /// <summary>
        /// 1-based 스테이지 인덱스로 StageData를 반환한다.
        /// 범위 초과 또는 null 배열이면 null 반환.
        /// </summary>
        /// <param name="index">1-based 스테이지 번호.</param>
        public StageData GetStage(int index)
        {
            if (_stages == null || index < 1 || index > _stages.Length)
                return null;
            return _stages[index - 1];
        }
    }
}
