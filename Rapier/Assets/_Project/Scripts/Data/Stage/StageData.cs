using System;
using UnityEngine;
using Game.Core.Stage;
using Game.Data.Equipment;

namespace Game.Data.Stage
{
    /// <summary>
    /// 스테이지 1개의 구성 데이터 SO.
    ///
    /// [필드]
    ///   _stageName          : 스테이지 표시 이름
    ///   _stageIndex         : 1-based 스테이지 번호
    ///   _rooms              : 방 배열 (IntermissionRoom + BossRoom × 1, 키프레임 SO는 빈 배열)
    ///   _hpMultiplier       : 보스 기본 HP 배율
    ///   _atkMultiplier      : 보스 기본 ATK 배율
    ///   _gradeDropRates     : 스테이지 공통 등급별 드롭률 오버라이드
    ///
    /// [규칙]
    ///   SO 필드는 읽기 전용 프로퍼티로만 외부 노출. setter 금지.
    ///   런타임 합성 StageData는 InitForCompose() 로만 필드를 채운다 (ScriptableObject.CreateInstance 후 즉시 호출).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Stage/StageData", fileName = "StageData")]
    public class StageData : ScriptableObject
    {
        [SerializeField] private string    _stageName;
        [SerializeField] private int       _stageIndex;
        [SerializeField] private RoomNode[] _rooms;
        [SerializeField] private float     _hpMultiplier  = 1f;
        [SerializeField] private float     _atkMultiplier = 1f;

        [Header("등급별 드롭률 (스테이지 공통)")]
        [Tooltip("스테이지 내 모든 보스에 적용되는 등급별 드롭률 오버라이드.\n" +
                 "비어있으면 각 보스의 DropTableData 기본값 사용.")]
        [SerializeField] private GradeDropRate[] _gradeDropRates = new GradeDropRate[0];

        // ── 런타임 합성 전용 (BossVariantDatabase에서 주입) ────────────
        /// <summary>런타임 합성 시 BossVariantEntry.dropTable 을 여기에 캐싱한다. SO 디스크 에셋에서는 null.</summary>
        [NonSerialized] public DropTableData ComposedDropTable;

        /// <summary>스테이지 표시 이름.</summary>
        public string    StageName     => _stageName;

        /// <summary>1-based 스테이지 번호.</summary>
        public int       StageIndex    => _stageIndex;

        /// <summary>방 배열 (InspectorOrder: Intermission→Boss×1 패턴). 키프레임 SO는 빈 배열.</summary>
        public RoomNode[] Rooms        => _rooms;

        /// <summary>보스 기본 HP에 곱하는 배율. 스테이지별 난이도 스케일링.</summary>
        public float     HpMultiplier  => _hpMultiplier;

        /// <summary>보스 기본 ATK에 곱하는 배율. 스테이지별 난이도 스케일링.</summary>
        public float     AtkMultiplier => _atkMultiplier;

        /// <summary>
        /// 스테이지 공통 등급별 드롭률 오버라이드 목록.
        /// 비어있으면 각 보스의 DropTableData 기본값을 그대로 사용한다.
        /// </summary>
        public GradeDropRate[] GradeDropRates => _gradeDropRates;

        /// <summary>
        /// StageComposer.Compose() 가 ScriptableObject.CreateInstance&lt;StageData&gt;() 후 호출하는 내부 초기화.
        /// Reflection 없이 필드를 직접 채운다. 디스크 에셋에서는 절대 호출하지 않는다.
        /// </summary>
        internal void InitForCompose(
            int stageIndex, float hpMultiplier, float atkMultiplier,
            RoomNode[] rooms, GradeDropRate[] gradeDropRates, DropTableData dropTable)
        {
            _stageName      = $"Stage {stageIndex}";
            _stageIndex     = stageIndex;
            _hpMultiplier   = hpMultiplier;
            _atkMultiplier  = atkMultiplier;
            _rooms          = rooms ?? Array.Empty<RoomNode>();
            _gradeDropRates = gradeDropRates ?? Array.Empty<GradeDropRate>();
            ComposedDropTable = dropTable;
        }
    }

    /// <summary>
    /// 스테이지별 등급 드롭률 오버라이드 항목.
    /// </summary>
    [Serializable]
    public class GradeDropRate
    {
        [Tooltip("드롭률을 오버라이드할 등급")]
        public EquipmentGrade grade;

        [Tooltip("드롭 확률 (0~1). 예: 0.5 = 50%")]
        [Range(0f, 1f)]
        public float dropRate;
    }
}
