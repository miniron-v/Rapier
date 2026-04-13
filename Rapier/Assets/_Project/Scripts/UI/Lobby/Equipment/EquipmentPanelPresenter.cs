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
    /// </summary>
    public class EquipmentPanelPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private EquipmentPanelView         _view;
        [SerializeField] private ItemDetailPopupPresenter   _itemDetailPresenter;
        [SerializeField] private RuneInventoryPopupPresenter _runeInventoryPresenter;

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
            var set     = _manager.GetCharacterSet(_characterId);
            var equipped = new Dictionary<EquipmentSlotType, EquipmentInstance>();
            foreach (var pair in set.GetAllEquipped())
                equipped[pair.Key] = pair.Value;
            _view.RefreshSlots(equipped);

            // 인벤토리 갱신 — 현재 선택된 탭 기반으로 필터링
            _view.RefreshInventory(_manager.EquipmentInventory);
        }

        private void SubscribeViewEvents()
        {
            if (_view == null) return;
            _view.OnSlotClicked          += HandleSlotClicked;
            _view.OnInventoryItemClicked += HandleInventoryItemClicked;
            _view.OnRuneSocketClicked    += HandleRuneSocketClicked;
            _view.OnInventoryTabChanged  += HandleInventoryTabChanged;
        }

        private void UnsubscribeViewEvents()
        {
            if (_view == null) return;
            _view.OnSlotClicked          -= HandleSlotClicked;
            _view.OnInventoryItemClicked -= HandleInventoryItemClicked;
            _view.OnRuneSocketClicked    -= HandleRuneSocketClicked;
            _view.OnInventoryTabChanged  -= HandleInventoryTabChanged;
        }

        private void SubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnEquipped         += HandleManagerEquipped;
            _manager.OnUnequipped       += HandleManagerUnequipped;
            _manager.OnRuneEquipped     += HandleManagerRuneEquipped;
            _manager.OnRuneUnequipped   += HandleManagerRuneUnequipped;
            _manager.OnInventoryChanged += HandleInventoryChanged;
        }

        private void UnsubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnEquipped         -= HandleManagerEquipped;
            _manager.OnUnequipped       -= HandleManagerUnequipped;
            _manager.OnRuneEquipped     -= HandleManagerRuneEquipped;
            _manager.OnRuneUnequipped   -= HandleManagerRuneUnequipped;
            _manager.OnInventoryChanged -= HandleInventoryChanged;
        }

        private void HandleInventoryChanged() => RefreshAll();

        private void HandleInventoryTabChanged(EquipmentPanelView.InventoryTab tab)
        {
            // 탭 전환 → 인벤토리 목록 재필터링
            if (_manager == null) return;
            _view.RefreshInventory(_manager.EquipmentInventory);
        }

        // ── Event Handlers (View → Presenter) ────────────────────────────────

        private void HandleSlotClicked(EquipmentSlotType slot)
        {
            if (_manager == null) return;
            var instance = _manager.GetEquipped(_characterId, slot);
            if (instance == null) return;

            // 슬롯 클릭 → 상세 팝업 표시
            _itemDetailPresenter?.Show(instance, slot);
        }

        private void HandleInventoryItemClicked(EquipmentInstance instance)
        {
            if (_manager == null || instance == null) return;

            // Phase 24: 즉시 장착 제거 → 상세 팝업 표시
            // 어느 슬롯에 장착될지 결정 (아이템 슬롯 타입 기준)
            _itemDetailPresenter?.Show(instance, instance.Data.SlotType);
        }

        private void HandleRuneSocketClicked(EquipmentSlotType slot, int socketIndex)
        {
            if (_manager == null) return;
            var instance = _manager.GetEquipped(_characterId, slot);
            // 장비가 없어도 룬 인벤토리 팝업 표시 가능 (빈 소켓 context)
            _runeInventoryPresenter?.Show(instance, slot, socketIndex);
        }

        // ── Event Handlers (Manager → Presenter) ─────────────────────────────

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
