using System;
using UnityEngine;

namespace Game.Data.Save
{
    /// <summary>
    /// 버전별 마이그레이션 체인.
    /// 로드 직후 <see cref="SaveManager.Load"/> 에서 호출된다.
    /// 새 스키마 버전 추가 시 PROGRESSION.md §5 마이그레이션 규약과 쌍으로 갱신.
    /// </summary>
    public static class SaveMigrator
    {
        /// <summary>
        /// <paramref name="data"/> 의 version 이 <see cref="SaveData.CurrentSchemaVersion"/> 미만이면
        /// 단계별 마이그레이션을 수행한다.
        /// </summary>
        /// <returns>마이그레이션이 1회 이상 수행되어 재저장이 필요한 경우 true.</returns>
        public static bool TryMigrate(SaveData data)
        {
            if (data == null) return false;

            bool migrated = false;

            while (data.version < SaveData.CurrentSchemaVersion)
            {
                switch (data.version)
                {
                    case 0:
                        MigrateV0ToV1(data);
                        break;
                    case 1:
                        MigrateV1ToV2(data);
                        break;
                    case 2:
                        MigrateV2ToV3(data);
                        break;
                    case 3:
                        MigrateV3ToV4(data);
                        break;
                    default:
                        // 알 수 없는 버전 — 루프 탈출로 무한 루프 방지
                        Debug.LogError($"[SaveMigrator] 알 수 없는 버전: {data.version}. 마이그레이션 중단.");
                        return migrated;
                }
                migrated = true;
            }

            return migrated;
        }

        // ── v0 → v1 ───────────────────────────────────────────────

        /// <summary>
        /// v0(version 필드 없음) → v1 마이그레이션.
        /// - deviceId 초기화
        /// - schemaCreatedAt 초기화
        /// - PlayerPrefs 레거시 Progress_CurrentStage 흡수 및 삭제
        /// </summary>
        private static void MigrateV0ToV1(SaveData data)
        {
            Debug.Log("[SaveMigrator] v0 → v1 마이그레이션 시작");

            // deviceId 초기화
            if (string.IsNullOrEmpty(data.deviceId))
                data.deviceId = SystemInfo.deviceUniqueIdentifier;

            // schemaCreatedAt 초기화
            if (data.schemaCreatedAt == 0)
                data.schemaCreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // PlayerPrefs 레거시 흡수: HomeTabPresenter 가 사용하던 Progress_CurrentStage
            const string legacyKey = "Progress_CurrentStage";
            if (PlayerPrefs.HasKey(legacyKey))
            {
                int legacyStage = PlayerPrefs.GetInt(legacyKey, 0);
#pragma warning disable CS0618
                data.highestStage = Math.Max(data.highestStage, legacyStage);
                Debug.Log($"[SaveMigrator] PlayerPrefs '{legacyKey}'={legacyStage} 흡수 → highestStage={data.highestStage}");
#pragma warning restore CS0618
                PlayerPrefs.DeleteKey(legacyKey);
                PlayerPrefs.Save();
            }

            data.version = 1;
            Debug.Log("[SaveMigrator] v0 → v1 마이그레이션 완료");
        }

        // ── v1 → v2 ───────────────────────────────────────────────

        /// <summary>
        /// v1 → v2 마이그레이션.
        /// - highestStage(레거시) → highestClearedStage 흡수.
        /// </summary>
        private static void MigrateV1ToV2(SaveData data)
        {
            Debug.Log("[SaveMigrator] v1 → v2 마이그레이션 시작");

#pragma warning disable CS0618
            // highestStage 레거시 흡수
            if (data.highestStage > 0 && data.highestClearedStage == 0)
            {
                data.highestClearedStage = data.highestStage;
                Debug.Log($"[SaveMigrator] highestStage={data.highestStage} → highestClearedStage={data.highestClearedStage} 흡수");
            }
            data.highestStage = 0; // 레거시 초기화
#pragma warning restore CS0618

            data.version = 2;
            Debug.Log("[SaveMigrator] v1 → v2 마이그레이션 완료");
        }

        // ── v2 → v3 ───────────────────────────────────────────────

        /// <summary>
        /// v2 → v3 마이그레이션.
        /// - crystal, epicPityCounter, uniquePityCounter 신규 필드 추가.
        ///   JsonUtility 역직렬화 시 기본값 0으로 초기화되므로 명시 작업 불필요.
        /// </summary>
        private static void MigrateV2ToV3(SaveData data)
        {
            Debug.Log("[SaveMigrator] v2 → v3 마이그레이션 시작");
            // 신규 int 필드(crystal, epicPityCounter, uniquePityCounter)는
            // JsonUtility 역직렬화 시 기본값 0으로 초기화되므로 명시 작업 불필요.
            data.version = 3;
            Debug.Log("[SaveMigrator] v2 → v3 마이그레이션 완료");
        }

        // ── v3 → v4 ───────────────────────────────────────────────

        /// <summary>
        /// v3 → v4 마이그레이션.
        /// - EquipmentSaveEntry.isLocked 신규 bool 필드 추가.
        ///   JsonUtility 역직렬화 시 기본값 false 로 초기화되므로 명시 작업 불필요.
        /// </summary>
        private static void MigrateV3ToV4(SaveData data)
        {
            Debug.Log("[SaveMigrator] v3 → v4 마이그레이션 시작");
            // isLocked 신규 bool 필드 — JsonUtility 역직렬화 시 기본값 false로 초기화되므로 명시 작업 불필요.
            data.version = 4;
            Debug.Log("[SaveMigrator] v3 → v4 마이그레이션 완료");
        }
    }
}
