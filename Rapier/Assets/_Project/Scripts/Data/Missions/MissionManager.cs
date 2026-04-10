using System;
using System.Collections.Generic;
using Game.Data.Save;
using UnityEngine;

namespace Game.Data.Missions
{
    /// <summary>
    /// 미션 진행 갱신, 04:00 리셋 판정, 보상 지급 담당.
    /// MonoBehaviour. OnEnable/OnDisable에서 이벤트 구독/해제 쌍 보장.
    /// §10 기준 일일 5종, 주간 3종.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        // ── 정적 이벤트 (게임 이벤트 통신) ───────────────────────────
        /// <summary>스테이지 클리어 알림. 외부에서 호출.</summary>
        public static event Action OnStageCleared;
        /// <summary>보스 처치 알림. 외부에서 호출.</summary>
        public static event Action OnBossKilled;
        /// <summary>저스트 회피 성공 알림. 외부에서 호출.</summary>
        public static event Action OnJustDodgeTriggered;
        /// <summary>차지 스킬 사용 알림. 외부에서 호출.</summary>
        public static event Action OnChargeSkillUsed;
        /// <summary>최고 기록 갱신 알림. 외부에서 호출.</summary>
        public static event Action OnStageRecordUpdated;

        // ── Serialized Fields ─────────────────────────────────────
        [SerializeField] private MissionData[] _dailyMissionDataSet;
        [SerializeField] private MissionData[] _weeklyMissionDataSet;

        // ── Private Fields ────────────────────────────────────────
        private readonly List<MissionProgress> _dailyProgresses  = new();
        private readonly List<MissionProgress> _weeklyProgresses = new();

        private SaveManager _saveManager;

        // ── 완료 카운트 (주간 #3 조건용) ─────────────────────────────
        [NonSerialized] private int _dailyAllCompletedDays = 0;

        // ── 이벤트 ────────────────────────────────────────────────
        /// <summary>미션 진행도 변경 이벤트. (missionId, current, isCompleted)</summary>
        public event Action<string, int, bool> OnMissionProgressChanged;

        // ── Properties ────────────────────────────────────────────
        public IReadOnlyList<MissionProgress> DailyProgresses  => _dailyProgresses;
        public IReadOnlyList<MissionProgress> WeeklyProgresses => _weeklyProgresses;

        // ── 공개 초기화 ────────────────────────────────────────────

        /// <summary>SaveManager 주입 및 저장 데이터 기반 초기화.</summary>
        public void Init(SaveManager saveManager)
        {
            _saveManager = saveManager;
            BuildProgresses(_saveManager.Current);
            CheckAndApplyReset();
        }

        // ── Unity Lifecycle ────────────────────────────────────────

        private void OnEnable()
        {
            OnStageCleared        += HandleStageCleared;
            OnBossKilled          += HandleBossKilled;
            OnJustDodgeTriggered  += HandleJustDodge;
            OnChargeSkillUsed     += HandleChargeSkill;
            OnStageRecordUpdated  += HandleStageRecord;
        }

        private void OnDisable()
        {
            OnStageCleared        -= HandleStageCleared;
            OnBossKilled          -= HandleBossKilled;
            OnJustDodgeTriggered  -= HandleJustDodge;
            OnChargeSkillUsed     -= HandleChargeSkill;
            OnStageRecordUpdated  -= HandleStageRecord;
        }

        // ── 공개 메서드 ────────────────────────────────────────────

        /// <summary>
        /// 지정 미션의 보상을 수령하고 재화를 지급한다.
        /// 성공 시 SaveManager.Save() 호출.
        /// </summary>
        /// <returns>수령 성공 여부</returns>
        public bool ClaimReward(string missionId)
        {
            var progress = FindProgress(missionId);
            if (progress == null) return false;
            if (!progress.ClaimReward()) return false;

            ApplyReward(progress.Data.Reward);
            FlushToSave();
            _saveManager.Save();

            // 일일 미션 전체 완료 체크 → 주간 미션 #3 조건 갱신
            CheckDailyAllCompleted();
            return true;
        }

