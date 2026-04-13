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
    /// [이벤트 구독/해제]
    ///   OnEnable / OnDisable 쌍으로 View 이벤트를 구독/해제한다.
    /// </summary>
    public class ItemDetailPopupPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private ItemDetailPopupView         _view;
        [SerializeField] private RuneInventoryPopupPresenter _runeInventoryPresenter;

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentManager   _manager;
        private string             _characterId;
        private EquipmentInstance  _currentInstance;
        private EquipmentSlotType  _currentSlot;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 View + 룬 인벤토리 팝업 참조를 주입한다.</summary>
        public void InitReferences(ItemDetailPopupView view, RuneInventoryPopupPresenter runeInventoryPresenter = null)
        {
            _view                   = view;
            _runeInventoryPresenter = runeInventoryPresenter;
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
            if (_view == null) return;
            _view.OnEquipClicked      += HandleEquipClicked;
            _view.OnCloseClicked      += HandleCloseClicked;
            _view.OnRuneSocketClicked += HandleRuneSocketClicked;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnEquipClicked      -= HandleEquipClicked;
            _view.OnCloseClicked      -= HandleCloseClicked;
            _view.OnRuneSocketClicked -= HandleRuneSocketClicked;
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// 팝업을 열어 아이템 상세를 표시한다.
        /// </summary>
        /// <param name="instance">표시할 장비 인스턴스.</param>
        /// <param name="slot">연관 슬롯 타입 (장착 시 사용).</param>
        public void Show(EquipmentInstance instance, EquipmentSlotType slot)
        {
            if (_view == null || instance == null) return;

            _currentInstance = instance;
            _currentSlot     = slot;

            bool isEquipped    = IsEquippedByCurrentChar(instance);
            bool equippedOther = !isEquipped && IsEquippedByAnyChar(instance);

            _view.SetData(instance, isEquipped, equippedOther);
            _view.SetVisible(true);
        }

        /// <summary>팝업을 닫는다.</summary>
        public void Hide()
        {
            _view?.SetVisible(false);
            _currentInstance = null;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleEquipClicked()
        {
            if (_manager == null || _currentInstance == null) return;

            bool isEquipped = IsEquippedByCurrentChar(_currentInstance);
            if (isEquipped)
            {
                // 해제
                _manager.Unequip(_characterId, _currentSlot);
                Debug.Log($"[ItemDetailPresenter] 해제: {_currentInstance.Data.ItemName} 슬롯={_currentSlot}");
            }
            else
            {
                // 장착 (다른 캐릭터에 장착 중이면 EquipmentManager 내부에서 자동 해제됨)
                _manager.Equip(_characterId, _currentInstance);
                Debug.Log($"[ItemDetailPresenter] 장착: {_currentInstance.Data.ItemName} → {_characterId}");
            }

            // 버튼 상태 갱신
            bool nowEquipped = IsEquippedByCurrentChar(_currentInstance);
            bool nowOther    = !nowEquipped && IsEquippedByAnyChar(_currentInstance);
            _view.SetData(_currentInstance, nowEquipped, nowOther);
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
