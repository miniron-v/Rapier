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
    /// - B2 IEquipmentSaveProvider를 통해 장비 직렬화.
    /// </summary>
    public class SaveManager
    {
        private const string SAVE_FILE_NAME = "save.json";

        private readonly string _savePath;
        private IEquipmentSaveProvider _equipProvider;

        // ── 현재 로드된 데이터 ──────────────────────────────────────
        private SaveData _current;
        public SaveData Current => _current;

        /// <summary>저장 완료 이벤트.</summary>
        public event Action OnSaved;

        /// <summary>로드 완료 이벤트.</summary>
        public event Action OnLoaded;

        public SaveManager()
        {
            _savePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
            _current  = new SaveData();
        }

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>
        /// 장비 저장 제공자를 주입한다. (B2 연결용)
        /// Save()/Load() 호출 전에 주입해야 장비 데이터가 포함된다.
        /// </summary>
        public void SetEquipmentProvider(IEquipmentSaveProvider provider)
        {
            _equipProvider = provider;
        }

        /// <summary>
        /// 현재 인메모리 데이터를 JSON 파일로 저장한다.
        /// 장비 프로바이더가 설정된 경우 장비 상태도 직렬화한다.
        /// </summary>
        public void Save()
        {
            // 장비 직렬화 (B2 연결)
            if (_equipProvider != null)
            {
                _current.ownedEquipment = _equipProvider.SerializeOwnedEquipment();
                _current.equippedMap    = _equipProvider.SerializeEquippedMap();
            }

            try
            {
                string json = JsonUtility.ToJson(_current, prettyPrint: true);
                // using 블록으로 파일 핸들 즉시 닫힘 보장
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
        }

        /// <summary>
        /// JSON 파일을 읽어 인메모리 데이터를 복원한다.
        /// 파일 없음 / 손상 시 기본값(new SaveData)으로 초기화한다.
        /// 장비 프로바이더가 설정된 경우 장비 역직렬화도 수행한다.
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
                // using 블록으로 파일 핸들 즉시 닫힘 보장
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

            // 장비 역직렬화 (B2 연결)
            if (_equipProvider != null)
            {
                _equipProvider.DeserializeOwnedEquipment(_current.ownedEquipment);
                _equipProvider.DeserializeEquippedMap(_current.equippedMap);
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
    }
}
