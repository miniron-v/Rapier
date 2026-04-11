using System;
using System.IO;
using UnityEngine;

namespace Game.Data.Save
{
    /// <summary>
    /// JSON 저장/로드 진입점. §11 기준.
    /// - PlayerPrefs 미사용.
    /// - 저장 위치: Application.persistentDataPath/save.json
    /// - 로드 실패/손상 시 기본값 반환.
    /// - IEquipmentSaveProvider를 통해 장비 직렬화.
    /// - ISaveSyncService 배선 (Phase 13-B, 현재 LocalOnly).
    /// </summary>
    public class SaveManager
    {
        private const string SAVE_FILE_NAME = "save.json";

        private readonly string _savePath;
        private IEquipmentSaveProvider _equipProvider;

        /// <summary>동기화 서비스. 기본값 LocalOnlySaveSyncService (항상 Disabled).</summary>
        private ISaveSyncService _syncService;

        // 마이그레이션 중 재귀 Save 방지 플래그
        private bool _isMigrating;

        // ── 현재 로드된 데이터 ──────────────────────────────────────
        private SaveData _current;
        /// <summary>현재 인메모리 저장 데이터.</summary>
        public SaveData Current => _current;

        /// <summary>저장 완료 이벤트.</summary>
        public event Action OnSaved;

        /// <summary>로드 완료 이벤트.</summary>
        public event Action OnLoaded;

        public SaveManager()
        {
            _savePath   = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
            _current    = new SaveData();
            _syncService = new LocalOnlySaveSyncService();
        }

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>
        /// 장비 저장 제공자를 주입한다.
        /// Save()/Load() 호출 전에 주입해야 장비 데이터가 포함된다.
        /// </summary>
        public void SetEquipmentProvider(IEquipmentSaveProvider provider)
        {
            _equipProvider = provider;
        }

        /// <summary>
        /// 동기화 서비스를 주입한다. 미주입 시 LocalOnlySaveSyncService 사용.
        /// </summary>
        public void SetSyncService(ISaveSyncService service)
        {
            _syncService = service ?? new LocalOnlySaveSyncService();
        }

        /// <summary>
        /// 현재 인메모리 데이터를 JSON 파일로 저장한다.
        /// 메타 필드(deviceId, schemaCreatedAt, lastSavedAt)를 갱신한 후 직렬화한다.
        /// </summary>
        public void Save()
        {
            SaveInternal(invokeMigrator: false);
        }

        /// <summary>
        /// JSON 파일을 읽어 인메모리 데이터를 복원한다.
        /// 파일 없음/손상 시 기본값(new SaveData)으로 초기화한다.
        /// 로드 직후 SaveMigrator.TryMigrate를 호출하며, 마이그레이션 발생 시 즉시 재저장한다.
        /// </summary>
        public void Load()
        {
            if (!File.Exists(_savePath))
            {
                Debug.Log("[SaveManager] Save file not found. Using defaults.");
                _current = new SaveData();
                OnLoaded?.Invoke();
                return;
            }

            try
            {
                string json;
                using (var reader = new StreamReader(_savePath))
                {
                    json = reader.ReadToEnd();
                }

                var loaded = JsonUtility.FromJson<SaveData>(json);
                if (loaded == null) throw new Exception("Deserialized null.");

                _current = loaded;
                Debug.Log($"[SaveManager] Loaded ← {_savePath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Load failed ({ex.Message}). Using defaults.");
                _current = new SaveData();
            }

            // 장비 역직렬화
            if (_equipProvider != null)
            {
                _equipProvider.DeserializeOwnedEquipment(_current.ownedEquipment);
                _equipProvider.DeserializeEquippedMap(_current.equippedMap);
            }

            // 마이그레이션: 버전 미만이면 단계별 업그레이드 후 재저장
            _isMigrating = true;
            bool migrated = SaveMigrator.TryMigrate(_current);
            _isMigrating = false;

            if (migrated)
            {
                Debug.Log("[SaveManager] 마이그레이션 완료 → 재저장");
                SaveInternal(invokeMigrator: false);
            }

            OnLoaded?.Invoke();
        }

        /// <summary>
        /// 저장 파일을 삭제하고 인메모리 데이터를 기본값으로 초기화한다.
        /// </summary>
        public void DeleteSave()
        {
            if (File.Exists(_savePath))
            {
                File.Delete(_savePath);
                Debug.Log("[SaveManager] Save file deleted.");
            }
            _current = new SaveData();
        }

        // ── 내부 저장 ─────────────────────────────────────────────

        /// <param name="invokeMigrator">true 이면 저장 전 마이그레이션 재실행 (현재 미사용).</param>
        private void SaveInternal(bool invokeMigrator)
        {
            if (_isMigrating)
            {
                // 마이그레이션 중 재귀 호출 방지
                Debug.LogWarning("[SaveManager] 마이그레이션 중 Save 재귀 호출 차단.");
                return;
            }

            // 장비 직렬화
            if (_equipProvider != null)
            {
                _current.ownedEquipment = _equipProvider.SerializeOwnedEquipment();
                _current.equippedMap    = _equipProvider.SerializeEquippedMap();
            }

            // 메타 필드 갱신
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _current.lastSavedAt = nowMs;

            if (_current.schemaCreatedAt == 0)
                _current.schemaCreatedAt = nowMs;

            if (string.IsNullOrEmpty(_current.deviceId))
                _current.deviceId = SystemInfo.deviceUniqueIdentifier;

            // 버전 승격 (신규 데이터는 Load 없이 바로 Save 하는 경우 대비)
            if (_current.version < SaveData.CurrentSchemaVersion)
                _current.version = SaveData.CurrentSchemaVersion;

            try
            {
                string json = JsonUtility.ToJson(_current, prettyPrint: true);
                using (var writer = new StreamWriter(_savePath, append: false))
                {
                    writer.Write(json);
                }
                OnSaved?.Invoke();
                Debug.Log($"[SaveManager] Saved → {_savePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
            }

            // TODO (Phase 14+): _syncService.PushAsync(_current.userId, _current) で서버 동기화
            // 현재는 LocalOnlySaveSyncService → Disabled 반환으로 무시
        }
    }
}
