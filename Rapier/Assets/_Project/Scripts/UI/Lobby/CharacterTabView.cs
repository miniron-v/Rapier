using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 2 — 캐릭터 관리 패널 View.
    ///
    /// [Phase 23c 재설계]
    ///   - 기존 2×2 캐릭터 아이콘 슬롯 폐기
    ///   - CharacterInfoPanelView (일러스트 + 변경 버튼 + 좌3/우5 슬롯) 로 대체
    ///   - 인벤토리 영역 (EquipmentPanelRoot) 유지
    ///   - 레벨업 패널 영역 (LevelUpPanelRoot) 유지
    ///
    /// [B3 hook]
    ///   LevelUpPanelRoot 하위에 레벨업/스킬 강화 패널을 추가할 것.
    /// </summary>
    public class CharacterTabView : LobbyTabViewBase
    {
        // ── B2/B3 Hook Roots ─────────────────────────────────────
        /// <summary>[B2] 장비 슬롯 + 인벤토리 패널이 붙는 루트 GameObject.</summary>
        [SerializeField] private GameObject _equipmentPanelRoot;

        /// <summary>[B3] 레벨업/스킬 강화 패널이 붙는 루트 GameObject.</summary>
        [SerializeField] private GameObject _levelUpPanelRoot;

        /// <summary>B2 hook: 장비 패널이 붙을 루트 GameObject.</summary>
        public GameObject EquipmentPanelRoot => _equipmentPanelRoot;

        /// <summary>B3 hook: 레벨업/스킬 영역이 붙을 루트 GameObject.</summary>
        public GameObject LevelUpPanelRoot => _levelUpPanelRoot;

        /// <summary>CharacterTabPresenter가 초기화 시 호출한다.</summary>
        public void Init(
            GameObject equipmentPanelRoot,
            GameObject levelUpPanelRoot)
        {
            _equipmentPanelRoot = equipmentPanelRoot;
            _levelUpPanelRoot   = levelUpPanelRoot;
        }
    }
}
