using UnityEngine;

namespace Game.Core.Stage
{
    /// <summary>
    /// 스테이지 한 방의 구성 데이터 노드.
    /// SO 또는 클래스로 RoomType과 보스 참조를 묶는다.
    /// StageBuilder가 배열로 보유하여 스테이지를 구성한다.
    /// </summary>
    [System.Serializable]
    public class RoomNode
    {
        /// <summary>방 종류 (보스 / 인터미션).</summary>
        public RoomType roomType;

        /// <summary>
        /// BossRoom일 때 스폰할 보스 프리팹.
        /// IntermissionRoom일 경우 null.
        /// </summary>
        public GameObject bossPrefab;

        /// <summary>
        /// BossRoom일 때 적용할 보스 스탯.
        /// IntermissionRoom일 경우 null.
        /// </summary>
        public Game.Enemies.BossStatData bossStatData;

        /// <summary>방 표시 이름 (디버그·HUD용).</summary>
        public string displayName;
    }
}
