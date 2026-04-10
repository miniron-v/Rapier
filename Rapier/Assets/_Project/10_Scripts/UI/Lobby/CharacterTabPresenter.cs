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
        private CharacterTabView _view;

        /// <summary>LobbyPresenter가 초기화 시 호출한다.</summary>
        public void Init(CharacterTabView view)
        {
            _view = view;
        }

        // ── 탭 전환 진입점 (LobbyPresenter가 호출) ───────────────
        /// <summary>탭이 표시될 때 LobbyPresenter가 호출한다.</summary>
        public void OnTabShown()
        {
            if (_view == null) return;
            _view.SetupCharacterSlots();
        }

        /// <summary>탭이 숨겨질 때 LobbyPresenter가 호출한다.</summary>
        public void OnTabHidden()
        {
            // 현재 등록된 버튼 리스너 없음 (Coming Soon 슬롯은 비인터랙티브)
        }
    }
}
