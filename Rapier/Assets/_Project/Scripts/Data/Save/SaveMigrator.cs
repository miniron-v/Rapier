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
                data.highestStage = Math.Max(data.highestStage, legacyStage);
                PlayerPrefs.DeleteKey(legacyKey);
                PlayerPrefs.Save();
                Debug.Log($"[SaveMigrator] PlayerPrefs '{legacyKey}'={legacyStage} 흡수 → highestStage={data.highestStage}");
            }

            data.version = 1;
            Debug.Log("[SaveMigrator] v0 → v1 마이그레이션 완료");
        }
    }
}
