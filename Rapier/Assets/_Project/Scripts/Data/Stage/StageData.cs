using UnityEngine;
using Game.Core.Stage;

namespace Game.Data.Stage
{
    /// <summary>
    /// 스테이지 1개의 구성 데이터 SO.
    ///
    /// [필드]
    ///   _stageName      : 스테이지 표시 이름
    ///   _stageIndex     : 1-based 스테이지 번호
    ///   _rooms          : 방 배열 (IntermissionRoom + BossRoom × 4)
    ///   _hpMultiplier   : 보스 기본 HP 배율
    ///   _atkMultiplier  : 보스 기본 ATK 배율
    ///
    /// [규칙]
    ///   SO 필드는 읽기 전용 프로퍼티로만 외부 노출. setter 금지.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Stage/StageData", fileName = "StageData")]
    public class StageData : ScriptableObject
    {
        [SerializeField] private string    _stageName;
        [SerializeField] private int       _stageIndex;
        [SerializeField] private RoomNode[] _rooms;
        [SerializeField] private float     _hpMultiplier  = 1f;
        [SerializeField] private float     _atkMultiplier = 1f;

        /// <summary>스테이지 표시 이름.</summary>
        public string    StageName     => _stageName;

        /// <summary>1-based 스테이지 번호.</summary>
        public int       StageIndex    => _stageIndex;

        /// <summary>방 배열 (InspectorOrder: Intermission→Boss×4 패턴).</summary>
        public RoomNode[] Rooms        => _rooms;

        /// <summary>보스 기본 HP에 곱하는 배율. 스테이지별 난이도 스케일링.</summary>
        public float     HpMultiplier  => _hpMultiplier;

        /// <summary>보스 기본 ATK에 곱하는 배율. 스테이지별 난이도 스케일링.</summary>
        public float     AtkMultiplier => _atkMultiplier;
    }
}
