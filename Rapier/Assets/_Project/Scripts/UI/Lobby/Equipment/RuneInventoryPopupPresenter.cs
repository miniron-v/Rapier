using System.Collections.Generic;
using Game.Data.Equipment;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 룬 인벤토리 팝업 Presenter.
    /// EquipmentManager(Model) 와 RuneInventoryPopupView(View) 사이를 중재한다.
    ///
    /// [동작]
    ///   - 소켓 맥락(장비 인스턴스 + 슬롯 + 소켓 인덱스)을 보유
    ///   - 캐릭터별 탭 전환 → RuneInventory 필터링하여 목록 표시
    ///   - targetCharacterId 가 비어있는 룬 → 모든 탭에서 공통 표시
    ///   - 룬 클릭 → RuneDetailPopupPresenter 위임
    ///   - 해제 버튼 → 소켓 맥락 기반 UnequipRune
    ///
    /// [이벤트 구독/해제]
    ///   OnEnable / OnDisable 쌍으로 View 이벤트를 구독/해제한다.
    /// </summary>
    public class RuneInventoryPopupPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private RuneInventoryPopupView  _view;
        [SerializeField] private RuneDetailPopupPresenter _runeDetailPresenter;

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentManager  _manager;
        private string            _characterId;

        // 현재 열린 소켓 맥락
        private EquipmentInstance _contextEquipment;
        private EquipmentSlotType _contextSlot;
        private int               _contextSocketIndex;
        private string            _currentTabId = "Rapier";

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 View 참조를 주입한다.</summary>
        public void InitReferences(RuneInventoryPopupView view, RuneDetailPopupPresenter runeDetailPresenter)
        {
            _view                = view;
            _runeDetailPresenter = runeDetailPresenter;
        }

        /// <summary>수동 DI. EquipmentPanelPresenter 에서 호출한다.</summary>
        public void Init(EquipmentManager manager, string characterId)
        {
            _manager      = manager;
            _characterId  = characterId;
            _currentTabId = characterId;

            _runeDetailPresenter?.Init(manager, characterId);
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnTabSelected   += HandleTabSelected;
            _view.OnRuneClicked   += HandleRuneClicked;
            _view.OnUnequipClicked += HandleUnequipClicked;
            _view.OnCloseClicked  += HandleCloseClicked;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnTabSelected   -= HandleTabSelected;
            _view.OnRuneClicked   -= HandleRuneClicked;
            _view.OnUnequipClicked -= HandleUnequipClicked;
            _view.OnCloseClicked  -= HandleCloseClicked;
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>
        /// 소켓 맥락을 설정하고 팝업을 연다.
        /// </summary>
        /// <param name="equipment">소켓을 가진 장비 인스턴스 (null 허용).</param>
        /// <param name="slot">장비 슬롯 타입.</param>
        /// <param name="socketIndex">소켓 인덱스.</param>
        public void Show(EquipmentInstance equipment, EquipmentSlotType slot, int socketIndex)
        {
            if (_view == null) return;

            _contextEquipment   = equipment;
            _contextSlot        = slot;
            _contextSocketIndex = socketIndex;

            // 탭 초기화 (현재 캐릭터 탭으로)
            _currentTabId = _characterId;
            _view.SetActiveTab(_currentTabId);

            RefreshRuneList();
            RefreshUnequipButton();

            _view.transform.SetAsLastSibling();
            _view.SetVisible(true);
        }

        /// <summary>팝업을 닫는다.</summary>
        public void Hide()
        {
            _view?.SetVisible(false);
            _contextEquipment = null;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleTabSelected(string characterId)
        {
            _currentTabId = characterId;
            _view.SetActiveTab(characterId);
            RefreshRuneList();
        }

        private void HandleRuneClicked(RuneItemData rune)
        {
            if (rune == null) return;
            // 룬 상세 팝업으로 위임 (소켓 맥락 전달)
            _runeDetailPresenter?.Show(rune, _contextEquipment, _contextSlot, _contextSocketIndex, _characterId);
        }

        private void HandleUnequipClicked()
        {
            if (_manager == null || _contextEquipment == null) return;

            // 현재 소켓에 룬이 장착되어 있을 때만 해제
            var rune = GetCurrentSocketRune();
            if (rune == null) return;

            _manager.UnequipRune(_characterId, _contextSlot, _contextSocketIndex);
            Debug.Log($"[RuneInventoryPresenter] 룬 해제: {rune.RuneName} 소켓={_contextSocketIndex}");

            RefreshUnequipButton();
        }

        private void HandleCloseClicked()
        {
            Hide();
        }

        // ── Private 헬퍼 ─────────────────────────────────────────────────────

        private void RefreshRuneList()
        {
            if (_manager == null) return;

            var allRunes   = _manager.RuneInventory;
            var filtered   = new List<RuneItemData>();
            foreach (var rune in allRunes)
            {
                if (rune == null) continue;
                // targetCharacterId 가 비어있으면 모든 탭에서 표시
                if (string.IsNullOrEmpty(rune.TargetCharacterId)
                    || rune.TargetCharacterId == _currentTabId)
                {
                    filtered.Add(rune);
                }
            }

            _view.RefreshRuneList(filtered);
        }

        private void RefreshUnequipButton()
        {
            var rune = GetCurrentSocketRune();
            _view.SetUnequipInteractable(rune != null);
        }

        private RuneItemData GetCurrentSocketRune()
        {
            if (_contextEquipment == null) return null;
            var runes = _contextEquipment.EquippedRunes;
            if (runes == null || _contextSocketIndex >= runes.Length) return null;
            return runes[_contextSocketIndex];
        }
    }
}