        // ── 리셋 로직 ──────────────────────────────────────────────

        /// <summary>
        /// 현재 시각(UTC) 기준 04:00 리셋 판정.
        /// 일일: 하루 이상 경과, 주간: 월요일 04:00 이후 경과.
        /// </summary>
        public void CheckAndApplyReset()
        {
            var now = DateTime.UtcNow;

            // ── 일일 리셋 ──────────────────────────────────────────
            var lastDaily = ParseReset(_saveManager.Current.lastDailyReset);
            if (ShouldResetDaily(lastDaily, now))
            {
                foreach (var p in _dailyProgresses) p.Reset();
                _saveManager.Current.lastDailyReset = FormatReset(now);
                Debug.Log("[MissionManager] Daily missions reset.");
            }

            // ── 주간 리셋 ──────────────────────────────────────────
            var lastWeekly = ParseReset(_saveManager.Current.lastWeeklyReset);
            if (ShouldResetWeekly(lastWeekly, now))
            {
                foreach (var p in _weeklyProgresses) p.Reset();
                _dailyAllCompletedDays = 0;
                _saveManager.Current.lastWeeklyReset = FormatReset(now);
                Debug.Log("[MissionManager] Weekly missions reset.");
            }
        }

        // ── 이벤트 핸들러 ──────────────────────────────────────────

        private void HandleStageCleared()
            => IncrementByEvent(MissionEvent.OnStageCleared);

        private void HandleBossKilled()
            => IncrementByEvent(MissionEvent.OnBossKilled);

        private void HandleJustDodge()
            => IncrementByEvent(MissionEvent.OnJustDodgeTriggered);

        private void HandleChargeSkill()
            => IncrementByEvent(MissionEvent.OnChargeSkillUsed);

        private void HandleStageRecord()
            => IncrementByEvent(MissionEvent.OnStageRecordUpdated);

        // ── 내부 메서드 ────────────────────────────────────────────

        private void BuildProgresses(SaveData saveData)
        {
            _dailyProgresses.Clear();
            foreach (var data in _dailyMissionDataSet)
            {
                var entry = FindSavedEntry(saveData.dailyMissions, data.MissionId);
                _dailyProgresses.Add(new MissionProgress(data,
                    entry?.currentCount ?? 0,
                    entry?.isCompleted  ?? false,
                    entry?.isRewarded   ?? false));
            }

            _weeklyProgresses.Clear();
            foreach (var data in _weeklyMissionDataSet)
            {
                var entry = FindSavedEntry(saveData.weeklyMissions, data.MissionId);
                _weeklyProgresses.Add(new MissionProgress(data,
                    entry?.currentCount ?? 0,
                    entry?.isCompleted  ?? false,
                    entry?.isRewarded   ?? false));
            }

            // 진행도 변경 이벤트 구독
            foreach (var p in _dailyProgresses)
            {
                var captured = p;
                p.OnProgressChanged += (cur, comp) =>
                    OnMissionProgressChanged?.Invoke(captured.Data.MissionId, cur, comp);
            }
            foreach (var p in _weeklyProgresses)
            {
                var captured = p;
                p.OnProgressChanged += (cur, comp) =>
                    OnMissionProgressChanged?.Invoke(captured.Data.MissionId, cur, comp);
            }
        }

        private void IncrementByEvent(MissionEvent mEvent)
        {
            bool changed = false;
            foreach (var p in _dailyProgresses)
            {
                if (p.Data.TrackEvent == mEvent && !p.IsCompleted)
                {
                    p.Increment();
                    changed = true;
                }
            }
            foreach (var p in _weeklyProgresses)
            {
                if (p.Data.TrackEvent == mEvent && !p.IsCompleted)
                {
                    p.Increment();
                    changed = true;
                }
            }

            if (changed)
            {
                // 일일 미션 4개 완료 (메타 미션) 체크
                int completedDaily = CountCompleted(_dailyProgresses);
                if (completedDaily >= 4)
                    IncrementByEvent(MissionEvent.OnDailyMissionCompleted);

                FlushToSave();
            }
        }

