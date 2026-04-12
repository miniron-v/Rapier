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
        // ── 스키마 버전 상수 ────────────────────────────────────────
        /// <summary>현재 스키마 버전. SaveMigrator 단계와 반드시 쌍으로 유지.</summary>
        public const int CurrentSchemaVersion = 2;

        // ── 메타 (계정 연동 대비) ───────────────────────────────────
        /// <summary>스키마 버전. 로드 후 마이그레이션 시 CurrentSchemaVersion 으로 승격.</summary>
        public int    version         = 0;
        /// <summary>계정 연동 후 서버 식별자. 미연동 시 빈 문자열.</summary>
        public string userId          = "";
        /// <summary>기기 고유값. 최초 Save 시 SystemInfo.deviceUniqueIdentifier 로 채움.</summary>
        public string deviceId        = "";
        /// <summary>Unix epoch(ms). 모든 Save() 호출 직전에 갱신.</summary>
        public long   lastSavedAt     = 0;
        /// <summary>최초 Save 시점. 이후 변경 금지. 마이그레이션 디버깅용.</summary>
        public long   schemaCreatedAt = 0;

        // ── 캐릭터 ─────────────────────────────────────────────────
        public string             lastCharacterId  = "Rapier";
        public List<CharacterSaveEntry> characters = new();

        // ── 장비 ───────────────────────────────────────────────────
        public List<EquipmentSaveEntry>  ownedEquipment  = new();
        /// <summary>
        /// 캐릭터별 장착 상태. JsonUtility 는 Dictionary 를 직렬화하지 못하므로
        /// List&lt;EquippedMapEntry&gt; 로 저장하고, SaveManager 경계에서 Dictionary 와 상호 변환한다.
        /// </summary>
        public List<EquippedMapEntry> equippedMap = new();

        // ── 진행 ───────────────────────────────────────────────────
        /// <summary>클리어한 가장 높은 스테이지 번호 (1-based). 0 = 아직 클리어 없음.</summary>
        public int            highestClearedStage = 0;
        /// <summary>레거시 필드. highestClearedStage 로 통합됨. 마이그레이션 v1→v2 에서 흡수.</summary>
        [Obsolete("highestClearedStage 를 사용할 것. v2 마이그레이션에서 제거 예정.")]
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
        /// <summary>스킬별 레벨. JsonUtility 는 Dictionary 를 직렬화하지 못하므로 List 로 저장.</summary>
        public List<SkillLevelEntry> skillLevels = new();
    }

    [Serializable]
    public class SkillLevelEntry
    {
        public string skillId = "";
        public int    level   = 1;
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
    public class EquippedMapEntry
    {
        public string       characterId = "";
        public List<string> instanceIds = new();
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
