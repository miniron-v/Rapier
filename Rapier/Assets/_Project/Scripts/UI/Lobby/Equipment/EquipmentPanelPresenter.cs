using System;
using System.Collections.Generic;
using Game.Data.Equipment;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 장비 패널 Presenter. EquipmentManager(Model) 와 View 계층 사이를 중재한다.
    ///
    /// Phase 24 변경:
    ///   - 인벤토리 아이템 클릭 → "즉시 장착" 제거, ItemDetailPopupPresenter 위임
    ///   - SlotType 기반 인벤토리 탭 필터링 (무기/방어구/장신구) 추가
    ///   - 룬 소켓 클릭 → RuneInventoryPopupPresenter 위임
    ///
    /// Phase 25-B 변경:
    ///   - EquipmentActionBarPresenter 연결
    ///   - 분해 모드 진입/종료 시 슬롯 시각화 위임
    ///   - 롱프레스 → ItemDetailPopupPresenter.Show (분해 모드 시 TODO 주석)
    ///   - 탭 전환 시 ActionBar 에 현재 탭 알림
    ///   - Hide 시 분해 모드 자동 종료
    /// </summary>
    public class EquipmentPanelPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private EquipmentPanelView          _view;
        [SerializeField] private ItemDetailPopupPresenter    _itemDetailPresenter;
        [SerializeField] private RuneInventoryPopupPresenter _runeInventoryPresenter;
        [SerializeField] private EquipmentActionBarPresenter _actionBarPresenter;

        // ── Private Fields ───────────────────────────────────────────────────

        private bool               _isInitialized;
        private EquipmentManager   _manager;
        private string             _characterId;

        // ── Properties ──────────────────────────────────────────────────────

        /// <summary>
        /// Init 이 한 번 이상 호출되었는지 여부.
        /// CharacterTabPresenter 가 중복 초기화를 방지하기 위해 사용한다.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>런타임 생성 시 View 참조를 주입한다 (LobbyHudSetup 에서 호출).</summary>
        public void InitReferences(
            EquipmentPanelView         view,
            ItemDetailPopupPresenter   itemDetailPresenter,
            RuneInventoryPopupPresenter runeInventoryPresenter)
        {
            _view                    = view;
            _itemDetailPresenter     = itemDetailPresenter;
            _runeInventoryPresenter  = runeInventoryPresenter;
        }

        /// <summary>Phase 25-B: ActionBarPresenter 추가 주입.</summary>
        public void InitActionBar(EquipmentActionBarPresenter actionBarPresenter)
        {
            _actionBarPresenter = actionBarPresenter;
        }

        /// <summary>
        /// 하위 호환: view 단독 주입 경로 (기존 LobbyHudSetup 호환).
        /// </summary>
        public void InitReferences(EquipmentPanelView view)
        {
            _view = view;
        }

        /// <summary>수동 DI. CharacterTabPresenter 에서 호출한다.</summary>
        public void Init(EquipmentManager manager, string characterId)
        {
            _manager       = manager;
            _characterId   = characterId;
            _isInitialized = true;

            _itemDetailPresenter?.Init(manager, characterId);
            _runeInventoryPresenter?.Init(manager, characterId);
            _actionBarPresenter?.Init(manager, characterId);
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            SubscribeViewEvents();
            SubscribeManagerEvents();
            SubscribeActionBarEvents();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeViewEvents();
            UnsubscribeManagerEvents();
            UnsubscribeActionBarEvents();
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
            // OnDisable 에서 ActionBarPresenter.OnDisable 이 분해 모드를 자동 종료한다.
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_manager == null) return;

            // 8슬롯 갱신
            var set      = _manager.GetCharacterSet(_characterId);
            var equipped = new Dictionary<EquipmentSlotType, EquipmentInstance>();
            foreach (var pair in set.GetAllEquipped())
                equipped[pair.Key] = pair.Value;
            _view.RefreshSlots(equipped);

            // ActionBar 컨텍스트 갱신
            _actionBarPresenter?.UpdateInventoryContext(_manager.EquipmentInventory, equipped);

            // 인벤토리 갱신 — 분해 모드 상태/선택 집합을 View 에 전달
            bool isDismantle      = _actionBarPresenter?.IsDismantleMode ?? false;
            var  selectedSet      = _actionBarPresenter?.SelectedInstances
                                     ?? (IReadOnlyCollection<EquipmentInstance>)
                                        Array.Empty<EquipmentInstance>();
            _view.SetDismantleMode(isDismantle, equipped, selectedSet);
            _view.RefreshInventory(_manager.EquipmentInventory);
        }

        private void SubscribeViewEvents()
        {
            if (_view == null) return;
            _view.OnSlotClicked                  += HandleSlotClicked;
            _view.OnInventoryItemClicked         += HandleInventoryItemClicked;
            _view.OnRuneSocketClicked            += HandleRuneSocketClicked;
            _view.OnInventoryTabChanged          += HandleInventoryTabChanged;
            _view.OnInventoryItemDismantleToggled+= HandleInventoryItemDismantleToggled;
            _view.OnInventoryItemLongPressed     += HandleInventoryItemLongPressed;
            _view.OnRequestInventoryRefresh      += HandleRequestInventoryRefresh;
        }

        private void UnsubscribeViewEvents()
        {
            if (_view == null) return;
            _view.OnSlotClicked                  -= HandleSlotClicked;
            _view.OnInventoryItemClicked         -= HandleInventoryItemClicked;
            _view.OnRuneSocketClicked            -= HandleRuneSocketClicked;
            _view.OnInventoryTabChanged          -= HandleInventoryTabChanged;
            _view.OnInventoryItemDismantleToggled-= HandleInventoryItemDismantleToggled;
            _view.OnInventoryItemLongPressed     -= HandleInventoryItemLongPressed;
            _view.OnRequestInventoryRefresh      -= HandleRequestInventoryRefresh;
        }

        private void SubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnEquipped                   += HandleManagerEquipped;
            _manager.OnUnequipped                 += HandleManagerUnequipped;
            _manager.OnRuneEquipped               += HandleManagerRuneEquipped;
            _manager.OnRuneUnequipped             += HandleManagerRuneUnequipped;
            _manager.OnInventoryChanged           += HandleInventoryChanged;
            _manager.OnEquipmentInventoryChanged  += HandleInventoryChanged;
        }

        private void UnsubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnEquipped                   -= HandleManagerEquipped;
            _manager.OnUnequipped                 -= HandleManagerUnequipped;
            _manager.OnRuneEquipped               -= HandleManagerRuneEquipped;
            _manager.OnRuneUnequipped             -= HandleManagerRuneUnequipped;
            _manager.OnInventoryChanged           -= HandleInventoryChanged;
            _manager.OnEquipmentInventoryChanged  -= HandleInventoryChanged;
        }

        private void SubscribeActionBarEvents()
        {
            if (_actionBarPresenter == null) return;
            _actionBarPresenter.OnDismantleModeChanged += HandleDismantleModeChanged;
            _actionBarPresenter.OnSelectionChanged     += HandleSelectionChanged;
        }

        private void UnsubscribeActionBarEvents()
        {
            if (_actionBarPresenter == null) return;
            _actionBarPresenter.OnDismantleModeChanged -= HandleDismantleModeChanged;
            _actionBarPresenter.OnSelectionChanged     -= HandleSelectionChanged;
        }

        // ── Event Handlers (View → Presenter) ────────────────────────────────

        private void HandleSlotClicked(EquipmentSlotType slot)
        {
            if (_manager == null) return;
            var instance = _manager.GetEquipped(_characterId, slot);
            if (instance == null) return;

            _itemDetailPresenter?.Show(instance, slot);
        }

        private void HandleInventoryItemClicked(EquipmentInstance instance)
        {
            if (_manager == null || instance == null) return;

            // Phase 24: 즉시 장착 제거 → 상세 팝업 표시
            _itemDetailPresenter?.Show(instance, instance.Data.SlotType);
        }

        private void HandleInventoryItemDismantleToggled(EquipmentInstance instance)
        {
            // 분해 모드 선택 토글 → ActionBarPresenter 위임
            _actionBarPresenter?.ToggleSelection(instance);
        }

        private void HandleInventoryItemLongPressed(EquipmentInstance instance)
        {
            if (_manager == null || instance == null) return;

            bool isDismantle = _actionBarPresenter?.IsDismantleMode ?? false;

            if (isDismantle)
            {
                // TODO Phase 25-C 머지 후 disableActions:true 로 변경
                _itemDetailPresenter?.Show(instance, instance.Data.SlotType);
            }
            else
            {
                _itemDetailPresenter?.Show(instance, instance.Data.SlotType);
            }
        }

        private void HandleRuneSocketClicked(EquipmentSlotType slot, int socketIndex)
        {
            if (_manager == null) return;
            var instance = _manager.GetEquipped(_characterId, slot);
            _runeInventoryPresenter?.Show(instance, slot, socketIndex);
        }

        private void HandleInventoryTabChanged(EquipmentPanelView.InventoryTab tab)
        {
            // 탭 전환 → ActionBar 에 탭 알림
            _actionBarPresenter?.NotifyTabChanged(tab);

            if (_manager == null) return;
            _view.RefreshInventory(_manager.EquipmentInventory);
        }

        private void HandleRequestInventoryRefresh()
        {
            if (_manager == null) return;
            _view.RefreshInventory(_manager.EquipmentInventory);
        }

        // ── Event Handlers (ActionBar → Presenter) ────────────────────────────

        private void HandleDismantleModeChanged(bool isDismantle)
        {
            if (_manager == null) return;
            var set      = _manager.GetCharacterSet(_characterId);
            var equipped = new Dictionary<EquipmentSlotType, EquipmentInstance>();
            foreach (var pair in set.GetAllEquipped())
                equipped[pair.Key] = pair.Value;

            var selectedSet = _actionBarPresenter?.SelectedInstances
                               ?? (IReadOnlyCollection<EquipmentInstance>)
                                  Array.Empty<EquipmentInstance>();

            _view.SetDismantleMode(isDismantle, equipped, selectedSet);
            _view.RefreshInventory(_manager.EquipmentInventory);
        }

        private void HandleSelectionChanged(
            IReadOnlyCollection<EquipmentInstance> selectedSet)
        {
            _view.RefreshSelectionVisuals(selectedSet);
            _view.RefreshInventory(_manager?.EquipmentInventory
                                   ?? Array.Empty<EquipmentInstance>());
        }

        // ── Event Handlers (Manager → Presenter) ─────────────────────────────

        private void HandleInventoryChanged() => RefreshAll();

        private void HandleManagerEquipped(string characterId, EquipmentSlotType slot, EquipmentInstance instance)
        {
            if (characterId != _characterId) return;
            RefreshAll();
        }

        private void HandleManagerUnequipped(string characterId, EquipmentSlotType slot, EquipmentInstance instance)
        {
            if (characterId != _characterId) return;
            RefreshAll();
        }

        private void HandleManagerRuneEquipped(string characterId, EquipmentSlotType slot, int socketIndex, RuneItemData rune)
        {
            if (characterId != _characterId) return;
            RefreshAll();
        }

        private void HandleManagerRuneUnequipped(string characterId, EquipmentSlotType slot, int socketIndex)
        {
            if (characterId != _characterId) return;
            RefreshAll();
        }
    }
}
