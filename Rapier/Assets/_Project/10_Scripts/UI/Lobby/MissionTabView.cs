using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 4 — 미션 패널 View.
    ///
    /// [B1 구현]
    ///   플레이스홀더 패널만 표시.
    ///
    /// [B3 hook]
    ///   MissionPanelRoot 하위에 일일/주간 미션 목록과 보상 수령 UI를 추가할 것.
    ///   MissionTabPresenter를 통해 저장 데이터(JSON)와 연동한다.
    /// </summary>
    public class MissionTabView : LobbyTabViewBase
    {
        // [B3] 이 GameObject 하위에 미션 목록 UI 추가
        // Inspector에서 "MissionPanelRoot" 이름의 자식 GameObject를 할당한다.
        [SerializeField] private GameObject _missionPanelRoot;

        /// <summary>B3 hook: 미션 패널이 붙을 루트 GameObject.</summary>
        public GameObject MissionPanelRoot => _missionPanelRoot;

        /// <summary>
        /// MissionTabPresenter가 초기화 시 호출한다.
        /// </summary>
        public void Init(GameObject missionPanelRoot)
        {
            _missionPanelRoot = missionPanelRoot;
        }
    }
}
