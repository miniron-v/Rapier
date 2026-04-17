using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Equipment;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// EquipmentActionBar Presenter.
    /// 분해 모드 진입/종료, 일괄 선택 드롭다운, 분해 실행 흐름을 담당한다.
    ///
    /// 이벤트 구독/해제: OnEnable / OnDisable 쌍.
    /// </summary>
    public class EquipmentActionBarPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private EquipmentActionBarView  _view;
        [SerializeField] private BulkSelectDropdownView  _dropdownView;
        [SerializeField] private DismantleResultModalPresenter _resultModalPresenter;

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentManager   _manager;
        private string             _characterId;

        private bool               _isDismantleMode;
        private readonly HashSet<EquipmentInstance> _selectedInstances = new();

        // 현재 인벤토리 탭 (EquipmentPanelView 에서 전달)
        private EquipmentPanelView.InventoryTab _currentTab = EquipmentPanelView.InventoryTab.Weapon;

        // 현재 인벤토리 전체 목록 (필터링용)
        private IReadOnlyList<EquipmentInstance> _inventory = Array.Empty<EquipmentInstance>();

        // 현재 장착 상태 (장착 여부 판단용)
        private IReadOnlyDictionary<EquipmentSlotType, EquipmentInstance> _equipped
            = new Dictionary<EquipmentSlotType, EquipmentInstance>();

        // ── 이벤트 (외부 구독용) ────────────────────────────────────────────

        /// <summary>분해 모드 진입/종료 이벤트. EquipmentPanelPresenter 가 구독한다.</summary>
        public event Action<bool> OnDismantleModeChanged;

        /// <summary>분해 선택 세트 변경 이벤트. InventoryItemView 시각화 업데이트 용도.</summary>
        public event Action<IReadOnlyCollection<EquipmentInstance>> OnSelectionChanged;

        // ── Properties ──────────────────────────────────────────────────────

        /// <summary>현재 분해 모드 여부.</summary>
        public bool IsDismantleMode => _isDismantleMode;

        /// <summary>현재 선택된 인스턴스 집합 (읽기 전용).</summary>
        public IReadOnlyCollection<EquipmentInstance> SelectedInstances => _selectedInstances;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>런타임 생성 시 View 참조를 주입한다 (LobbyHudSetup 호출).</summary>
        public void InitReferences(
            EquipmentActionBarView       view,
            BulkSelectDropdownView       dropdownView,
            DismantleResultModalPresenter resultModalPresenter)
        {
            _view                 = view;
            _dropdownView         = dropdownView;
            _resultModalPresenter = resultModalPresenter;
        }

        /// <summary>수동 DI. EquipmentPanelPresenter 에서 호출한다.</summary>
        public void Init(EquipmentManager manager, string characterId)
        {
            _manager     = manager;
            _characterId = characterId;
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            SubscribeViewEvents();
            SubscribeManagerEvents();
            RefreshDustDisplay();
        }

        private void OnDisable()
        {
            UnsubscribeViewEvents();
            UnsubscribeManagerEvents();

            // 패널 비활성 시 분해 모드 자동 종료 (시나리오 g)
            if (_isDismantleMode)
                ExitDismantleMode();
        }

        // ── Public 메서드 ────────────────────────────────────────────────────

        /// <summary>현재 인벤토리 목록 및 장착 상태를 갱신한다. EquipmentPanelPresenter 에서 호출.</summary>
        public void UpdateInventoryContext(
            IReadOnlyList<EquipmentInstance>                       inventory,
            IReadOnlyDictionary<EquipmentSlotType, EquipmentInstance> equipped)
        {
            _inventory = inventory ?? Array.Empty<EquipmentInstance>();
            _equipped  = equipped  ?? new Dictionary<EquipmentSlotType, EquipmentInstance>();

            // 분해 모드 중 선택 목록에서 이제 존재하지 않는 인스턴스 정리
            _selectedInstances.RemoveWhere(inst => !_inventory.Contains(inst));
            RefreshDismantleButtonState();
        }

        /// <summary>현재 탭 변경 알림. EquipmentPanelPresenter 에서 호출.</summary>
        public void NotifyTabChanged(EquipmentPanelView.InventoryTab tab)
        {
            _currentTab = tab;
        }

        /// <summary>분해 선택 토글. InventoryItemView 탭 시 EquipmentPanelPresenter 를 통해 호출.</summary>
        public void ToggleSelection(EquipmentInstance instance)
        {
            if (instance == null) return;

            // 장착 중이면 무반응
            if (IsEquipped(instance)) return;

            if (_selectedInstances.Contains(instance))
                _selectedInstances.Remove(instance);
            else
                _selectedInstances.Add(instance);

            RefreshDismantleButtonState();
            OnSelectionChanged?.Invoke(_selectedInstances);
        }

        /// <summary>선택 초기화.</summary>
        public void ClearSelection()
        {
            _selectedInstances.Clear();
            RefreshDismantleButtonState();
            OnSelectionChanged?.Invoke(_selectedInstances);
        }

        // ── Private 분해 모드 ─────────────────────────────────────────────────

        private void EnterDismantleMode()
        {
            _isDismantleMode = true;
            _selectedInstances.Clear();
            _view?.SetDismantleMode(true);
            RefreshDismantleButtonState();
            OnDismantleModeChanged?.Invoke(true);
            OnSelectionChanged?.Invoke(_selectedInstances);
        }

        private void ExitDismantleMode()
        {
            _isDismantleMode = false;
            _selectedInstances.Clear();
            _view?.SetDismantleMode(false);
            _dropdownView?.Hide();
            RefreshDismantleButtonState();
            OnDismantleModeChanged?.Invoke(false);
            OnSelectionChanged?.Invoke(_selectedInstances);
        }

        private void RefreshDustDisplay()
        {
            if (_manager != null)
                _view?.SetDustText(_manager.Dust);
        }

        private void RefreshDismantleButtonState()
        {
            _view?.SetDosDismantleInteractable(_isDismantleMode && _selectedInstances.Count > 0);
        }

        private bool IsEquipped(EquipmentInstance instance)
        {
            if (instance == null || _equipped == null) return false;
            foreach (var kv in _equipped)
                if (kv.Value == instance) return true;
            return false;
        }

        // ── 일괄 선택 ─────────────────────────────────────────────────────────

        private void HandleBulkSelectClicked()
        {
            // 현재 탭의 등급 4종 항목을 드롭다운으로 표시
            _dropdownView?.Show(_currentTab, HandleBulkGradeSelected);
        }

        private void HandleBulkGradeSelected(EquipmentGrade grade)
        {
            if (_inventory == null) return;

            // 현재 탭의 미장착 + 등급 일치 인스턴스를 기존 선택에 추가
            foreach (var inst in _inventory)
            {
                if (inst == null || inst.Data == null)                   continue;
                if (inst.Grade > grade)                                    continue;
                if (!BelongsToCurrentTab(inst.Data.SlotType))            continue;
                if (IsEquipped(inst))                                     continue;
                _selectedInstances.Add(inst);
            }

            RefreshDismantleButtonState();
            OnSelectionChanged?.Invoke(_selectedInstances);
            _dropdownView?.Hide();
        }

        private bool BelongsToCurrentTab(EquipmentSlotType slot)
        {
            return _currentTab switch
            {
                EquipmentPanelView.InventoryTab.Weapon    => slot == EquipmentSlotType.Weapon,
                EquipmentPanelView.InventoryTab.Armor     => slot == EquipmentSlotType.Hat
                                                          || slot == EquipmentSlotType.Top
                                                          || slot == EquipmentSlotType.Bottom
                                                          || slot == EquipmentSlotType.Shoes
                                                          || slot == EquipmentSlotType.Gloves,
                EquipmentPanelView.InventoryTab.Accessory => slot == EquipmentSlotType.Necklace
                                                          || slot == EquipmentSlotType.Ring,
                _ => false
            };
        }

        // ── 분해 실행 ─────────────────────────────────────────────────────────

        private void HandleDosDismantleClicked()
        {
            if (_manager == null || _selectedInstances.Count == 0) return;

            int totalDust = _manager.Dismantle(_selectedInstances);
            _selectedInstances.Clear();
            RefreshDismantleButtonState();

            // 결과 모달 표시
            _resultModalPresenter?.Show(totalDust, HandleResultModalClosed);
        }

        private void HandleResultModalClosed()
        {
            // 닫기 → 분해 모드 종료
            ExitDismantleMode();
        }

        // ── View 이벤트 구독/해제 ────────────────────────────────────────────

        private void SubscribeViewEvents()
        {
            if (_view == null) return;
            _view.OnDismantleEnterClicked += HandleDismantleEnterClicked;
            _view.OnBackClicked           += HandleBackClicked;
            _view.OnBulkSelectClicked     += HandleBulkSelectClicked;
            _view.OnDosDismantleClicked   += HandleDosDismantleClicked;
        }

        private void UnsubscribeViewEvents()
        {
            if (_view == null) return;
            _view.OnDismantleEnterClicked -= HandleDismantleEnterClicked;
            _view.OnBackClicked           -= HandleBackClicked;
            _view.OnBulkSelectClicked     -= HandleBulkSelectClicked;
            _view.OnDosDismantleClicked   -= HandleDosDismantleClicked;
        }

        private void SubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnDustChanged += HandleDustChanged;
        }

        private void UnsubscribeManagerEvents()
        {
            if (_manager == null) return;
            _manager.OnDustChanged -= HandleDustChanged;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleDismantleEnterClicked() => EnterDismantleMode();
        private void HandleBackClicked()           => ExitDismantleMode();
        private void HandleDustChanged(int newAmount) => _view?.SetDustText(newAmount);
    }
}
