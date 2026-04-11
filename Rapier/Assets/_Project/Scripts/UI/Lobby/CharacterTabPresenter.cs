using Game.Core;
using Game.Data.Equipment;
using Game.UI.Lobby.Equipment;
using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 2 — 캐릭터 관리 탭 Presenter.
    ///
    /// [역할]
    ///   - Rapier 슬롯 활성화, 나머지 3칸 Coming Soon 상태 설정
    ///   - B2 장비 영역 / B3 레벨업 영역 hook: 연결만 예약, 실구현 없음
    ///
    /// [이벤트 구독/해제]
    ///   OnTabShown (LobbyPresenter가 탭 활성화 시 호출): 슬롯 초기화
    ///   OnTabHidden (LobbyPresenter가 탭 비활성화 시 호출): 정리
    ///
    /// ── 구독/이벤트 매핑 ────────────────────────────────────────────────
    /// | 이벤트          | 구독 위치    | 해제 위치    | 핸들러             |
    /// |-----------------|-------------|-------------|-------------------|
    /// | (없음 — 슬롯 버튼 Coming Soon 비인터랙티브) |
    /// ─────────────────────────────────────────────────────────────────────
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

        // ── 탭 전환 진입점 (LobbyPresenter가 호출) ───────────────
        /// <summary>탭이 표시될 때 LobbyPresenter가 호출한다.</summary>
        public void OnTabShown()
        {
            if (_view == null) return;
            _view.SetupCharacterSlots();

            if (_equipmentPanel == null) return;
            if (!_equipmentPanel.IsInitialized)
            {
                var manager = ServiceLocator.TryGet<EquipmentManager>();
                if (manager != null)
                    _equipmentPanel.Init(manager, "Rapier");
            }
            _equipmentPanel.Show();
        }

        /// <summary>탭이 숨겨질 때 LobbyPresenter가 호출한다.</summary>
        public void OnTabHidden()
        {
            // 현재 등록된 버튼 리스너 없음 (Coming Soon 슬롯은 비인터랙티브)
        }
    }
}
