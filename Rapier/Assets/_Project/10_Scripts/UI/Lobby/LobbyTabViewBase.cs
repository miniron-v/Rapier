using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 모든 로비 탭 View의 공통 기반 클래스.
    /// Show/Hide만 담당하며 로직 없음.
    /// </summary>
    public abstract class LobbyTabViewBase : MonoBehaviour, ILobbyTabView
    {
        /// <inheritdoc/>
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        /// <inheritdoc/>
        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
