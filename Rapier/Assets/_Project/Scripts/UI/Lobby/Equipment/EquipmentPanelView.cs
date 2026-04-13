using System;
using System.Collections.Generic;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 장비 패널 전체 View. 8슬롯 그리드 + 인벤토리 3탭(무기/방어구/장신구)을 표시한다.
    /// IEquipmentPanelView 구현. 로직 없음.
    ///
    /// Phase 24 변경:
    ///   - 인벤토리 탭 3개 (무기/방어구/장신구) 추가
    ///   - 탭 전환 이벤트 OnInventoryTabChanged 추가
    ///   - RefreshInventory 는 현재 탭에 맞게 필터링된 목록만 표시
    /// </summary>
    public class EquipmentPanelView : MonoBehaviour, IEquipmentPanelView
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("8 슬롯 뷰 (순서: Weapon/Hat/Top/Bottom/Shoes/Gloves/Necklace/Ring)")]
        [SerializeField] private List<EquipmentSlotView> _slotViews = new();

        [Header("인벤토리 탭 버튼")]
        [SerializeField] private Button          _weaponTabButton;
        [SerializeField] private TextMeshProUGUI _weaponTabText;
        [SerializeField] private Button          _armorTabButton;
        [SerializeField] private TextMeshProUGUI _armorTabText;
        [SerializeField] private Button          _accessoryTabButton;
        [SerializeField] private TextMeshProUGUI _accessoryTabText;

        [Header("인벤토리")]
        [SerializeField] private Transform        _inventoryContent;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;

        // ── 상수 ─────────────────────────────────────────────────────────────

        private static readonly Color COLOR_TAB_ACTIVE   = new Color(0.9f, 0.8f, 0.2f, 1f);
        private static readonly Color COLOR_TAB_INACTIVE = new Color(0.5f, 0.5f, 0.5f, 1f);

        // ── Private Fields ───────────────────────────────────────────────────

        private readonly List<InventoryItemView> _inventoryItems = new();
        private InventoryTab _currentTab = InventoryTab.Weapon;

        // ── 인벤토리 탭 정의 ────────────────────────────────────────────────

        /// <summary>인벤토리 탭 분류.</summary>
        public enum InventoryTab { Weapon, Armor, Accessory }

        // ── IEquipmentPanelView 이벤트 ──────────────────────────────────────

        /// <inheritdoc/>
        public event Action<EquipmentSlotType> OnSlotClicked;

        /// <inheritdoc/>
        public event Action<EquipmentInstance> OnInventoryItemClicked;

        /// <inheritdoc/>
        public event Action<EquipmentSlotType, int> OnRuneSocketClicked;

        /// <summary>인벤토리 탭 전환 이벤트.</summary>
        public event Action<InventoryTab> OnInventoryTabChanged;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            InitSlotViews();
            InitTabButtons();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>
        /// 런타임 생성 시 SerializeField 참조를 외부에서 주입한다 (LobbyHudSetup 에서 호출).
        /// </summary>
        public void InitReferences(List<EquipmentSlotView> slots, Transform inventoryContent,
                                   InventoryItemView inventoryItemPrefab)
        {
            _slotViews           = slots ?? new List<EquipmentSlotView>();
            _inventoryContent    = inventoryContent;
            _inventoryItemPrefab = inventoryItemPrefab;
        }

        /// <summary>탭 버튼 참조를 주입한다 (LobbyHudSetup 에서 호출).</summary>
        public void InitTabReferences(
            Button          weaponTabButton,
            TextMeshProUGUI weaponTabText,
            Button          armorTabButton,
            TextMeshProUGUI armorTabText,
            Button          accessoryTabButton,
            TextMeshProUGUI accessoryTabText)
        {
            _weaponTabButton    = weaponTabButton;
            _weaponTabText      = weaponTabText;
            _armorTabButton     = armorTabButton;
            _armorTabText       = armorTabText;
            _accessoryTabButton = accessoryTabButton;
            _accessoryTabText   = accessoryTabText;
            InitTabButtons();
        }

        // ── IEquipmentPanelView 메서드 ──────────────────────────────────────

        /// <inheritdoc/>
        public void RefreshSlots(IReadOnlyDictionary<EquipmentSlotType, EquipmentInstance> equipped)
        {
            var slotTypes = (EquipmentSlotType[])System.Enum.GetValues(typeof(EquipmentSlotType));
            for (int i = 0; i < _slotViews.Count && i < slotTypes.Length; i++)
            {
                equipped.TryGetValue(slotTypes[i], out var instance);
                _slotViews[i].Refresh(instance);
            }
        }

        /// <inheritdoc/>
        public void RefreshInventory(IReadOnlyList<EquipmentInstance> inventory)
        {
            // 현재 탭에 맞게 필터링
            var filtered = FilterByTab(inventory, _currentTab);

            // 기존 뷰 비활성화 후 재사용
            foreach (var item in _inventoryItems)
                item.gameObject.SetActive(false);

            for (int i = 0; i < filtered.Count; i++)
            {
                InventoryItemView view;
                if (i < _inventoryItems.Count)
                {
                    view = _inventoryItems[i];
                }
                else
                {
                    view = Instantiate(_inventoryItemPrefab, _inventoryContent);
                    view.OnClicked += HandleInventoryItemClicked;
                    _inventoryItems.Add(view);
                }
                view.gameObject.SetActive(true);
                view.Refresh(filtered[i]);
            }
        }

        /// <inheritdoc/>
        public void SetSlotSelected(EquipmentSlotType slot, bool selected)
        {
            var slotView = FindSlotView(slot);
            slotView?.SetSelected(selected);
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        // ── Private 탭 메서드 ────────────────────────────────────────────────

        private void InitTabButtons()
        {
            if (_weaponTabButton != null)
            {
                _weaponTabButton.onClick.RemoveAllListeners();
                _weaponTabButton.onClick.AddListener(() => HandleTabClicked(InventoryTab.Weapon));
            }
            if (_armorTabButton != null)
            {
                _armorTabButton.onClick.RemoveAllListeners();
                _armorTabButton.onClick.AddListener(() => HandleTabClicked(InventoryTab.Armor));
            }
            if (_accessoryTabButton != null)
            {
                _accessoryTabButton.onClick.RemoveAllListeners();
                _accessoryTabButton.onClick.AddListener(() => HandleTabClicked(InventoryTab.Accessory));
            }
            RefreshTabHighlight();
        }

        private void HandleTabClicked(InventoryTab tab)
        {
            _currentTab = tab;
            RefreshTabHighlight();
            OnInventoryTabChanged?.Invoke(tab);
        }

        private void RefreshTabHighlight()
        {
            if (_weaponTabText    != null) _weaponTabText.color    = _currentTab == InventoryTab.Weapon    ? COLOR_TAB_ACTIVE : COLOR_TAB_INACTIVE;
            if (_armorTabText     != null) _armorTabText.color     = _currentTab == InventoryTab.Armor     ? COLOR_TAB_ACTIVE : COLOR_TAB_INACTIVE;
            if (_accessoryTabText != null) _accessoryTabText.color = _currentTab == InventoryTab.Accessory ? COLOR_TAB_ACTIVE : COLOR_TAB_INACTIVE;
        }

        private static List<EquipmentInstance> FilterByTab(IReadOnlyList<EquipmentInstance> inventory, InventoryTab tab)
        {
            var result = new List<EquipmentInstance>();
            foreach (var inst in inventory)
            {
                if (inst == null || inst.Data == null) continue;
                if (BelongsToTab(inst.Data.SlotType, tab))
                    result.Add(inst);
            }
            return result;
        }

        private static bool BelongsToTab(EquipmentSlotType slot, InventoryTab tab)
        {
            return tab switch
            {
                InventoryTab.Weapon    => slot == EquipmentSlotType.Weapon,
                InventoryTab.Armor     => slot == EquipmentSlotType.Hat
                                       || slot == EquipmentSlotType.Top
                                       || slot == EquipmentSlotType.Bottom
                                       || slot == EquipmentSlotType.Shoes
                                       || slot == EquipmentSlotType.Gloves,
                InventoryTab.Accessory => slot == EquipmentSlotType.Necklace
                                       || slot == EquipmentSlotType.Ring,
                _                      => false
            };
        }

        // ── Private Slot 메서드 ──────────────────────────────────────────────

        private void InitSlotViews()
        {
            var slotTypes = (EquipmentSlotType[])System.Enum.GetValues(typeof(EquipmentSlotType));
            for (int i = 0; i < _slotViews.Count && i < slotTypes.Length; i++)
            {
                _slotViews[i].Init(slotTypes[i]);
                _slotViews[i].OnClicked += HandleSlotClicked;
            }
        }

        private EquipmentSlotView FindSlotView(EquipmentSlotType slot)
        {
            var slotTypes = (EquipmentSlotType[])System.Enum.GetValues(typeof(EquipmentSlotType));
            for (int i = 0; i < _slotViews.Count && i < slotTypes.Length; i++)
            {
                if (slotTypes[i] == slot)
                    return _slotViews[i];
            }
            return null;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleSlotClicked(EquipmentSlotType slot)
        {
            OnSlotClicked?.Invoke(slot);
        }

        private void HandleInventoryItemClicked(EquipmentInstance instance)
        {
            OnInventoryItemClicked?.Invoke(instance);
        }
    }
}
