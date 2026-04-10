using UnityEngine;

namespace Game.Data.Missions
{
    /// <summary>
    /// 미션 정의 ScriptableObject. §10 기준.
    /// SO 값은 런타임 불변. 진행 상태는 MissionProgress가 관리.
    /// 경로: Assets/_Project/30_ScriptableObjects/Missions/
    /// </summary>
    [CreateAssetMenu(
        fileName = "MissionData",
        menuName  = "Game/Data/Missions/MissionData")]
    public class MissionData : ScriptableObject
    {
        [Header("식별")]
        [SerializeField] private string      _missionId   = "";
        [SerializeField] private MissionType _missionType = MissionType.Daily;

        [Header("내용")]
        [SerializeField] [TextArea] private string _description = "";
        [SerializeField] private MissionEvent _trackEvent = MissionEvent.OnBossKilled;
        [SerializeField] private int _targetCount = 1;

        [Header("보상")]
        [SerializeField] private MissionReward _reward = new();

        // ── 읽기 전용 프로퍼티 ─────────────────────────────────────
        public string      MissionId   => _missionId;
        public MissionType MissionType => _missionType;
        public string      Description => _description;
        public MissionEvent TrackEvent  => _trackEvent;
        public int         TargetCount => _targetCount;
        public MissionReward Reward    => _reward;
    }
}
