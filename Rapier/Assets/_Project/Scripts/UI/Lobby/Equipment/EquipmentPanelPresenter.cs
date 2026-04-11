using System.Collections.Generic;
using Game.Data.Equipment;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 장비 패널 Presenter. EquipmentManager(Model)와 IEquipmentPanelView(View) 사이를 중재한다.
    /// 장착/해제/룬 관리 로직을 담당한다.
    /// </summary>
    public class EquipmentPanelPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private EquipmentPanelView _view;

        // ── Private Fields ───────────────────────────────────────────────────

        private bool _isInitialized;
        private EquipmentManager _manager;
        private string _characterId;

        // 현재 선택된 슬롯 (인벤토리에서 아이템 선택 시 이 슬롯에 장착)
        private EquipmentSlotType? _selectedSlot;

        // ── Properties ──────────────────────────────────────────────────────

        /// <summary>
        /// Init 이 한 번 이상 호출되었는지 여부. 단방향 플래그 (true → false 전환 없음).
        /// CharacterTabPresenter 가 중복 초기화를 방지하기 위해 사용한다.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 런타임 생성 시 View 참조를 외부에서 주입한다 (LobbyHudSetup 에서 호출).
        /// </summary>
        /// <param name="view">연결할 EquipmentPanelView.</param>
        public void InitReferences(EquipmentPanelView view)
        {
            _view = view;
        }

        /// <summary>수동 DI. B1 또는 12-E에서 호출한다.</summary>
        public void Init(EquipmentManager manager, string characterId)
        {
            _manager        = manager;
            _characterId    = characterId;
            _isInitialized  = true;
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            SubscribeViewEvents();
            SubscribeManagerEvents();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeViewEvents();
            UnsubscribeManagerEvents();
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>패널을 표시한다.</summary>
        public void Show()
        {
            _view.SetVisible(true);
            RefreshAll();
        }

        /// <summary>패널을 숨긴다.</summary>
        public void Hide()
        {
            _view.SetVisible(false);
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_manager == null) return;

            // 8슬롯 갱신
            var set = _manager.GetCharacterSet(_characterId);
            var equipped = new Dictionary<EquipmentSlotType, EquipmentInstance>();
            foreach (var pair in set.GetAllEquipped())
                equipped[pair.Key] = pair.Value;
            _view.RefreshSlots(equipped);

            // 인벤토리 갱신
            _view.RefreshInventory(_manager.EquipmentInventory);
        }

        private void SubscribeViewEvents()
        {
            if (_view == null) return;
            _view.OnSlotClicked          += HandleSlotClicked;
            _view.OnInventoryItemClicked += HandleInventoryItemClicked;
            _view.OnRuneSocketClicked    += HandleRuneSocketClicked;
        }

        private void UnsubscribeViewEvents()
        {
            if (_view == null) return;
            _view.OnSlotClicked          -= HandleSlotClicked;
            _view.OnInventoryItemClicked -= HandleInventoryItemClicked;
            _view.OnRuneSocketClicked    -= HandleRuneSocketClicked;
        }

        private void SubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnEquipped       += HandleManagerEquipped;
            _manager.OnUnequipped     += HandleManagerUnequipped;
            _manager.OnRuneEquipped   += HandleManagerRuneEquipped;
            _manager.OnRuneUnequipped += HandleManagerRuneUnequipped;
            _manager.OnInventoryChanged += HandleInventoryChanged;
        }

        private void UnsubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnEquipped       -= HandleManagerEquipped;
            _manager.OnUnequipped     -= HandleManagerUnequipped;
            _manager.OnRuneEquipped   -= HandleManagerRuneEquipped;
            _manager.OnRuneUnequipped -= HandleManagerRuneUnequipped;
            _manager.OnInventoryChanged -= HandleInventoryChanged;
        }

        private void HandleInventoryChanged() => RefreshAll();

        // ── Event Handlers (View → Presenter) ────────────────────────────────

        private void HandleSlotClicked(EquipmentSlotType slot)
        {
            // 슬롯 선택 토글: 이미 선택된 슬롯 재클릭 시 해제
            if (_selectedSlot == slot)
            {
                _selectedSlot = null;
                _view.SetSlotSelected(slot, false);
            }
            else
            {
                if (_selectedSlot.HasValue)
                    _view.SetSlotSelected(_selectedSlot.Value, false);
                _selectedSlot = slot;
                _view.SetSlotSelected(slot, true);
            }
        }

        private void HandleInventoryItemClicked(EquipmentInstance instance)
        {
            if (_manager == null || instance == null) return;

            // 슬롯 미선택 상태이면 아이템의 슬롯 타입에 맞는 슬롯에 자동 장착
            var targetSlot = _selectedSlot ?? instance.Data.SlotType;

            // 슬롯 타입 불일치 시 무시
            if (instance.Data.SlotType != targetSlot)
            {
                Debug.Log($"[EquipmentPanelPresenter] 슬롯 타입 불일치: {instance.Data.SlotType} → {targetSlot}");
                return;
            }

            _manager.Equip(_characterId, instance);

            // 선택 해제
            if (_selectedSlot.HasValue)
            {
                _view.SetSlotSelected(_selectedSlot.Value, false);
                _selectedSlot = null;
            }
        }

        private void HandleRuneSocketClicked(EquipmentSlotType slot, int socketIndex)
        {
            // 현재는 해제 동작만 수행 (룬 선택 UI는 12-E에서 연결)
            _manager?.UnequipRune(_characterId, slot, socketIndex);
        }

        // ── Event Handlers (Manager → Presenter) ─────────────────────────────

        private void HandleManagerEquipped(string characterId, EquipmentSlotType slot,
                                           EquipmentInstance instance)
        {
            if (characterId != _characterId) return;
            RefreshAll();
        }

        private void HandleManagerUnequipped(string characterId, EquipmentSlotType slot,
                                             EquipmentInstance instance)
        {
            if (characterId != _characterId) return;
            RefreshAll();
        }

        private void HandleManagerRuneEquipped(string characterId, EquipmentSlotType slot,
                                               int socketIndex, RuneItemData rune)
        {
            if (characterId != _characterId) return;
            RefreshAll();
        }

        private void HandleManagerRuneUnequipped(string characterId, EquipmentSlotType slot,
                                                  int socketIndex)
        {
            if (characterId != _characterId) return;
            RefreshAll();
        }
    }
}
