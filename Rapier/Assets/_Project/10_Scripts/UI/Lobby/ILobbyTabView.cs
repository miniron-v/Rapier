using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 로비 탭 인덱스 (1-기준, DesignDoc §9 기준).
    /// </summary>
    public enum LobbyTabIndex
    {
        Shop        = 1,
        Character   = 2,
        Home        = 3,
        Mission     = 4,
        Settings    = 5,
    }

    /// <summary>
    /// 개별 탭 패널 View의 공통 계약.
    /// Presenter가 이 인터페이스를 통해 View와 통신한다.
    /// </summary>
    public interface ILobbyTabView
    {
        /// <summary>패널을 화면에 표시한다.</summary>
        void Show();

        /// <summary>패널을 화면에서 숨긴다.</summary>
        void Hide();
    }
}
