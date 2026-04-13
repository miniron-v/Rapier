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
    /// [Phase 23c 재설계]
    ///   - 기존 2×2 캐릭터 슬롯 로직 폐기
    ///   - CharacterInfoPanelPresenter 위임 (일러스트 + 변경 버튼 + 좌3/우5 슬롯)
    ///   - 탭 진입 시 CharacterInfoPanelPresenter.Show(), 탭 이탈 시 Hide()
    ///     → 모달 열림 중 다른 탭 이동 시 Hide() 가 모달을 자동 닫는다 (lock/release 짝)
    ///
    /// ── 구독/이벤트 매핑 ─────────────────────────────────────────────────
    /// | 이벤트                              | 구독 위치  | 해제 위치  | 핸들러      |
    /// |-------------------------------------|-----------|-----------|------------|
    /// | (CharacterInfoPanelPresenter 내부)  | Show      | Hide      | (내부 관리) |
    /// ────────────────────────────────────────────────────────────────────
    /// </summary>
    public class CharacterTabPresenter : MonoBehaviour
    {
        // LobbyHudSetup(에디터 시점) 에서 주입 후 씬 저장 시 유지되어야 하므로 [SerializeField] 필수.
        [SerializeField] private CharacterInfoPanelPresenter _infoPanelPresenter;
        [SerializeField] private EquipmentPanelPresenter     _equipmentPanel;

        private CharacterTabView _view;

        /// <summary>LobbyPresenter가 초기화 시 호출한다.</summary>
        public void Init(CharacterTabView view)
        {
            _view = view;
        }

        /// <summary>
        /// 런타임 생성 시 CharacterInfoPanelPresenter 참조를 주입한다 (LobbyHudSetup 에서 호출).
        /// </summary>
        public void InitInfoPanel(CharacterInfoPanelPresenter infoPanelPresenter)
        {
            _infoPanelPresenter = infoPanelPresenter;
        }

        /// <summary>
        /// 런타임 생성 시 EquipmentPanelPresenter 참조를 주입한다 (하위 호환, LobbyHudSetup 에서 호출).
        /// </summary>
        public void InitEquipmentPanel(EquipmentPanelPresenter equipmentPanel)
        {
            _equipmentPanel = equipmentPanel;
        }

        // ── 탭 전환 진입점 (LobbyPresenter가 호출) ────────────────

        /// <summary>탭이 표시될 때 LobbyPresenter가 호출한다.</summary>
        public void OnTabShown()
        {
            _infoPanelPresenter?.Show();
        }

        /// <summary>탭이 숨겨질 때 LobbyPresenter가 호출한다.
        /// 모달이 열려 있으면 CharacterInfoPanelPresenter.Hide() 에서 자동 닫힌다.</summary>
        public void OnTabHidden()
        {
            _infoPanelPresenter?.Hide();
        }
    }
}
