using System;

namespace Game.Data.Missions
{
    /// <summary>
    /// 미션 진행 상태 런타임 모델. MonoBehaviour 미사용.
    /// 메모리와 JSON 저장 양쪽에 반영된다.
    /// </summary>
    public class MissionProgress
    {
        public MissionData Data        { get; }
        public int         Current     { get; private set; }
        public bool        IsCompleted { get; private set; }
        public bool        IsRewarded  { get; private set; }

        /// <summary>진행도 변경 이벤트. (currentCount, isCompleted)</summary>
        public event Action<int, bool> OnProgressChanged;

        public MissionProgress(MissionData data, int savedCurrent = 0,
                               bool savedCompleted = false, bool savedRewarded = false)
        {
            Data        = data;
            Current     = savedCurrent;
            IsCompleted = savedCompleted;
            IsRewarded  = savedRewarded;
        }

        /// <summary>
        /// 진행도를 amount만큼 증가시킨다.
        /// 이미 완료된 미션은 무시한다.
        /// </summary>
        public void Increment(int amount = 1)
        {
            if (IsCompleted) return;
            Current = Math.Min(Current + amount, Data.TargetCount);
            if (Current >= Data.TargetCount)
                IsCompleted = true;
            OnProgressChanged?.Invoke(Current, IsCompleted);
        }

        /// <summary>보상 수령 처리. 이미 수령한 경우 false 반환.</summary>
        public bool ClaimReward()
        {
            if (!IsCompleted || IsRewarded) return false;
            IsRewarded = true;
            return true;
        }

        /// <summary>리셋 시 진행 상태 초기화.</summary>
        public void Reset()
        {
            Current     = 0;
            IsCompleted = false;
            IsRewarded  = false;
            OnProgressChanged?.Invoke(Current, IsCompleted);
        }
    }
}
