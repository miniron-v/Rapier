using System;
using System.Collections.Generic;
using Game.Data.Equipment;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 장비 패널 전체 View. 8슬롯 그리드 + 인벤토리 탭을 표시한다.
    /// IEquipmentPanelView 구현. 로직 없음.
    /// </summary>
    public class EquipmentPanelView : MonoBehaviour, IEquipmentPanelView
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("8 슬롯 뷰 (순서: Weapon/Hat/Top/Bottom/Shoes/Gloves/Necklace/Ring)")]
        [SerializeField] private List<EquipmentSlotView> _slotViews = new();

        [Header("인벤토리")]
        [SerializeField] private Transform _inventoryContent;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;

        // ── Private Fields ───────────────────────────────────────────────────

        private readonly List<InventoryItemView> _inventoryItems = new();

        // ── IEquipmentPanelView 이벤트 ──────────────────────────────────────

        /// <inheritdoc/>
        public event Action<EquipmentSlotType> OnSlotClicked;

        /// <inheritdoc/>
        public event Action<EquipmentInstance> OnInventoryItemClicked;

        /// <inheritdoc/>
        public event Action<EquipmentSlotType, int> OnRuneSocketClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            InitSlotViews();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>
        /// 런타임 생성 시 SerializeField 참조를 외부에서 주입한다 (LobbyHudSetup 에서 호출).
        /// Awake 보다 먼저 호출되어야 하므로 AddComponent 직후 즉시 호출할 것.
        /// </summary>
        /// <param name="slots">8개 슬롯 뷰 목록 (순서: Weapon/Hat/Top/Bottom/Shoes/Gloves/Necklace/Ring).</param>
        /// <param name="inventoryContent">인벤토리 아이템의 부모 Transform.</param>
        /// <param name="inventoryItemPrefab">인벤토리 아이템 복제 템플릿.</param>
        public void InitReferences(List<EquipmentSlotView> slots, Transform inventoryContent,
                                   InventoryItemView inventoryItemPrefab)
        {
            _slotViews           = slots ?? new List<EquipmentSlotView>();
            _inventoryContent    = inventoryContent;
            _inventoryItemPrefab = inventoryItemPrefab;
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
            // 기존 뷰 비활성화 후 재사용
            foreach (var item in _inventoryItems)
                item.gameObject.SetActive(false);

            for (int i = 0; i < inventory.Count; i++)
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
                view.Refresh(inventory[i]);
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

        // ── Private Methods ──────────────────────────────────────────────────

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
