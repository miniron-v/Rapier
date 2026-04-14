using Game.Characters;
using Game.Core;
using Game.Data.Equipment;
using Game.Data.Save;
using Game.UI.Lobby.Equipment;
using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 캐릭터 정보 패널 Presenter.
    ///
    /// [Phase 23c]
    ///   - CharacterInfoPanelView (일러스트 + 변경 버튼 + 좌3/우5 슬롯) 관리
    ///   - CharacterSelectModalPresenter 위임
    ///   - EquipmentPanelPresenter (슬롯 갱신) 연결
    ///   - CharacterTabPresenter.OnTabShown/OnTabHidden 에서 호출됨
    ///
    /// ── 구독/이벤트 매핑 ─────────────────────────────────────────────
    /// | 이벤트                                       | 구독/해제 위치      | 핸들러                       |
    /// |----------------------------------------------|--------------------|-----------------------------|
    /// | CharacterInfoPanelView.OnChangeCharacterClicked | Show/Hide         | HandleChangeClicked         |
    /// | CharacterSelectModalPresenter.OnCharacterSelected | Show/Hide       | HandleModalCharacterSelected |
    /// ─────────────────────────────────────────────────────────────────
    /// </summary>
    public class CharacterInfoPanelPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private CharacterInfoPanelView      _view;
        [SerializeField] private CharacterSelectModalPresenter _modalPresenter;
        [SerializeField] private EquipmentPanelPresenter      _equipmentPanel;

        // ── CharacterStatData 참조 (에디터에서 주입) ─────────────────────────

        [Header("캐릭터 StatData (주입)")]
        [SerializeField] private CharacterStatData _rapierData;
        [SerializeField] private CharacterStatData _assassinData;

        // ── Private Fields ───────────────────────────────────────────────────

        private string _currentCharacterId;
        private bool   _isActive;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 호출.</summary>
        public void InitReferences(
            CharacterInfoPanelView      view,
            CharacterSelectModalPresenter modalPresenter,
            EquipmentPanelPresenter      equipmentPanel,
            CharacterStatData            rapierData,
            CharacterStatData            assassinData)
        {
            _view            = view;
            _modalPresenter  = modalPresenter;
            _equipmentPanel  = equipmentPanel;
            _rapierData      = rapierData;
            _assassinData    = assassinData;
        }

        // ── 탭 진입점 ────────────────────────────────────────────────────────

        /// <summary>CharacterTabPresenter.OnTabShown 에서 호출.</summary>
        public void Show()
        {
            if (_view == null) return;

            _isActive = true;
            _view.gameObject.SetActive(true);

            // 이벤트 구독
            _view.OnChangeCharacterClicked      -= HandleChangeClicked;
            _view.OnChangeCharacterClicked      += HandleChangeClicked;

            if (_modalPresenter != null)
            {
                _modalPresenter.OnCharacterSelected -= HandleModalCharacterSelected;
                _modalPresenter.OnCharacterSelected += HandleModalCharacterSelected;
            }

            // 현재 캐릭터로 초기화
            _currentCharacterId = GetCurrentCharacterId();
            RefreshIllustration(_currentCharacterId);
            ShowEquipmentPanel(_currentCharacterId);
        }

        /// <summary>CharacterTabPresenter.OnTabHidden 에서 호출.</summary>
        public void Hide()
        {
            _isActive = false;

            if (_view != null)
                _view.OnChangeCharacterClicked -= HandleChangeClicked;

            if (_modalPresenter != null)
                _modalPresenter.OnCharacterSelected -= HandleModalCharacterSelected;

            // 모달이 열려 있으면 닫음
            _modalPresenter?.CloseModal();

            if (_view != null)
                _view.gameObject.SetActive(false);
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private void HandleChangeClicked()
        {
            _modalPresenter?.OpenModal(_currentCharacterId);
        }

        private void HandleModalCharacterSelected(string characterId)
        {
            if (characterId == _currentCharacterId) return;

            _currentCharacterId = characterId;

            // SaveData 갱신
            var saveManager = ServiceLocator.TryGet<SaveManager>();
            if (saveManager != null)
            {
                saveManager.Current.lastCharacterId = characterId;
                saveManager.Save();
            }

            RefreshIllustration(characterId);
            ShowEquipmentPanel(characterId);
        }

        private void RefreshIllustration(string characterId)
        {
            if (_view == null) return;
            var data = GetStatData(characterId);
            // illustSprite 우선, 없으면 fallback으로 sprite
            var sprite = data?.illustSprite != null ? data.illustSprite : data?.sprite;
            _view.SetIllustration(sprite);
        }

        private void ShowEquipmentPanel(string characterId)
        {
            if (_equipmentPanel == null) return;
            var manager = ServiceLocator.TryGet<EquipmentManager>();
            if (manager != null)
                _equipmentPanel.Init(manager, characterId);
            _equipmentPanel.Show();
        }

        private CharacterStatData GetStatData(string characterId)
        {
            return characterId switch
            {
                "Rapier"   => _rapierData,
                "Assassin" => _assassinData,
                _          => null
            };
        }

        private string GetCurrentCharacterId()
        {
            var saveManager = ServiceLocator.TryGet<SaveManager>();
            return saveManager?.Current.lastCharacterId ?? "Rapier";
        }
    }
}
