using System.Collections.Generic;
using Game.Data.Missions;
using UnityEngine;

namespace Game.UI.Lobby.Mission
{
    /// <summary>
    /// 미션 패널 Presenter. Model-View 중재.
    /// OnEnable/OnDisable에서 이벤트 구독/해제 쌍 보장.
    /// </summary>
    public class MissionPanelPresenter : MonoBehaviour
    {
        // ── Serialized Fields ─────────────────────────────────────
        [SerializeField] private MissionPanelView _view;

        // ── Private Fields ────────────────────────────────────────
        private MissionManager _manager;
        private bool           _showingDaily = true;

        // ── 공개 초기화 ────────────────────────────────────────────

        /// <summary>MissionManager 주입 및 View 초기화.</summary>
        public void Init(MissionManager manager)
        {
            _manager = manager;
            RefreshView();
        }

        // ── Unity Lifecycle ────────────────────────────────────────

        private void OnEnable()
        {
            if (_manager != null)
                _manager.OnMissionProgressChanged += HandleMissionProgressChanged;
        }

        private void OnDisable()
        {
            if (_manager != null)
                _manager.OnMissionProgressChanged -= HandleMissionProgressChanged;
        }

        // ── 공개 메서드 (View에서 호출) ────────────────────────────

        /// <summary>일일 탭 선택.</summary>
        public void OnDailyTabSelected()
        {
            _showingDaily = true;
            _view.SetActiveTab(isDaily: true);
            RefreshView();
        }

        /// <summary>주간 탭 선택.</summary>
        public void OnWeeklyTabSelected()
        {
            _showingDaily = false;
            _view.SetActiveTab(isDaily: false);
            RefreshView();
        }

        /// <summary>보상 수령 버튼 클릭.</summary>
        public void OnClaimReward(string missionId)
        {
            if (_manager == null) return;
            bool success = _manager.ClaimReward(missionId);
            if (success)
            {
                _view.ShowRewardToast("보상 수령 완료!");
                RefreshView();
            }
        }

        // ── 이벤트 핸들러 ──────────────────────────────────────────

        private void HandleMissionProgressChanged(string missionId, int current, bool isCompleted)
        {
            RefreshView();
        }

        // ── 내부 메서드 ────────────────────────────────────────────

        private void RefreshView()
        {
            if (_manager == null || _view == null) return;

            var source = _showingDaily
                ? (IReadOnlyList<MissionProgress>)_manager.DailyProgresses
                : _manager.WeeklyProgresses;

            var items = new List<MissionItemViewData>(source.Count);
            foreach (var p in source)
            {
                items.Add(new MissionItemViewData
                {
                    MissionId   = p.Data.MissionId,
                    Description = p.Data.Description,
                    Current     = p.Current,
                    Target      = p.Data.TargetCount,
                    IsCompleted = p.IsCompleted,
                    IsRewarded  = p.IsRewarded,
                });
            }
            _view.RefreshList(items);
        }
    }
}
