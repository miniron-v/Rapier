using Game.Data.Equipment;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 아이템 상세 팝업 Presenter.
    /// EquipmentManager(Model) 와 ItemDetailPopupView(View) 사이를 중재한다.
    ///
    /// [장착 규칙]
    ///   - 이미 이 캐릭터에 장착 중 → "해제" (Unequip)
    ///   - 다른 캐릭터에 장착 중 → "장착" (타 캐릭터 자동 해제, 룬 유지)
    ///   - 미장착 → "장착"
    ///
    /// [강화 규칙 (Phase 25-C)]
    ///   - 강화 버튼 클릭 → EnhanceModalPresenter.Show 호출
    ///   - disableActions 모드 또는 최대 단계 도달 시 강화 버튼 비활성
    ///
    /// [이벤트 구독/해제]
    ///   OnEnable / OnDisable 쌍으로 View 이벤트를 구독/해제한다.
    /// </summary>
    public class ItemDetailPopupPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private ItemDetailPopupView         _view;
        [SerializeField] private RuneInventoryPopupPresenter _runeInventoryPresenter;
        [SerializeField] private EnhanceModalPresenter       _enhanceModalPresenter;

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentManager   _manager;
        private string             _characterId;
        private EquipmentInstance  _currentInstance;
        private EquipmentSlotType  _currentSlot;
        private bool               _disableActions;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 View + 룬 인벤토리 팝업 + 강화 모달 참조를 주입한다.</summary>
        public void InitReferences(
            ItemDetailPopupView view,
            RuneInventoryPopupPresenter runeInventoryPresenter = null,
            EnhanceModalPresenter enhanceModalPresenter = null)
        {
            _view                   = view;
            _runeInventoryPresenter = runeInventoryPresenter;
            _enhanceModalPresenter  = enhanceModalPresenter;
        }

        /// <summary>수동 DI. EquipmentPanelPresenter 에서 호출한다.</summary>
        public void Init(EquipmentManager manager, string characterId)
        {
            _manager     = manager;
            _characterId = characterId;

            // EnhanceModalPresenter 에도 manager 주입
            _enhanceModalPresenter?.Init(manager);
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnEquipClicked      += HandleEquipClicked;
            _view.OnEnhanceClicked    += HandleEnhanceClicked;
            _view.OnCloseClicked      += HandleCloseClicked;
            _view.OnRuneSocketClicked += HandleRuneSocketClicked;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnEquipClicked      -= HandleEquipClicked;
            _view.OnEnhanceClicked    -= HandleEnhanceClicked;
            _view.OnCloseClicked      -= HandleCloseClicked;
            _view.OnRuneSocketClicked -= HandleRuneSocketClicked;
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// 팝업을 열어 아이템 상세를 표시한다.
        /// </summary>
        /// <param name="instance">표시할 장비 인스턴스.</param>
        /// <param name="slot">연관 슬롯 타입 (장착 시 사용).</param>
        /// <param name="disableActions">
        /// true 이면 분해 모드 — 장착/강화 버튼 모두 비활성, 닫기만 활성.
        /// 25-B 에서 분해 모드 진입 시 이 플래그를 true 로 호출한다.
        /// </param>
        public void Show(EquipmentInstance instance, EquipmentSlotType slot, bool disableActions = false)
        {
            if (_view == null || instance == null) return;

            _currentInstance = instance;
            _currentSlot     = slot;
            _disableActions  = disableActions;

            bool isEquipped    = IsEquippedByCurrentChar(instance);
            bool equippedOther = !isEquipped && IsEquippedByAnyChar(instance);

            _view.SetData(instance, isEquipped, equippedOther, disableActions);
            _view.transform.SetAsLastSibling();
            _view.SetVisible(true);
        }

        /// <summary>팝업을 닫는다.</summary>
        public void Hide()
        {
            _view?.SetVisible(false);
            _currentInstance = null;
        }

        /// <summary>
        /// 강화 완료 후 서브스탯 펄스 힌트를 받아 View 에 전달한다.
        /// EnhanceModalPresenter 또는 외부 시스템에서 호출할 수 있다.
        /// </summary>
        public void HintSubStatPulse(int subStatIndex)
        {
            _view?.PulseSubStatHighlight(subStatIndex);
        }

        /// <summary>
        /// 현재 표시 중인 아이템의 View 데이터를 재갱신한다.
        /// 강화 완료 후 EnhanceModalPresenter 에서 닫힐 때 호출한다.
        /// </summary>
        public void RefreshCurrentItem()
        {
            if (_view == null || _currentInstance == null) return;
            bool isEquipped    = IsEquippedByCurrentChar(_currentInstance);
            bool equippedOther = !isEquipped && IsEquippedByAnyChar(_currentInstance);
            _view.SetData(_currentInstance, isEquipped, equippedOther, _disableActions);
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleEquipClicked()
        {
            if (_manager == null || _currentInstance == null) return;
            if (_disableActions) return;

            bool isEquipped = IsEquippedByCurrentChar(_currentInstance);
            if (isEquipped)
            {
                // 해제 후 즉시 창 닫기 (장착과 대칭)
                _manager.Unequip(_characterId, _currentSlot);
                Debug.Log($"[ItemDetailPresenter] 해제: {_currentInstance.Data.ItemName} 슬롯={_currentSlot}");
                Hide();
            }
            else
            {
                // 장착 (다른 캐릭터에 장착 중이면 EquipmentManager 내부에서 자동 해제됨)
                _manager.Equip(_characterId, _currentInstance);
                Debug.Log($"[ItemDetailPresenter] 장착: {_currentInstance.Data.ItemName} → {_characterId}");
                // 장착 즉시 창 닫기
                Hide();
            }
        }

        private void HandleEnhanceClicked()
        {
            if (_manager == null || _currentInstance == null) return;
            if (_disableActions) return;
            if (_enhanceModalPresenter == null) return;

            // 최대 강화 단계는 진입 자체를 막음 (View 에서 버튼 비활성화되어 있어야 하지만 방어 처리)
            if (_currentInstance.EnhanceLevel >= _currentInstance.MaxEnhanceLevel) return;

            _enhanceModalPresenter.Show(_currentInstance);
        }

        private void HandleCloseClicked()
        {
            Hide();
        }

        private void HandleRuneSocketClicked(int socketIndex)
        {
            // 룬 소켓 클릭 → 룬 인벤토리 팝업 위임 (소켓 맥락 전달)
            // 아이템 상세 팝업은 열린 채로 유지 (닫지 않음 — 뒤에서 보임)
            if (_runeInventoryPresenter != null && _currentInstance != null)
            {
                _runeInventoryPresenter.Show(_currentInstance, _currentSlot, socketIndex);
            }
        }

        // ── Private 헬퍼 ─────────────────────────────────────────────────────

        private bool IsEquippedByCurrentChar(EquipmentInstance instance)
        {
            if (_manager == null) return false;
            var equipped = _manager.GetEquipped(_characterId, instance.Data.SlotType);
            return equipped == instance;
        }

        private bool IsEquippedByAnyChar(EquipmentInstance instance)
        {
            // EquipmentManager 의 공개 API 만으로 판단 — 장착 세트를 순회
            // 현재 캐릭터 포함 모든 캐릭터에서 찾음
            // 구현 캐릭터: Rapier, Assassin
            if (_manager == null) return false;
            foreach (var cid in new[] { "Rapier", "Assassin" })
            {
                var set = _manager.GetCharacterSet(cid);
                foreach (var kv in set.GetAllEquipped())
                {
                    if (kv.Value == instance) return true;
                }
            }
            return false;
        }
    }
}
