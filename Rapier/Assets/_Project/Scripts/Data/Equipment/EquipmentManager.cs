using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Save;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 강화 결과를 나타내는 읽기 전용 구조체 (Phase 25-A).
    /// </summary>
    public readonly struct EnhanceResult
    {
        /// <summary>강화 성공 여부</summary>
        public readonly bool Success;
        /// <summary>소모된 가루</summary>
        public readonly int DustSpent;
        /// <summary>강화 전 단계</summary>
        public readonly int PreviousLevel;
        /// <summary>강화 후 단계 (실패 시 PreviousLevel 과 동일)</summary>
        public readonly int NewLevel;
        /// <summary>서브스탯이 업그레이드되었는지 여부</summary>
        public readonly bool HasUpgradedSubStat;
        /// <summary>업그레이드된 서브스탯 타입 (HasUpgradedSubStat 이 true 일 때만 유효)</summary>
        public readonly StatType UpgradedSubStat;
        /// <summary>업그레이드된 서브스탯 가산량 (HasUpgradedSubStat 이 true 일 때만 유효)</summary>
        public readonly float UpgradedSubAmount;

        internal EnhanceResult(bool success, int dustSpent, int previousLevel, int newLevel,
                               bool hasUpgradedSubStat = false, StatType upgradedSubStat = default, float upgradedSubAmount = 0f)
        {
            Success             = success;
            DustSpent           = dustSpent;
            PreviousLevel       = previousLevel;
            NewLevel            = newLevel;
            HasUpgradedSubStat  = hasUpgradedSubStat;
            UpgradedSubStat     = upgradedSubStat;
            UpgradedSubAmount   = upgradedSubAmount;
        }
    }

    /// <summary>
    /// 장착/해제/룬 서비스. 인벤토리 및 캐릭터별 장착 세트를 관리한다.
    /// Phase 15-A: Init(saveManager, database) 로 SaveManager 직접 배선.
    /// Equip/Unequip/EquipRune/UnequipRune 내부에서 TrySave → SaveManager.Save() 체인.
    /// IEquipmentSaveProvider (Game.Data.Save) 를 직접 구현하여 SaveManager 에 제공한다.
    /// Phase 25-A: 분해(Dismantle) / 강화(TryEnhance) / 가루(Dust) API 추가.
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

        /// <summary>
        /// 장비 인벤토리가 분해 등으로 직접 변경될 때 발행 (Phase 25-A).
        /// Dismantle 완료 후 발행된다.
        /// </summary>
        public event Action OnEquipmentInventoryChanged;

        /// <summary>
        /// 가루 수량이 변경될 때 발행 (Phase 25-A). 파라미터 = 변경 후 가루 수량.
        /// </summary>
        public event Action<int> OnDustChanged;

        /// <summary>
        /// 강화 완료 시 발행 (Phase 25-A). 성공/실패 무관 (단, Max/비용부족은 발행 X).
        /// </summary>
        public event Action<EquipmentInstance, EnhanceResult> OnEquipmentEnhanced;

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

        // Phase 25-A: 강화 테이블 (null 이면 강화 안전 모드 — 항상 실패 반환)
        private EnhanceTableData _enhanceTable;

        // Phase 14: 현재 프로젝트에 구현된 캐릭터 ID 화이트리스트.
        // equippedMap 복원 시 이 집합에 없는 키는 "미구현" 으로 판정되어 스킵된다 (§7-5 방어 로직).
        // PascalCase 리터럴 정책으로 통일 — OrdinalIgnoreCase 비교자 불필요.
        // Phase 26-D: Warrior / Ranger 추가.
        private static readonly HashSet<string> _implementedCharacters
            = new HashSet<string> { "Rapier", "Assassin", "Warrior", "Ranger" };

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>
        /// SaveManager 와 SO 레지스트리를 주입하고 ServiceLocator 에 자신을 등록한다.
        /// 씬 간 EquipmentManager 인스턴스를 공유하려면 반드시 이 메서드를 호출한다.
        /// <para>
        /// <paramref name="saveManager"/> 는 Equip/Unequip 시 TrySave → Save() 체인에 사용된다.
        /// <paramref name="database"/> 가 null 이면 Deserialize 단계에서 모든 항목이 스킵된다 (경고만, 예외 없음).
        /// <paramref name="enhanceTable"/> 가 null 이면 강화 API 가 항상 실패 반환하는 안전 모드로 동작한다.
        /// </para>
        /// </summary>
        public void Init(Game.Data.Save.SaveManager saveManager = null, EquipmentDatabase database = null, EnhanceTableData enhanceTable = null)
        {
            _saveManager  = saveManager;
            _database     = database;
            _enhanceTable = enhanceTable;

            if (_database == null)
                Debug.LogWarning("[EquipmentManager] EquipmentDatabase is null — all deserialize will skip.");

            if (_enhanceTable == null)
                Debug.LogWarning("[EquipmentManager] EnhanceTableData is null — TryEnhance will always fail (safe mode).");

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

        // ── 가루 (Dust) API (Phase 25-A) ─────────────────────────────────────

        /// <summary>현재 보유 가루 수량.</summary>
        public int Dust => _saveManager?.Current?.dust ?? 0;

        /// <summary>
        /// 가루를 추가한다. TrySave() 및 OnDustChanged 이벤트를 자동 발행한다.
        /// </summary>
        public void AddDust(int amount)
        {
            if (_saveManager?.Current == null || amount <= 0) return;
            _saveManager.Current.dust += amount;
            TrySave();
            OnDustChanged?.Invoke(_saveManager.Current.dust);
        }

        /// <summary>
        /// 가루를 소모한다. 성공 시 true, 부족 시 false 반환.
        /// 성공 시 TrySave() 및 OnDustChanged 이벤트를 자동 발행한다.
        /// </summary>
        public bool TryConsumeDust(int amount)
        {
            if (_saveManager?.Current == null) return false;
            if (_saveManager.Current.dust < amount) return false;
            _saveManager.Current.dust -= amount;
            TrySave();
            OnDustChanged?.Invoke(_saveManager.Current.dust);
            return true;
        }

        // ── 분해 API (Phase 25-A) ─────────────────────────────────────────────

        /// <summary>
        /// 분해 시 획득할 가루 수량을 계산한다 (실제 분해 없음).
        /// </summary>
        public int CalculateDismantleYield(EquipmentInstance inst)
        {
            if (inst == null) return 0;
            (int baseYield, int perEnhance) = GetDismantleYield(inst.Grade);
            return baseYield + perEnhance * inst.EnhanceLevel;
        }

        /// <summary>
        /// 장비 목록을 분해하여 가루를 획득한다.
        /// 장착 중인 항목은 스킵 + LogWarning. 총 획득 가루를 반환한다.
        /// </summary>
        public int Dismantle(IEnumerable<EquipmentInstance> targets)
        {
            if (targets == null) return 0;

            int totalDust = 0;
            var toRemove  = new List<EquipmentInstance>();

            foreach (var inst in targets)
            {
                if (inst == null) continue;

                // 장착 중인 항목 스킵
                if (AnyCharacterEquipsInstance(inst))
                {
                    Debug.LogWarning($"[EquipmentManager] Dismantle skipped — instance '{inst.InstanceId}' is equipped.");
                    continue;
                }

                totalDust += CalculateDismantleYield(inst);
                toRemove.Add(inst);
            }

            // 인벤토리에서 제거
            foreach (var inst in toRemove)
                _equipmentInventory.Remove(inst);

            if (totalDust > 0)
                AddDust(totalDust);    // TrySave + OnDustChanged 내부 발행
            else
                TrySave();             // 인벤토리 변경만 저장

            OnEquipmentInventoryChanged?.Invoke();
            return totalDust;
        }

        /// <summary>
        /// 어떤 캐릭터든 해당 인스턴스를 장착 중이면 true 반환.
        /// </summary>
        private bool AnyCharacterEquipsInstance(EquipmentInstance inst)
        {
            foreach (var set in _characterSets.Values)
            {
                foreach (var kv in set.GetAllEquipped())
                {
                    if (kv.Value == inst) return true;
                }
            }
            return false;
        }

        /// <summary>등급별 분해 기본 가루와 강화당 보너스를 반환한다.</summary>
        private static (int baseYield, int perEnhance) GetDismantleYield(EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Normal => (5,   2),
                EquipmentGrade.Rare   => (20,  5),
                EquipmentGrade.Epic   => (80,  15),
                EquipmentGrade.Unique => (250, 40),
                _                    => (5,   2),
            };
        }

        // ── 강화 API (Phase 25-A) ─────────────────────────────────────────────

        /// <summary>
        /// 장비 인스턴스 강화를 시도한다.
        /// <list type="bullet">
        ///   <item>최대 강화 단계 도달 → Success=false, DustSpent=0 (이벤트 발행 X)</item>
        ///   <item>가루 부족 → Success=false, DustSpent=0 (이벤트 발행 X)</item>
        ///   <item>성공/실패 모두 이벤트 OnEquipmentEnhanced 발행</item>
        /// </list>
        /// </summary>
        public EnhanceResult TryEnhance(EquipmentInstance inst)
        {
            if (inst == null)
                return new EnhanceResult(false, 0, 0, 0);

            int prevLevel = inst.EnhanceLevel;
            int maxLevel  = inst.MaxEnhanceLevel;

            // 1. 최대 강화 단계 도달
            if (prevLevel >= maxLevel)
                return new EnhanceResult(false, 0, prevLevel, prevLevel);

            // 2. 강화 테이블 없음 (안전 모드)
            if (_enhanceTable == null)
            {
                Debug.LogWarning("[EquipmentManager] TryEnhance: EnhanceTableData is null — safe mode, always fail.");
                return new EnhanceResult(false, 0, prevLevel, prevLevel);
            }

            int targetLevel = prevLevel + 1;
            int cost        = _enhanceTable.GetDustCost(targetLevel);

            // 3. 가루 부족
            if (Dust < cost)
                return new EnhanceResult(false, 0, prevLevel, prevLevel);

            // 4. 가루 소모
            TryConsumeDust(cost);   // TrySave + OnDustChanged 내부 발행

            // 5. 성공률 굴림
            int successPercent = _enhanceTable.GetSuccessPercent(targetLevel);
            bool success = UnityEngine.Random.Range(0, 100) < successPercent;

            bool hasUpgraded    = false;
            StatType upgStat    = default;
            float    upgAmount  = 0f;

            if (success)
            {
                inst.SetEnhanceLevel(targetLevel);

                // 서브스탯 강화 (3·6·9·12·15 단계)
                if (IsSubStatEnhanceStep(targetLevel) && inst.SubStats != null && inst.SubStats.Count > 0)
                {
                    int pickedIndex = UnityEngine.Random.Range(0, inst.SubStats.Count);
                    var pickedSub   = inst.SubStats[pickedIndex];

                    // 풀에서 해당 서브스탯 엔트리 조회 → max × 0.25 가산
                    var pool = inst.Data?.SubStatPool;
                    if (pool != null)
                    {
                        StatRollEntry matchedEntry = null;
                        foreach (var e in pool.Entries)
                        {
                            if (e != null && e.statType == pickedSub.statType)
                            {
                                matchedEntry = e;
                                break;
                            }
                        }

                        if (matchedEntry != null)
                        {
                            float rangeMax = GetRangeMax(matchedEntry, inst.Grade);
                            float delta    = rangeMax * 0.25f;

                            if (matchedEntry.usePercent)
                                inst.EnhanceSubStat(pickedIndex, 0f, delta);
                            else
                                inst.EnhanceSubStat(pickedIndex, delta, 0f);

                            hasUpgraded = true;
                            upgStat     = pickedSub.statType;
                            upgAmount   = delta;
                        }
                        else
                        {
                            Debug.LogWarning($"[EquipmentManager] TryEnhance: SubStatPool 에서 {pickedSub.statType} 매칭 실패 — 서브스탯 강화 스킵.");
                        }
                    }
                }
            }

            TrySave();

            var result = new EnhanceResult(success, cost, prevLevel, inst.EnhanceLevel, hasUpgraded, upgStat, upgAmount);
            OnEquipmentEnhanced?.Invoke(inst, result);
            return result;
        }

        /// <summary>서브스탯 강화가 발생하는 단계(3·6·9·12·15)이면 true.</summary>
        private static bool IsSubStatEnhanceStep(int level)
        {
            return level == 3 || level == 6 || level == 9 || level == 12 || level == 15;
        }

        /// <summary>StatRollEntry 의 등급별 max 값을 반환한다.</summary>
        private static float GetRangeMax(StatRollEntry entry, EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Rare   => entry.rare.max,
                EquipmentGrade.Epic   => entry.epic.max,
                EquipmentGrade.Unique => entry.unique.max,
                _                    => entry.normal.max,
            };
        }

        // ── 잠금 API (Phase 27) ─────────────────────────────────────────────────

        /// <summary>아이템 잠금 상태를 설정하고 저장한다.</summary>
        public void SetItemLocked(EquipmentInstance instance, bool locked)
        {
            if (instance == null) return;
            instance.SetLocked(locked);
            TrySave();
            OnEquipmentInventoryChanged?.Invoke();
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

                // Phase 22-B: 서브스탯 롤 결과 직렬화
                var subStatsCopy = new List<StatEntry>();
                if (instance.SubStats != null)
                {
                    foreach (var sub in instance.SubStats)
                        subStatsCopy.Add(sub);
                }

                bool hasRolled = instance.RolledMainStat.HasValue;
                StatEntry rolledMainVal = hasRolled ? instance.RolledMainStat.Value : default;

                var entry = new EquipmentSaveEntry
                {
                    instanceId  = instance.InstanceId,
                    dataAssetId = instance.Data != null ? instance.Data.name : "",
                    grade       = (int)instance.Grade,
                    runeAssetIds = instance.EquippedRunes != null
                        ? instance.EquippedRunes
                            .Select(r => r != null ? r.name : "")
                            .ToList()
                        : new List<string>(),
                    // Phase 22-B
                    subStats      = subStatsCopy,
                    hasRolledMain = hasRolled,
                    rolledMain    = rolledMainVal,
                    // Phase 25-A
                    enhanceLevel  = instance.EnhanceLevel,
                    // Phase 27
                    isLocked      = instance.IsLocked,
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
                // Phase 22-B: 저장된 subStats / rolledMain 으로 복원 (재롤 없음)
                var grade    = (EquipmentGrade)entry.grade;
                var savedSubs = entry.subStats != null
                    ? new List<StatEntry>(entry.subStats)
                    : null;
                var instance = new EquipmentInstance(
                    entry.instanceId,
                    foundData,
                    grade,
                    savedSubs,
                    entry.hasRolledMain,
                    entry.rolledMain,
                    entry.enhanceLevel);   // Phase 25-A

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

                // Phase 27: 잠금 상태 복원
                instance.SetLocked(entry.isLocked);

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
