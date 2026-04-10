using System;
using System.Collections.Generic;

namespace Game.Data.Save
{
    /// <summary>
    /// JSON 직렬화 루트 모델. §11-2 저장 항목 전체 포함.
    /// RunStat / 스테이지 내 진행 상태는 저장하지 않는다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // ── 캐릭터 ─────────────────────────────────────────────────
        public string             lastCharacterId  = "rapier";
        public List<CharacterSaveEntry> characters = new();

        // ── 장비 ───────────────────────────────────────────────────
        public List<EquipmentSaveEntry>  ownedEquipment  = new();
        /// <summary>key = characterId, value = 슬롯별 장비 instanceId 목록</summary>
        public Dictionary<string, List<string>> equippedMap = new();

        // ── 진행 ───────────────────────────────────────────────────
        public int            highestStage     = 0;
        public List<int>      clearedStages    = new();

        // ── 재화 ───────────────────────────────────────────────────
        public int  gold             = 0;
        public int  gachaTicket      = 0;
        public int  reinforceMaterial = 0;
        public int  runeGachaTicket  = 0;

        // ── 미션 ───────────────────────────────────────────────────
        public List<MissionProgressEntry> dailyMissions  = new();
        public List<MissionProgressEntry> weeklyMissions = new();
        public string lastDailyReset  = "";   // ISO 8601 UTC
        public string lastWeeklyReset = "";   // ISO 8601 UTC

        // ── 설정 ───────────────────────────────────────────────────
        public SettingsSaveData settings = new();
    }

    // ── 보조 데이터 클래스 ──────────────────────────────────────────

    [Serializable]
    public class CharacterSaveEntry
    {
        public string characterId = "";
        public int    level       = 1;
        /// <summary>스킬별 레벨. key = 스킬 ID</summary>
        public Dictionary<string, int> skillLevels = new();
    }

    [Serializable]
    public class EquipmentSaveEntry
    {
        public string instanceId   = "";   // GUID
        public string dataAssetId  = "";   // SO 에셋 이름
        public int    grade        = 0;    // EquipmentGrade enum int
        /// <summary>룬 소켓에 장착된 룬 에셋 이름 목록</summary>
        public List<string> runeAssetIds = new();
    }

    [Serializable]
    public class MissionProgressEntry
    {
        public string missionId    = "";
        public int    currentCount = 0;
        public bool   isCompleted  = false;
        public bool   isRewarded   = false;
    }

    [Serializable]
    public class SettingsSaveData
    {
        public float bgmVolume     = 1f;
        public float sfxVolume     = 1f;
        public bool  vibration     = true;
        public int   graphicsLevel = 1;   // 0=Low, 1=Mid, 2=High
    }
}
