using System.Collections.Generic;
using UnityEngine;
using Game.Data.Equipment;

namespace Game.UI.Lobby.Shop
{
    /// <summary>
    /// 가챠 결과 모달 Presenter.
    /// GachaResultModalView를 제어하고 닫기 이벤트를 처리한다.
    /// </summary>
    public class GachaResultModalPresenter : MonoBehaviour
    {
        [SerializeField] private GachaResultModalView _view;

        /// <summary>참조 주입.</summary>
        public void InitReferences(GachaResultModalView view)
        {
            _view = view;
        }

        private void OnEnable()
        {
            if (_view != null) _view.OnCloseClicked += HandleCloseClicked;
        }

        private void OnDisable()
        {
            if (_view != null) _view.OnCloseClicked -= HandleCloseClicked;
        }

        /// <summary>모달을 열고 결과 아이템 목록을 표시한다.</summary>
        public void Show(IReadOnlyList<EquipmentInstance> items)
        {
            _view.SetVisible(true);
            _view.ShowResults(items);
        }

        // ── Event Handlers ────────────────────────────────────────────────

        private void HandleCloseClicked()
        {
            _view.SetVisible(false);
        }
    }
}
