using System;
using UnityEngine;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 분해 완료 결과 모달 Presenter.
    /// sortingOrder 400 Canvas 에 올라간다.
    ///
    /// 이벤트 구독/해제: OnEnable / OnDisable 쌍.
    /// </summary>
    public class DismantleResultModalPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private DismantleResultModalView _view;

        // ── Private Fields ───────────────────────────────────────────────────

        private Action _onClosedCallback;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>런타임 생성 시 View 참조를 주입한다 (LobbyHudSetup 호출).</summary>
        public void InitReferences(DismantleResultModalView view)
        {
            _view = view;
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_view == null) return;
            _view.OnCloseClicked += HandleCloseClicked;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.OnCloseClicked -= HandleCloseClicked;
        }

        // ── Public 메서드 ────────────────────────────────────────────────────

        /// <summary>
        /// 모달을 열어 분해 결과를 표시한다.
        /// </summary>
        /// <param name="totalDust">획득한 가루 총량.</param>
        /// <param name="onClosed">닫기 후 실행할 콜백 (분해 모드 종료 등).</param>
        public void Show(int totalDust, Action onClosed = null)
        {
            if (_view == null) return;

            _onClosedCallback = onClosed;
            _view.SetBody(totalDust);
            _view.transform.SetAsLastSibling();
            _view.SetVisible(true);
        }

        /// <summary>모달을 강제 닫는다.</summary>
        public void Hide()
        {
            _view?.SetVisible(false);
            _onClosedCallback = null;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleCloseClicked()
        {
            _view?.SetVisible(false);
            var cb = _onClosedCallback;
            _onClosedCallback = null;
            cb?.Invoke();
        }
    }
}
