using System.Collections.Generic;

namespace Game.UI.Lobby.Mission
{
    /// <summary>
    /// 미션 패널 View 계약 인터페이스.
    /// Presenter가 이 인터페이스에만 의존한다 (DIP).
    /// </summary>
    public interface IMissionPanelView
    {
        /// <summary>미션 목록 UI를 갱신한다.</summary>
        void RefreshList(IReadOnlyList<MissionItemViewData> items);

        /// <summary>탭(일일/주간) 선택 UI를 갱신한다.</summary>
        void SetActiveTab(bool isDaily);

        /// <summary>보상 수령 결과 토스트를 표시한다.</summary>
        void ShowRewardToast(string message);
    }

    /// <summary>미션 목록 항목 뷰 데이터.</summary>
    public class MissionItemViewData
    {
        public string MissionId   { get; set; }
        public string Description { get; set; }
        public int    Current     { get; set; }
        public int    Target      { get; set; }
        public bool   IsCompleted { get; set; }
        public bool   IsRewarded  { get; set; }
    }
}
