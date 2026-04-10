using System;
using UnityEngine;

namespace Game.Data.Missions
{
    /// <summary>미션 보상 정의. SO 필드에 직렬화.</summary>
    [Serializable]
    public class MissionReward
    {
        [Tooltip("골드 보상량")]
        public int gold              = 0;
        [Tooltip("가챠 티켓 수")]
        public int gachaTicket       = 0;
        [Tooltip("강화 재료 수")]
        public int reinforceMaterial = 0;
        [Tooltip("룬 가챠 티켓 수")]
        public int runeGachaTicket   = 0;
        [Tooltip("에픽 장비 확정 수 (0=없음)")]
        public int epicEquipCount    = 0;
    }
}
