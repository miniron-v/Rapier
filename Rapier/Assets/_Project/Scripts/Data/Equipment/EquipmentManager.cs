using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 장착/해제/룬 서비스. 인벤토리 및 캐릭터별 장착 세트를 관리한다.
    /// Phase 13-B: Init() 시 ServiceLocator 에 등록하여 씬 간 공유.
    /// Dispose() 시 ServiceLocator 에서 해제.
    /// B3 연결 전까지 IEquipmentSaveProvider 스텁은 null 허용(저장 스킵).
    /// </summary>
    public class EquipmentManager
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

        // ── 내부 상태 ────────────────────────────────────────────────────────

        // 전체 보유 장비 인스턴스 인벤토리
        private readonly List<EquipmentInstance> _equipmentInventory = new();

        // 전체 보유 룬 인벤토리
        private readonly List<RuneItemData> _runeInventory = new();

        // 캐릭터 ID → 장착 세트
        private readonly Dictionary<string, CharacterEquipmentSet> _characterSets = new();

        // B3 저장 프로바이더 (null이면 저장 스킵)
        private IEquipmentSaveProvider _saveProvider;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 저장 프로바이더를 주입하고 ServiceLocator 에 자신을 등록한다.
        /// 씬 간 EquipmentManager 인스턴스를 공유하려면 반드시 이 메서드를 호출한다.
        /// B3 완성 전까지 saveProvider 는 null 가능.
        /// </summary>
        public void Init(IEquipmentSaveProvider saveProvider = null)
        {
            _saveProvider = saveProvider;

            // 이미 다른 인스턴스가 등록된 경우 중복 등록 방지 (로그는 ServiceLocator가 출력)
            var existing = ServiceLocator.Get<EquipmentManager>();
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
            var registered = ServiceLocator.Get<EquipmentManager>();
            if (registered == this)
                ServiceLocator.Unregister<EquipmentManager>();
        }

        // ── 인벤토리 접근 ────────────────────────────────────────────────────

        /// <summary>인벤토리에 장비를 추가한다.</summary>
        public void AddEquipmentToInventory(EquipmentInstance instance)
        {
            if (instance == null) return;
            _equipmentInventory.Add(instance);
        }

        /// <summary>인벤토리에서 장비를 제거한다.</summary>
        public bool RemoveEquipmentFromInventory(EquipmentInstance instance)
            => _equipmentInventory.Remove(instance);

        /// <summary>인벤토리에 룬을 추가한다.</summary>
        public void AddRuneToInventory(RuneItemData rune)
        {
            if (rune == null) return;
            _runeInventory.Add(rune);
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
            if (_saveProvider == null) return;
            _saveProvider.SaveInventory(_equipmentInventory, _runeInventory);
            foreach (var set in _characterSets.Values)
                _saveProvider.SaveCharacterEquipment(set);
        }
    }
}
