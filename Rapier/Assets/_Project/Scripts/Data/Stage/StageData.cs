using System;
using UnityEngine;
using Game.Data.Equipment;

namespace Game.Data.Stage
{
    /// <summary>
    /// 스테이지 키프레임 SO. 디스크에 저장되는 읽기 전용 데이터 정의.
    ///
    /// [필드]
    ///   _stageName    : 스테이지 표시 이름 (키프레임 식별용)
    ///   _stageIndex   : 1-based 스테이지 번호 (키프레임 위치)
    ///   _hpMultiplier : 보스 기본 HP 배율 (키프레임 값)
    ///   _atkMultiplier: 보스 기본 ATK 배율 (키프레임 값)
    ///
    /// 방 배열·드롭률 등 런타임 합성 값은 StageContext POCO 에 담긴다.
    ///
    /// [규칙] CLAUDE.md §7
    ///   SO 값은 런타임 불변. 외부 노출은 읽기 전용 프로퍼티로만. setter 금지.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Stage/StageData", fileName = "StageData")]
    public class StageData : ScriptableObject
    {
        [SerializeField] private string _stageName;
        [SerializeField] private int    _stageIndex;
        [SerializeField] private float  _hpMultiplier  = 1f;
        [SerializeField] private float  _atkMultiplier = 1f;

        /// <summary>스테이지 표시 이름 (키프레임 식별용).</summary>
        public string StageName     => _stageName;

        /// <summary>1-based 스테이지 번호 (키프레임 위치).</summary>
        public int    StageIndex    => _stageIndex;

        /// <summary>보스 기본 HP에 곱하는 키프레임 배율. StageDatabase 가 보간에 사용.</summary>
        public float  HpMultiplier  => _hpMultiplier;

        /// <summary>보스 기본 ATK에 곱하는 키프레임 배율. StageDatabase 가 보간에 사용.</summary>
        public float  AtkMultiplier => _atkMultiplier;
    }

    /// <summary>
    /// 스테이지별 등급 드롭률 오버라이드 항목. StageContext 가 소유.
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
