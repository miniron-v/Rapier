using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Save;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 장착/해제/룬 서비스. 인벤토리 및 캐릭터별 장착 세트를 관리한다.
    /// Phase 15-A: Init(saveManager, database) 로 SaveManager 직접 배선.
    /// Equip/Unequip/EquipRune/UnequipRune 내부에서 TrySave → SaveManager.Save() 체인.
    /// IEquipmentSaveProvider (Game.Data.Save) 를 직접 구현하여 SaveManager 에 제공한다.
    /// </summary>
    public class EquipmentManager : Game.Data.Save.IEquipmentSaveProvider
    {
        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>장비 장착 이벤트 (캐릭터 ID, 슬롯, 새 인스턴스)</summary>
        public event Action<string, EquipmentSlotType, EquipmentInstance> OnEquipped;

        /// <summary>장비 해제 이벤트 (캐릭터 ID, 슬롯, 해제된 인스턴스)</summary>
        public event Action<string, EquipmentSlotType, EquipmentInstance> OnUnequipped;

        /// <summary>룬 장착 이벤트 (캐릭터 ID, 슬롯, 소켓 인덱스, 룬)</summary>
        public event Action<string, EquipmentSlotType, int, RuneItemData> OnRuneEquipped;

        /// <summary>룬 해제 이벤트 (캐릭터 ID, 슬롯, 소켓 인덱스)</summary>
        public event Action<string, EquipmentSlotType, int> OnRuneUnequipped;

        /// <summary>
        /// 인벤토리(장비/룬) 내용이 변경되었을 때 발행.
        /// AddEquipmentToInventory / RemoveEquipmentFromInventory / AddRuneToInventory 에서 호출된다.
        /// Deserialize 경로는 Presenter 구독 이전이므로 이벤트를 발행하지 않는다.
        /// </summary>
        public event Action OnInventoryChanged;

        // ── 내부 상태 ────────────────────────────────────────────────────────

        // 전체 보유 장비 인스턴스 인벤토리
        private readonly List<EquipmentInstance> _equipmentInventory = new();

        // 전체 보유 룬 인벤토리
        private readonly List<RuneItemData> _runeInventory = new();

        // 캐릭터 ID → 장착 세트
        private readonly Dictionary<string, CharacterEquipmentSet> _characterSets = new();

        // SaveManager 주입 필드 (null이면 저장 스킵).
        private Game.Data.Save.SaveManager _saveManager;

        // Phase 14: SO 레지스트리 (Deserialize 에서 assetId → SO 조회)
        private EquipmentDatabase _database;

        // Phase 14: 현재 프로젝트에 구현된 캐릭터 ID 화이트리스트.
        // equippedMap 복원 시 이 집합에 없는 키는 "미구현" 으로 판정되어 스킵된다 (§7-5 방어 로직).
        // 향후 Warrior/Assassin/Ranger 추가 시 여기에 등록할 것.
        // PascalCase 리터럴 정책으로 통일 — OrdinalIgnoreCase 비교자 불필요.
        private static readonly HashSet<string> _implementedCharacters
            = new HashSet<string> { "Rapier" };

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>
        /// SaveManager 와 SO 레지스트리를 주입하고 ServiceLocator 에 자신을 등록한다.
        /// 씬 간 EquipmentManager 인스턴스를 공유하려면 반드시 이 메서드를 호출한다.
        /// <para>
        /// <paramref name="saveManager"/> 는 Equip/Unequip 시 TrySave → Save() 체인에 사용된다.
        /// <paramref name="database"/> 가 null 이면 Deserialize 단계에서 모든 항목이 스킵된다 (경고만, 예외 없음).
        /// </para>
        /// </summary>
        public void Init(Game.Data.Save.SaveManager saveManager = null, EquipmentDatabase database = null)
        {
            _saveManager = saveManager;
            _database    = database;

            if (_database == null)
                Debug.LogWarning("[EquipmentManager] EquipmentDatabase is null — all deserialize will skip.");

            // 이미 다른 인스턴스가 등록된 경우 중복 등록 방지 (경고 없이 조회)
            var existing = ServiceLocator.TryGet<EquipmentManager>();
            if (existing != null && existing != this)
            {
                // 기존 인스턴스가 남아있음 — 새 인스턴스로 교체하지 않고 자신을 폐기
                Debug.LogWarning("[EquipmentManager] ServiceLocator에 이미 다른 인스턴스 등록됨. Init 스킵.");
                return;
            }
            ServiceLocator.Register(this);
        }

        /// <summary>
        /// ServiceLocator 에서 자신을 해제한다.
        /// 장비 시스템이 필요 없어지는 시점(앱 종료, 씬 전체 초기화 등)에 호출한다.
        /// </summary>
        public void Dispose()
        {
            var registered = ServiceLocator.TryGet<EquipmentManager>();
            if (registered == this)
                ServiceLocator.Unregister<EquipmentManager>();
        }

        // ── 인벤토리 접근 ────────────────────────────────────────────────────

        /// <summary>인벤토리에 장비를 추가한다.</summary>
        public void AddEquipmentToInventory(EquipmentInstance instance)
        {
            if (instance == null) return;
            _equipmentInventory.Add(instance);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>인벤토리에서 장비를 제거한다.</summary>
        public bool RemoveEquipmentFromInventory(EquipmentInstance instance)
        {
            bool removed = _equipmentInventory.Remove(instance);
            if (removed) OnInventoryChanged?.Invoke();
            return removed;
        }

        /// <summary>인벤토리에 룬을 추가한다.</summary>
        public void AddRuneToInventory(RuneItemData rune)
        {
            if (rune == null) return;
            _runeInventory.Add(rune);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>보유 장비 인벤토리 (읽기 전용)</summary>
        public IReadOnlyList<EquipmentInstance> EquipmentInventory => _equipmentInventory;

        /// <summary>보유 룬 인벤토리 (읽기 전용)</summary>
        public IReadOnlyList<RuneItemData> RuneInventory => _runeInventory;

        // ── 캐릭터 장착/해제 ─────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터에 장비를 장착한다.
        /// 다른 캐릭터가 이미 이 인스턴스를 장착하고 있으면 자동 해제.
        /// </summary>
        public bool Equip(string characterId, EquipmentInstance instance)
        {
            if (string.IsNullOrEmpty(characterId) || instance == null)
            {
                Debug.LogWarning("[EquipmentManager] Equip: 유효하지 않은 인수");
                return false;
            }

            // 다른 캐릭터가 이 인스턴스를 장착 중이면 먼저 해제
            foreach (var (cid, set) in _characterSets)
            {
                if (cid == characterId) continue;
                var current = set.GetEquipped(instance.Data.SlotType);
                if (current == instance)
                {
                    set.Unequip(instance.Data.SlotType);
                    OnUnequipped?.Invoke(cid, instance.Data.SlotType, instance);
                }
            }

            var targetSet = GetOrCreateSet(characterId);
            var displaced = targetSet.Equip(instance);

            if (displaced != null)
                OnUnequipped?.Invoke(characterId, displaced.Data.SlotType, displaced);

            OnEquipped?.Invoke(characterId, instance.Data.SlotType, instance);
            TrySave();
            return true;
        }

        /// <summary>캐릭터의 특정 슬롯 장비를 해제한다.</summary>
        public EquipmentInstance Unequip(string characterId, EquipmentSlotType slot)
        {
            if (!_characterSets.TryGetValue(characterId, out var set))
                return null;

            var instance = set.Unequip(slot);
            if (instance != null)
            {
                OnUnequipped?.Invoke(characterId, slot, instance);
                TrySave();
            }
            return instance;
        }

        /// <summary>캐릭터의 특정 슬롯에 장착된 장비를 반환한다. 없으면 null.</summary>
        public EquipmentInstance GetEquipped(string characterId, EquipmentSlotType slot)
        {
            if (!_characterSets.TryGetValue(characterId, out var set))
                return null;
            return set.GetEquipped(slot);
        }

        // ── 룬 장착/해제 ─────────────────────────────────────────────────────

        /// <summary>캐릭터 장비 슬롯의 소켓에 룬을 장착한다.</summary>
        public bool EquipRune(string characterId, EquipmentSlotType slot,
                              int socketIndex, RuneItemData rune)
        {
            if (!_characterSets.TryGetValue(characterId, out var set))
            {
                Debug.LogWarning($"[EquipmentManager] EquipRune: 캐릭터 {characterId} 세트 없음");
                return false;
            }

            bool ok = set.EquipRune(slot, socketIndex, rune);
            if (ok)
            {
                OnRuneEquipped?.Invoke(characterId, slot, socketIndex, rune);
                TrySave();
            }
            return ok;
        }

        /// <summary>캐릭터 장비 슬롯의 소켓에서 룬을 해제한다.</summary>
        public bool UnequipRune(string characterId, EquipmentSlotType slot, int socketIndex)
        {
            if (!_characterSets.TryGetValue(characterId, out var set))
                return false;

            bool ok = set.UnequipRune(slot, socketIndex);
            if (ok)
            {
                OnRuneUnequipped?.Invoke(characterId, slot, socketIndex);
                TrySave();
            }
            return ok;
        }

        // ── 캐릭터 세트 조회 ─────────────────────────────────────────────────

        /// <summary>캐릭터의 장착 세트를 반환한다. 없으면 빈 세트를 생성해 반환.</summary>
        public CharacterEquipmentSet GetCharacterSet(string characterId)
            => GetOrCreateSet(characterId);

        // ── 내부 헬퍼 ────────────────────────────────────────────────────────

        /// <summary>
        /// Deserialize 전용 내부 장착 경로. OnEquipped 이벤트를 발행하지 않는다.
        /// MetaStatProvider 는 CharacterPresenterBase.Init 시점에 1회 BuildContainer 를 호출하므로
        /// 초기화 페이즈에서는 이벤트 발행이 불필요하다 (§7-5).
        /// </summary>
        private void EquipInternal(CharacterEquipmentSet set, EquipmentInstance instance)
        {
            // CharacterEquipmentSet.Equip 은 기존 슬롯을 교체하고 이전 인스턴스를 반환.
            // Deserialize 페이즈에서는 이미 Clear 했으므로 displaced == null 이 정상.
            set.Equip(instance);
        }

        private CharacterEquipmentSet GetOrCreateSet(string characterId)
        {
            if (!_characterSets.TryGetValue(characterId, out var set))
            {
                set = new CharacterEquipmentSet(characterId);
                _characterSets[characterId] = set;
            }
            return set;
        }

        private void TrySave()
        {
            _saveManager?.Save();
        }

        // ── IEquipmentSaveProvider (Game.Data.Save) 구현 ────────────────────

        /// <summary>
        /// 현재 보유 장비 인벤토리를 직렬화 가능한 <see cref="EquipmentSaveEntry"/> 목록으로 반환한다.
        /// 직렬화는 호출 즉시 현재 메모리 상태를 반영한다.
        /// </summary>
        public List<EquipmentSaveEntry> SerializeOwnedEquipment()
        {
            var result = new List<EquipmentSaveEntry>(_equipmentInventory.Count);
            foreach (var instance in _equipmentInventory)
            {
                if (instance == null) continue;

                var entry = new EquipmentSaveEntry
                {
                    instanceId  = instance.InstanceId,
                    dataAssetId = instance.Data != null ? instance.Data.name : "",
                    grade       = (int)instance.Grade,
                    runeAssetIds = instance.EquippedRunes != null
                        ? instance.EquippedRunes
                            .Select(r => r != null ? r.name : "")
                            .ToList()
                        : new List<string>()
                };
                result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// 캐릭터별 장착 상태를 직렬화 가능한 형태로 반환한다.
        /// key = 캐릭터 ID, value = 해당 세트에서 장착된 EquipmentInstance 의 instanceId 목록.
        /// 직렬화는 호출 즉시 현재 메모리 상태를 반영한다.
        /// </summary>
        public Dictionary<string, List<string>> SerializeEquippedMap()
        {
            var map = new Dictionary<string, List<string>>(_characterSets.Count);
            foreach (var (characterId, set) in _characterSets)
            {
                var ids = new List<string>();
                foreach (var kv in set.GetAllEquipped())
                {
                    if (kv.Value != null)
                        ids.Add(kv.Value.InstanceId);
                }
                map[characterId] = ids;
            }
            return map;
        }

        /// <summary>
        /// 저장 데이터에서 장비 목록을 역직렬화하여 인벤토리를 복원한다.
        /// <para>
        /// §7-3 흐름: 인벤토리 초기화 → 각 엔트리를 DB 조회 → EquipmentInstance 재구성 → 추가.
        /// SO 에셋 누락 시 해당 엔트리만 스킵 (나머지 복원 계속). 크래시 없음.
        /// </para>
        /// </summary>
        public void DeserializeOwnedEquipment(List<EquipmentSaveEntry> entries)
        {
            // 1. 기존 상태 초기화 (빈 세이브 로드 포함 안전)
            _equipmentInventory.Clear();

            // 2. null/empty 방어
            if (entries == null) return;

            int restored = 0;

            // 3. 각 엔트리 복원
            foreach (var entry in entries)
            {
                if (entry == null) continue;

                // DB 조회
                var foundData = _database?.FindEquipment(entry.dataAssetId);
                if (foundData == null)
                {
                    Debug.LogWarning($"[EquipmentManager] Unknown equipment '{entry.dataAssetId}' — skipped.");
                    continue;
                }

                // EquipmentInstance 재구성 (내부 복원 생성자 사용 — §7-4: 저장값 Grade 우선)
                var grade    = (EquipmentGrade)entry.grade;
                var instance = new EquipmentInstance(entry.instanceId, foundData, grade);

                // 룬 소켓 복원
                if (entry.runeAssetIds != null)
                {
                    int socketCount = instance.EquippedRunes.Length;
                    int loopCount   = Math.Min(entry.runeAssetIds.Count, socketCount);
                    for (int i = 0; i < loopCount; i++)
                    {
                        string runeId = entry.runeAssetIds[i];
                        if (string.IsNullOrEmpty(runeId)) continue;

                        var rune = _database?.FindRune(runeId);
                        if (rune == null)
                        {
                            Debug.LogWarning($"[EquipmentManager] Unknown rune '{runeId}' on equipment '{entry.dataAssetId}' — socket cleared.");
                            // 해당 소켓은 null (장비 자체는 유지)
                        }
                        else
                        {
                            instance.EquipRune(i, rune);
                        }
                    }
                }

                _equipmentInventory.Add(instance);
                restored++;
            }

            // 4. 복원 결과 로그
            Debug.Log($"[EquipmentManager] Restored {restored}/{entries.Count} equipment items.");
        }

        /// <summary>
        /// 저장 데이터에서 장착 상태를 역직렬화하여 캐릭터 세트를 복원한다.
        /// <para>
        /// §7-5 흐름: 세트 초기화 → 캐릭터 존재 확인 → instanceId 로 인벤토리 조회 → EquipInternal 로 조용히 배치.
        /// OnEquipped 이벤트는 발행하지 않음 (MetaStatProvider 가 Init 시점 1회 BuildContainer 호출).
        /// </para>
        /// </summary>
        public void DeserializeEquippedMap(Dictionary<string, List<string>> map)
        {
            // 1. 모든 CharacterEquipmentSet 초기화 (빈 슬롯으로)
            foreach (var set in _characterSets.Values)
            {
                foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
                    set.Unequip(slot);
            }

            // 2. null/empty 방어
            if (map == null || map.Count == 0) return;

            // 3. 각 (characterId, instanceIdList) 처리
            foreach (var (characterId, instanceIdList) in map)
            {
                // 미구현 캐릭터 스킵 (화이트리스트 기반 — §7-5)
                if (!_implementedCharacters.Contains(characterId))
                {
                    Debug.LogWarning($"[EquipmentManager] Character '{characterId}' not implemented — equipped map entry skipped.");
                    continue;
                }

                if (instanceIdList == null) continue;

                // 유효한 캐릭터 — 세트가 없으면 생성 (Bootstrap 최초 Load 시 _characterSets 가 비어있음)
                var targetSet = GetOrCreateSet(characterId);
                foreach (var instanceId in instanceIdList)
                {
                    if (string.IsNullOrEmpty(instanceId)) continue;

                    var found = _equipmentInventory.FirstOrDefault(i => i.InstanceId == instanceId);
                    if (found == null)
                    {
                        Debug.LogWarning($"[EquipmentManager] Equipped instance '{instanceId}' not in owned inventory — slot skipped.");
                        continue;
                    }

                    // 이벤트 발행 없이 조용히 슬롯 배치 (EquipInternal)
                    EquipInternal(targetSet, found);
                }
            }
        }
    }
}
