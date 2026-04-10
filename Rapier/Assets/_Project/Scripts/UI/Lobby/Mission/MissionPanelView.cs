using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Lobby.Mission
{
    /// <summary>
    /// 미션 패널 View. 화면 표시만 담당. 로직 없음.
    /// §9-4 미션 탭 hook 연결점.
    /// </summary>
    public class MissionPanelView : MonoBehaviour, IMissionPanelView
    {
        // ── Serialized Fields ─────────────────────────────────────
        [Header("탭")]
        [SerializeField] private Button _dailyTabButton;
        [SerializeField] private Button _weeklyTabButton;

        [Header("미션 목록 컨테이너")]
        [SerializeField] private Transform       _listContainer;
        [SerializeField] private MissionItemView _itemPrefab;

        [Header("토스트")]
        [SerializeField] private TextMeshProUGUI _toastText;
        [SerializeField] private float           _toastDuration = 2f;

        // ── Private Fields ────────────────────────────────────────
        private MissionPanelPresenter      _presenter;
        private readonly List<MissionItemView> _spawnedItems = new();
        private Coroutine                  _toastCoroutine;

        // ── 공개 초기화 ────────────────────────────────────────────

        /// <summary>Presenter 주입 및 버튼 이벤트 연결.</summary>
        public void Init(MissionPanelPresenter presenter)
        {
            _presenter = presenter;
            _dailyTabButton?.onClick.AddListener(() => _presenter.OnDailyTabSelected());
            _weeklyTabButton?.onClick.AddListener(() => _presenter.OnWeeklyTabSelected());
        }

        // ── IMissionPanelView 구현 ─────────────────────────────────

        /// <summary>미션 목록 UI를 갱신한다.</summary>
        public void RefreshList(IReadOnlyList<MissionItemViewData> items)
        {
            // 기존 항목 비활성화 (풀링 간소화)
            foreach (var item in _spawnedItems)
                item.gameObject.SetActive(false);

            for (int i = 0; i < items.Count; i++)
            {
                MissionItemView itemView;
                if (i < _spawnedItems.Count)
                {
                    itemView = _spawnedItems[i];
                    itemView.gameObject.SetActive(true);
                }
                else
                {
                    itemView = Instantiate(_itemPrefab, _listContainer);
                    _spawnedItems.Add(itemView);
                }
                itemView.Bind(items[i], _presenter);
            }
        }

        /// <summary>탭 선택 UI 갱신.</summary>
        public void SetActiveTab(bool isDaily)
        {
            // 탭 강조 표시는 컬러로 처리 (확장 가능)
            if (_dailyTabButton  != null)
                _dailyTabButton.interactable  = !isDaily;
            if (_weeklyTabButton != null)
                _weeklyTabButton.interactable = isDaily;
        }

        /// <summary>보상 수령 토스트.</summary>
        public void ShowRewardToast(string message)
        {
            if (_toastText == null) return;
            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(ShowToastCoroutine(message));
        }

        // ── 내부 ──────────────────────────────────────────────────

        private System.Collections.IEnumerator ShowToastCoroutine(string message)
        {
            _toastText.text    = message;
            _toastText.enabled = true;
            yield return new WaitForSeconds(_toastDuration);
            _toastText.enabled = false;
            _toastCoroutine    = null;
        }
    }
}