        private void CheckDailyAllCompleted()
        {
            // 일일 미션 전체(보상 수령 포함) 완료 여부
            bool allDone = true;
            foreach (var p in _dailyProgresses)
                if (!p.IsRewarded) { allDone = false; break; }

            if (allDone)
            {
                _dailyAllCompletedDays++;
                IncrementByEvent(MissionEvent.OnDailyAllCompleted);
            }
        }

        private int CountCompleted(List<MissionProgress> progresses)
        {
            int count = 0;
            foreach (var p in progresses)
                if (p.IsCompleted) count++;
            return count;
        }

        private void ApplyReward(MissionReward reward)
        {
            if (_saveManager == null) return;
            var data = _saveManager.Current;
            data.gold              += reward.gold;
            data.gachaTicket       += reward.gachaTicket;
            data.reinforceMaterial += reward.reinforceMaterial;
            data.runeGachaTicket   += reward.runeGachaTicket;
            // epicEquipCount는 인벤토리 시스템(B2)이 처리한다.
            Debug.Log($"[MissionManager] Reward applied: gold+{reward.gold}, ticket+{reward.gachaTicket}");
        }

        private void FlushToSave()
        {
            if (_saveManager == null) return;
            var data = _saveManager.Current;

            data.dailyMissions.Clear();
            foreach (var p in _dailyProgresses)
                data.dailyMissions.Add(new MissionProgressEntry
                {
                    missionId    = p.Data.MissionId,
                    currentCount = p.Current,
                    isCompleted  = p.IsCompleted,
                    isRewarded   = p.IsRewarded,
                });

            data.weeklyMissions.Clear();
            foreach (var p in _weeklyProgresses)
                data.weeklyMissions.Add(new MissionProgressEntry
                {
                    missionId    = p.Data.MissionId,
                    currentCount = p.Current,
                    isCompleted  = p.IsCompleted,
                    isRewarded   = p.IsRewarded,
                });
        }

        private MissionProgress FindProgress(string missionId)
        {
            foreach (var p in _dailyProgresses)
                if (p.Data.MissionId == missionId) return p;
            foreach (var p in _weeklyProgresses)
                if (p.Data.MissionId == missionId) return p;
            return null;
        }

        private static MissionProgressEntry FindSavedEntry(
            List<MissionProgressEntry> entries, string id)
        {
            if (entries == null) return null;
            foreach (var e in entries)
                if (e.missionId == id) return e;
            return null;
        }

        // ── 리셋 시각 판정 유틸리티 ────────────────────────────────

        /// <summary>마지막 리셋 이후 04:00(UTC)가 한 번 이상 지났으면 true.</summary>
        private static bool ShouldResetDaily(DateTime last, DateTime now)
        {
            var resetToday = now.Date.AddHours(4);
            if (resetToday > now) resetToday = resetToday.AddDays(-1);
            return last < resetToday;
        }

        /// <summary>마지막 리셋 이후 월요일 04:00(UTC)가 한 번 이상 지났으면 true.</summary>
        private static bool ShouldResetWeekly(DateTime last, DateTime now)
        {
            // 현재 주의 월요일 04:00 계산
            int daysToMonday = ((int)now.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var mondayReset  = now.Date.AddDays(-daysToMonday).AddHours(4);
            if (mondayReset > now) mondayReset = mondayReset.AddDays(-7);
            return last < mondayReset;
        }

        private static DateTime ParseReset(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return DateTime.MinValue;
            return DateTime.TryParse(iso, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt
                : DateTime.MinValue;
        }

        private static string FormatReset(DateTime dt)
            => dt.ToString("O"); // ISO 8601 round-trip
    }
}
