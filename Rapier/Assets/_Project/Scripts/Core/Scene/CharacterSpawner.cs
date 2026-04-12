using System;
using UnityEngine;
using Game.Data.Save;

namespace Game.Core
{
    /// <summary>
    /// 게임 씬 진입 시 SaveData.lastCharacterId 에 따라 적절한 캐릭터 프리팹을 동적으로 스폰한다.
    ///
    /// [동작 흐름]
    ///   Start() → SaveManager.Current.lastCharacterId 읽기
    ///   → _entries 에서 매칭 entry 검색 (없으면 index 0 폴백)
    ///   → Instantiate(entry.prefab, transform.position, Quaternion.identity)
    ///   → ProgressionManager / BossRushManager 는 FindObjectOfType&lt;CharacterPresenterBase&gt;()
    ///     로 스폰된 캐릭터를 자동으로 찾으므로 별도 배선 불필요.
    ///
    /// [폴백 규칙]
    ///   - SaveManager 미등록(GameBootstrap 실행 전) 또는 매칭 entry 없음 → index 0 entry 사용.
    ///   - _entries 배열이 비어있으면 경고 후 종료.
    /// </summary>
    public class CharacterSpawner : MonoBehaviour
    {
        // ── 직렬화 필드 ──────────────────────────────────────────
        [SerializeField] private CharacterPrefabEntry[] _entries;

        // ── Unity 라이프사이클 ────────────────────────────────────
        private void Start()
        {
            if (_entries == null || _entries.Length == 0)
            {
                Debug.LogError("[CharacterSpawner] _entries 배열이 비어있습니다. 캐릭터를 스폰할 수 없습니다.");
                return;
            }

            // SaveManager 에서 lastCharacterId 읽기
            string characterId = GetLastCharacterId();

            // 매칭 entry 검색 (없으면 index 0 폴백)
            var entry = FindEntry(characterId);

            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[CharacterSpawner] '{characterId}' 에 대응하는 prefab 없음. index 0 폴백.");
                entry = _entries[0];
            }

            if (entry == null || entry.prefab == null)
            {
                Debug.LogError("[CharacterSpawner] index 0 entry 도 null 입니다. 스폰 불가.");
                return;
            }

            var spawned = Instantiate(entry.prefab, transform.position, Quaternion.identity);
            Debug.Log($"[CharacterSpawner] '{entry.characterId}' 스폰 완료 @ {transform.position}");
        }

        // ── Private 메서드 ────────────────────────────────────────
        private string GetLastCharacterId()
        {
            var saveManager = ServiceLocator.TryGet<SaveManager>();
            if (saveManager == null)
            {
                Debug.LogWarning("[CharacterSpawner] SaveManager 미등록. 기본값 'Rapier' 사용.");
                return "Rapier";
            }
            return saveManager.Current.lastCharacterId;
        }

        private CharacterPrefabEntry FindEntry(string characterId)
        {
            foreach (var entry in _entries)
            {
                if (entry != null && entry.characterId == characterId)
                    return entry;
            }
            return null;
        }
    }

    /// <summary>characterId → prefab 매핑 한 쌍.</summary>
    [Serializable]
    public class CharacterPrefabEntry
    {
        /// <summary>SaveData.lastCharacterId 와 일치해야 하는 식별자 문자열.</summary>
        public string     characterId;
        /// <summary>스폰할 캐릭터 프리팹.</summary>
        public GameObject prefab;
    }
}
