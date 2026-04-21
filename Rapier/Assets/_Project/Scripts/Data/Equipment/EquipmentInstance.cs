using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 런타임 장비 인스턴스. SO 정의(EquipmentItemData)를 참조하고,
    /// 룬 장착 상태와 드롭 시 롤된 스탯을 런타임에 관리한다.
    ///
    /// Phase 22-B:
    ///   - SubStats: SO 고정 복사 제거 → 드롭 시 풀 기반 롤 결과 저장.
    ///   - RolledMainStat: 장신구 전용 롤된 메인스탯 (null 이면 SO._mainStat fallback).
    /// </summary>
    [Serializable]
    public class EquipmentInstance
    {
        [SerializeField] private string _instanceId;
        [SerializeField] private EquipmentItemData _data;

        // 룬 슬롯은 SO 등급에서 결정된 개수만큼 허용. null = 비어있음.
        [NonSerialized] private RuneItemData[] _equippedRunes;

        // 런타임 Grade 오버라이드 (저장값 우선 — §7-4). null 이면 SO 원본값 사용.
        [NonSerialized] private EquipmentGrade? _runtimeGrade;

        // Phase 22-B: 드롭 시 롤된 서브스탯 목록
        [NonSerialized] private List<StatEntry> _subStats;

        // Phase 22-B: 장신구 전용 롤된 메인스탯. null 이면 SO._mainStat 사용.
        [NonSerialized] private StatEntry? _rolledMainStat;

        // Phase 25-A: 강화 단계 (0 = 강화 없음)
        [NonSerialized] private int _enhanceLevel;

        // Phase 27: 아이템 잠금 상태 (true = 잠금, 분해 불가)
        [NonSerialized] private bool _isLocked;

        // ── 프로퍼티 ────────────────────────────────────────────────────────

        /// <summary>인스턴스 고유 ID</summary>
        public string InstanceId => _instanceId;
        /// <summary>장비 SO 정의</summary>
        public EquipmentItemData Data => _data;
        /// <summary>현재 장착된 룬 배열 (인덱스 = 소켓 번호)</summary>
        public RuneItemData[] EquippedRunes => _equippedRunes;
        /// <summary>
        /// 런타임 등급. 저장된 값이 있으면 저장값을, 없으면 SO 원본 Grade 를 반환한다.
        /// 강화/재감정 시스템에서 저장값이 SO 원본과 달라질 수 있다 (§7-4).
        /// </summary>
        public EquipmentGrade Grade => _runtimeGrade ?? (_data != null ? _data.Grade : EquipmentGrade.Normal);

        /// <summary>
        /// 드롭 시 롤된 서브스탯 목록 (읽기 전용). Phase 22-B 이후 모든 서브스탯은 여기서 조회한다.
        /// </summary>
        public IReadOnlyList<StatEntry> SubStats => _subStats;

        /// <summary>
        /// 장신구 전용 롤된 메인스탯. null 이면 해당 SO._mainStat 으로 fallback.
        /// 무기/방어구에서는 항상 null.
        /// </summary>
        public StatEntry? RolledMainStat => _rolledMainStat;

        /// <summary>현재 강화 단계. 0 = 강화 없음.</summary>
        public int EnhanceLevel => _enhanceLevel;

        /// <summary>이 장비의 최대 강화 단계 (등급 기반).</summary>
        public int MaxEnhanceLevel => EquipmentGradeHelper.GetMaxEnhance(Grade);

        /// <summary>아이템 잠금 여부. true 이면 분해 일괄/개별 선택 불가.</summary>
        public bool IsLocked => _isLocked;

        // ── 생성자 (드롭 경로) ───────────────────────────────────────────────

        /// <summary>
        /// 드롭 시 새 장비 인스턴스를 생성한다. 풀 기반으로 서브스탯과 메인스탯을 롤한다.
        /// </summary>
        public EquipmentInstance(EquipmentItemData data)
        {
            _instanceId    = Guid.NewGuid().ToString();
            _data          = data;
            _equippedRunes = new RuneItemData[data != null ? data.RuneSocketCount : 0];
            _subStats      = new List<StatEntry>();

            if (data == null) return;

            EquipmentGrade grade = data.Grade;

            // ── 서브스탯 롤 ──────────────────────────────────────────────────
            // N = (int)grade: Normal=0, Rare=1, Epic=2, Unique=3
            int subCount = (int)grade;
            if (subCount > 0 && data.SubStatPool != null)
            {
                var exclude = new HashSet<StatType>();
                var entries = data.SubStatPool.Entries;
                for (int i = 0; i < subCount; i++)
                {
                    var picked = StatRollEntry.PickWeighted(entries, exclude);
                    if (picked == null) break;

                    float value = picked.Roll(grade);
                    var entry = BuildStatEntry(picked.statType, value, picked.usePercent);
                    _subStats.Add(entry);
                    exclude.Add(picked.statType);
                }
            }

            // ── 장신구 메인스탯 롤 ──────────────────────────────────────────
            bool isAccessory = data.SlotType == EquipmentSlotType.Necklace
                            || data.SlotType == EquipmentSlotType.Ring;
            if (isAccessory && data.MainStatPool != null)
            {
                var picked = StatRollEntry.PickWeighted(data.MainStatPool.Entries, null);
                if (picked != null)
                {
                    float value = picked.Roll(grade);
                    _rolledMainStat = BuildStatEntry(picked.statType, value, picked.usePercent);
                }
            }
        }

        // ── 생성자 (저장 복원 경로) ──────────────────────────────────────────

        /// <summary>
        /// 저장 데이터로부터 장비 인스턴스를 복원한다. 롤을 다시 수행하지 않고 저장값을 그대로 사용한다.
        /// </summary>
        internal EquipmentInstance(
            string instanceId,
            EquipmentItemData data,
            EquipmentGrade runtimeGrade,
            List<StatEntry> subStats,
            bool hasRolledMain,
            StatEntry rolledMain,
            int enhanceLevel = 0)
        {
            _instanceId    = instanceId;
            _data          = data;
            _runtimeGrade  = runtimeGrade;
            _subStats      = subStats ?? new List<StatEntry>();
            _enhanceLevel  = enhanceLevel;

            // 소켓 수는 저장값 Grade 기준으로 결정 (SO 원본 Grade 아님)
            int socketCount = EquipmentGradeHelper.GetRuneSocketCount(runtimeGrade);
            _equippedRunes = new RuneItemData[socketCount];

            _rolledMainStat = hasRolledMain ? (StatEntry?)rolledMain : null;
        }

        /// <summary>
        /// 저장 데이터로부터 장비 인스턴스를 복원한다 (Phase 14 Deserialize 호환 경로).
        /// subStats/rolledMain 없이 이전 포맷 저장 데이터 로드 시 사용.
        /// </summary>
        internal EquipmentInstance(string instanceId, EquipmentItemData data, EquipmentGrade runtimeGrade)
            : this(instanceId, data, runtimeGrade, null, false, default, 0)
        {
        }

        // ── 룬 조작 ────────────────────────────────────────────────────────

        /// <summary>지정 소켓에 룬을 장착한다. 범위 초과 시 false 반환.</summary>
        public bool EquipRune(int socketIndex, RuneItemData rune)
        {
            if (socketIndex < 0 || socketIndex >= _equippedRunes.Length)
            {
                Debug.LogWarning($"[EquipmentInstance] 소켓 인덱스 {socketIndex} 범위 초과 (최대 {_equippedRunes.Length - 1})");
                return false;
            }
            _equippedRunes[socketIndex] = rune;
            return true;
        }

        /// <summary>지정 소켓의 룬을 해제한다.</summary>
        public bool UnequipRune(int socketIndex)
        {
            if (socketIndex < 0 || socketIndex >= _equippedRunes.Length)
            {
                Debug.LogWarning($"[EquipmentInstance] 소켓 인덱스 {socketIndex} 범위 초과");
                return false;
            }
            _equippedRunes[socketIndex] = null;
            return true;
        }

        /// <summary>런타임 복원 시 룬 슬롯 배열을 초기화한다. (저장 로드 후 호출)</summary>
        public void InitRunes()
        {
            _equippedRunes = new RuneItemData[_data != null ? _data.RuneSocketCount : 0];
        }

        // ── 강화 조작 (Phase 25-A) ────────────────────────────────────────────

        /// <summary>
        /// 강화 단계를 설정한다. 저장 복원 및 강화 API 전용.
        /// 직접 호출은 EquipmentManager.TryEnhance 또는 Deserialize 경로에서만.
        /// </summary>
        internal void SetEnhanceLevel(int level)
        {
            _enhanceLevel = level;
        }

        /// <summary>
        /// 아이템 잠금 상태를 설정한다. EquipmentManager.SetItemLocked 전용.
        /// </summary>
        public void SetLocked(bool locked)
        {
            _isLocked = locked;
        }

        /// <summary>
        /// 서브스탯 누적값을 강화로 가산한다. EquipmentManager.TryEnhance 전용.
        /// </summary>
        /// <param name="index">_subStats 내 인덱스</param>
        /// <param name="deltaFlat">flat 가산량</param>
        /// <param name="deltaPercent">percent 가산량</param>
        internal void EnhanceSubStat(int index, float deltaFlat, float deltaPercent)
        {
            if (_subStats == null || index < 0 || index >= _subStats.Count) return;
            var sub = _subStats[index];
            sub.flatValue    += deltaFlat;
            sub.percentValue += deltaPercent;
            _subStats[index]  = sub;
        }

        // ── 내부 헬퍼 ──────────────────────────────────────────────────────

        /// <summary>
        /// usePercent 에 따라 flat 또는 percent 필드에 값을 세팅한 StatEntry 를 생성한다.
        /// </summary>
        private static StatEntry BuildStatEntry(StatType statType, float value, bool usePercent)
        {
            return new StatEntry
            {
                statType     = statType,
                flatValue    = usePercent ? 0f    : value,
                percentValue = usePercent ? value : 0f,
            };
        }
    }
}
