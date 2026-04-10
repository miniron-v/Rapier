using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Lobby.Mission
{
    /// <summary>
    /// 미션 목록 단일 항목 View.
    /// 진행 바, 설명, 보상 수령 버튼 표시.
    /// </summary>
    public class MissionItemView : MonoBehaviour
    {
        // ── Serialized Fields ─────────────────────────────────────
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private Slider          _progressBar;
        [SerializeField] private Button          _claimButton;
        [SerializeField] private TextMeshProUGUI _claimButtonText;

        // ── Private Fields ────────────────────────────────────────
        private string                 _missionId;
        private MissionPanelPresenter  _presenter;

        // ── 공개 메서드 ────────────────────────────────────────────

        /// <summary>데이터 바인딩. View 갱신만 수행.</summary>
        public void Bind(MissionItemViewData data, MissionPanelPresenter presenter)
        {
            _missionId = data.MissionId;
            _presenter = presenter;

            if (_descriptionText != null)
                _descriptionText.text = data.Description;

            if (_progressText != null)
                _progressText.text = $"{data.Current} / {data.Target}";

            if (_progressBar != null)
            {
                _progressBar.minValue = 0f;
                _progressBar.maxValue = data.Target;
                _progressBar.value    = data.Current;
            }

            if (_claimButton != null)
            {
                _claimButton.onClick.RemoveAllListeners();
                _claimButton.interactable = data.IsCompleted && !data.IsRewarded;
                if (_claimButtonText != null)
                    _claimButtonText.text = data.IsRewarded ? "수령 완료" : "보상 수령";
                _claimButton.onClick.AddListener(OnClaimClicked);
            }
        }

        // ── 이벤트 핸들러 ──────────────────────────────────────────

        private void OnClaimClicked()
        {
            _presenter?.OnClaimReward(_missionId);
        }
    }
}
