using Game.Core;
using Game.Data.Equipment;
using Game.Data.Save;
using Game.UI.Lobby.Equipment;
using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 2 — 캐릭터 관리 탭 Presenter.
    ///
    /// [역할]
    ///   - Rapier + Assassin 슬롯 활성화, 나머지 2칸 Coming Soon 상태 설정
    ///   - CharacterTabView.OnCharacterSelected 구독 → lastCharacterId 저장
    ///   - 장비 패널 characterId 동적 변경
    ///
    /// [이벤트 구독/해제]
    ///   OnTabShown : 슬롯 초기화 + 현재 선택 캐릭터 하이라이트 + View 이벤트 구독
    ///   OnTabHidden : View 이벤트 구독 해제
    ///
    /// ── 구독/이벤트 매핑 ─────────────────────────────────────────────
    /// | 이벤트                        | 구독 위치    | 해제 위치    | 핸들러                   |
    /// |-------------------------------|-------------|-------------|--------------------------|
    /// | CharacterTabView.OnCharacterSelected | OnTabShown  | OnTabHidden  | HandleCharacterSelected  |
    /// ────────────────────────────────────────────────────────────────────
    /// </summary>
    public class CharacterTabPresenter : MonoBehaviour
    {
        // LobbyHudSetup(에디터 시점) 에서 InitEquipmentPanel 로 주입 후
        // 씬 저장 시 유지되어야 하므로 [SerializeField] 필수.
        [SerializeField] private EquipmentPanelPresenter _equipmentPanel;

        private CharacterTabView _view;

        /// <summary>LobbyPresenter가 초기화 시 호출한다.</summary>
        public void Init(CharacterTabView view)
        {
            _view = view;
        }

        /// <summary>
        /// 런타임 생성 시 EquipmentPanelPresenter 참조를 주입한다 (LobbyHudSetup 에서 호출).
        /// </summary>
        /// <param name="equipmentPanel">장비 패널 Presenter.</param>
        public void InitEquipmentPanel(EquipmentPanelPresenter equipmentPanel)
        {
            _equipmentPanel = equipmentPanel;
        }

        // ── 탭 전환 진입점 (LobbyPresenter가 호출) ────────────────
        /// <summary>탭이 표시될 때 LobbyPresenter가 호출한다.</summary>
        public void OnTabShown()
        {
            if (_view == null) return;

            // 이벤트 중복 구독 방지
            _view.OnCharacterSelected -= HandleCharacterSelected;
            _view.OnCharacterSelected += HandleCharacterSelected;

            _view.SetupCharacterSlots();

            // 현재 저장된 lastCharacterId 로 하이라이트 초기화
            string currentId = GetCurrentCharacterId();
            _view.SetHighlight(currentId);

            ShowEquipmentPanel(currentId);
        }

        /// <summary>탭이 숨겨질 때 LobbyPresenter가 호출한다.</summary>
        public void OnTabHidden()
        {
            if (_view != null)
                _view.OnCharacterSelected -= HandleCharacterSelected;
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────
        private void HandleCharacterSelected(string characterId)
        {
            // lastCharacterId 저장
            var saveManager = ServiceLocator.TryGet<SaveManager>();
            if (saveManager != null)
            {
                saveManager.Current.lastCharacterId = characterId;
                saveManager.Save();
                Debug.Log($"[CharacterTabPresenter] 캐릭터 선택 → {characterId} 저장 완료.");
            }
            else
            {
                Debug.LogWarning("[CharacterTabPresenter] SaveManager 미등록. lastCharacterId 저장 불가.");
            }

            // 하이라이트 갱신
            _view?.SetHighlight(characterId);

            // 장비 패널 갱신
            ShowEquipmentPanel(characterId);
        }

        // ── Private 메서드 ────────────────────────────────────────
        private string GetCurrentCharacterId()
        {
            var saveManager = ServiceLocator.TryGet<SaveManager>();
            return saveManager?.Current.lastCharacterId ?? "Rapier";
        }

        private void ShowEquipmentPanel(string characterId)
        {
            if (_equipmentPanel == null) return;

            // 캐릭터가 바뀌면 재초기화 (EquipmentPanelPresenter._characterId 갱신 목적)
            var manager = ServiceLocator.TryGet<EquipmentManager>();
            if (manager != null)
                _equipmentPanel.Init(manager, characterId);

            _equipmentPanel.Show();
        }
    }
}
