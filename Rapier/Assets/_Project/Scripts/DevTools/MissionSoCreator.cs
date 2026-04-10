#if UNITY_EDITOR
using System.IO;
using Game.Data.Missions;
using UnityEditor;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// §10 기준 일일 5종 + 주간 3종 MissionData SO를 자동 생성하는 에디터 유틸리티.
    /// Menu: Rapier/Create Mission SO Assets
    /// </summary>
    public static class MissionSoCreator
    {
        private const string OUTPUT_DIR = "Assets/_Project/ScriptableObjects/Missions";

        [MenuItem("Rapier/Create Mission SO Assets")]
        public static void CreateAll()
        {
            EnsureDir(OUTPUT_DIR);

            // ── 일일 미션 ──────────────────────────────────────────
            CreateMission("daily_01", MissionType.Daily, MissionEvent.OnStageCleared,
                "스테이지 1회 클리어", 1,
                gold: 500, gachaTicket: 1);

            CreateMission("daily_02", MissionType.Daily, MissionEvent.OnBossKilled,
                "보스 5마리 처치", 5,
                gold: 300);

            CreateMission("daily_03", MissionType.Daily, MissionEvent.OnJustDodgeTriggered,
                "저스트 회피 3회 성공", 3,
                gold: 200, reinforceMaterial: 5);

            CreateMission("daily_04", MissionType.Daily, MissionEvent.OnChargeSkillUsed,
                "차지 스킬 10회 사용", 10,
                gold: 200);

            CreateMission("daily_05", MissionType.Daily, MissionEvent.OnDailyMissionCompleted,
                "일일 미션 4개 완료", 4,
                gachaTicket: 1);

            // ── 주간 미션 ──────────────────────────────────────────
            CreateMission("weekly_01", MissionType.Weekly, MissionEvent.OnBossKilled,
                "보스 50마리 누적 처치", 50,
                gold: 3000, gachaTicket: 5);

            CreateMission("weekly_02", MissionType.Weekly, MissionEvent.OnStageRecordUpdated,
                "새 스테이지 도달 또는 최고 기록 갱신", 1,
                runeGachaTicket: 3);

            CreateMission("weekly_03", MissionType.Weekly, MissionEvent.OnDailyAllCompleted,
                "일일 미션 7일 모두 완료", 7,
                epicEquipCount: 1);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MissionSoCreator] Mission SO assets created.");
        }

        private static void CreateMission(string id, MissionType type, MissionEvent trackEvent,
            string desc, int targetCount,
            int gold = 0, int gachaTicket = 0, int reinforceMaterial = 0,
            int runeGachaTicket = 0, int epicEquipCount = 0)
        {
            string path = $"{OUTPUT_DIR}/{id}.asset";
            if (File.Exists(Path.Combine(Application.dataPath, path.Replace("Assets/", ""))))
            {
                Debug.Log($"[MissionSoCreator] Skip (exists): {path}");
                return;
            }

            var so = ScriptableObject.CreateInstance<MissionData>();

            // SerializedObject로 private SerializeField 설정
            var serialized = new SerializedObject(so);
            serialized.FindProperty("_missionId").stringValue   = id;
            serialized.FindProperty("_missionType").enumValueIndex = (int)type;
            serialized.FindProperty("_description").stringValue = desc;
            serialized.FindProperty("_trackEvent").enumValueIndex = (int)trackEvent;
            serialized.FindProperty("_targetCount").intValue    = targetCount;

            var rewardProp = serialized.FindProperty("_reward");
            rewardProp.FindPropertyRelative("gold").intValue              = gold;
            rewardProp.FindPropertyRelative("gachaTicket").intValue       = gachaTicket;
            rewardProp.FindPropertyRelative("reinforceMaterial").intValue = reinforceMaterial;
            rewardProp.FindPropertyRelative("runeGachaTicket").intValue   = runeGachaTicket;
            rewardProp.FindPropertyRelative("epicEquipCount").intValue    = epicEquipCount;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(so, path);
        }

        private static void EnsureDir(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
