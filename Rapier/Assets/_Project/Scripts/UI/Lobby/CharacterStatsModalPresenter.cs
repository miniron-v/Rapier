using Game.Characters;
using Game.Data.Stats;
using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 캐릭터 스탯 보기 모달 Presenter.
    /// CharacterInfoPanelPresenter 에서 Show(characterId, baseData) 를 호출하면
    /// CharacterStatSnapshot 을 빌드해 View 에 전달한다.
    /// </summary>
    public class CharacterStatsModalPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private CharacterStatsModalView _view;

        // ── 초기화 ───────────────────────────────────────────────────────────

        private void Awake()
        {
            SubscribeView();
        }

        private void OnDestroy()
        {
            UnsubscribeView();
        }

        /// <summary>LobbyHudSetup 에서 런타임 참조를 주입한다.</summary>
        public void InitReferences(CharacterStatsModalView view)
        {
            UnsubscribeView();
            _view = view;
            SubscribeView();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// 모달을 열고 characterId 캐릭터의 현재 스탯을 표시한다.
        /// </summary>
        /// <param name="characterId">캐릭터 식별자</param>
        /// <param name="baseData">기본 스탯 SO (null 이면 기본값 0)</param>
        public void Show(string characterId, CharacterStatData baseData)
        {
            if (_view == null) return;
            var snap = CharacterStatSnapshot.Build(characterId, baseData);
            _view.Show(snap);
        }

        /// <summary>모달을 닫는다.</summary>
        public void Hide()
        {
            _view?.Hide();
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private void SubscribeView()
        {
            if (_view == null) return;
            _view.OnCloseClicked -= HandleCloseClicked;
            _view.OnCloseClicked += HandleCloseClicked;
        }

        private void UnsubscribeView()
        {
            if (_view == null) return;
            _view.OnCloseClicked -= HandleCloseClicked;
        }

        private void HandleCloseClicked()
        {
            Hide();
        }
    }
}
