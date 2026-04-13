using Game.Data.Equipment;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 룬 상세 팝업 Presenter.
    /// EquipmentManager(Model) 와 RuneDetailPopupView(View) 사이를 중재한다.
    ///
    /// [동작]
    ///   - 소켓 맥락(장비 + 슬롯 + 소켓 인덱스) 보유
    ///   - 장착 버튼: 현재 소켓 맥락에 이 룬을 장착
    ///   - 닫기: 팝업 닫기
    ///
    /// [이벤트 구독/해제]
    ///   OnEnable / OnDisable 쌍으로 View 이벤트를 구독/해제한다.
    /// </summary>
    public class RuneDetailPopupPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private RuneDetailPopupView _view;

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentManager  _manager;
        private string            _characterId;

        // 소켓 맥락
        private RuneItemData      _currentRune;
        private EquipmentInstance _contextEquipment;
        private EquipmentSlotType _contextSlot;
        private int               _contextSocketIndex;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 View 참조를 주입한다.</summary>
        public void InitReferences(RuneDetailPopupView view)
        {
            _view = view;
        }

        /// <summary>수동 DI. RuneInventoryPopupPresenter 에서 호출한다.</summary>
        public void Init(EquipmentManager manager, string characterId)
        {
            _manager     = manager;
            _characterId = characterId;
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnEquipClicked += HandleEquipClicked;
            _view.OnCloseClicked += HandleCloseClicked;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnEquipClicked -= HandleEquipClicked;
            _view.OnCloseClicked -= HandleCloseClicked;
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// 소켓 맥락을 설정하고 룬 상세 팝업을 연다.
        /// </summary>
        public void Show(
            RuneItemData      rune,
            EquipmentInstance contextEquipment,
            EquipmentSlotType contextSlot,
            int               contextSocketIndex,
            string            characterId)
        {
            if (_view == null || rune == null) return;

            _currentRune        = rune;
            _contextEquipment   = contextEquipment;
            _contextSlot        = contextSlot;
            _contextSocketIndex = contextSocketIndex;
            _characterId        = characterId;

            _view.SetData(rune);

            // 장착 버튼 상태
            bool canEquip = contextEquipment != null;
            _view.SetEquipButtonState("장착", canEquip);

            _view.transform.SetAsLastSibling();
            _view.SetVisible(true);
        }

        /// <summary>팝업을 닫는다.</summary>
        public void Hide()
        {
            _view?.SetVisible(false);
            _currentRune = null;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleEquipClicked()
        {
            if (_manager == null || _currentRune == null || _contextEquipment == null) return;

            bool ok = _manager.EquipRune(_characterId, _contextSlot, _contextSocketIndex, _currentRune);
            if (ok)
            {
                Debug.Log($"[RuneDetailPresenter] 룬 장착: {_currentRune.RuneName} → {_characterId} 슬롯={_contextSlot} 소켓={_contextSocketIndex}");
                Hide();
            }
            else
            {
                Debug.LogWarning($"[RuneDetailPresenter] 룬 장착 실패: {_currentRune.RuneName}");
            }
        }

        private void HandleCloseClicked()
        {
            Hide();
        }
    }
}
