using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 장비/룬 SO 레지스트리 (ScriptableObject).
    /// <para>
    /// <c>dataAssetId</c> / <c>runeAssetId</c> (= SO 에셋 이름) 로부터 런타임 SO 레퍼런스를 조회한다.
    /// 배열에 등록된 SO 의 <c>.name</c> (Unity 에셋명) 을 키로 내부 Dictionary 캐시를 구축한다.
    /// 에셋 위치: <c>Assets/_Project/Resources/EquipmentDatabase.asset</c>
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentDatabase", menuName = "Game/Data/Equipment/EquipmentDatabase")]
    public class EquipmentDatabase : ScriptableObject
    {
        // ── Serialized Fields ──────────────────────────────────────────────────
        [SerializeField] private EquipmentItemData[] _equipment = Array.Empty<EquipmentItemData>();
        [SerializeField] private RuneItemData[]       _runes     = Array.Empty<RuneItemData>();

        // ── Cache (NonSerialized — 런타임 불변 원칙 §7) ───────────────────────
        [NonSerialized] private Dictionary<string, EquipmentItemData> _equipmentCache;
        [NonSerialized] private Dictionary<string, RuneItemData>       _runeCache;

        // ── Unity Lifecycle ────────────────────────────────────────────────────

        private void OnEnable()
        {
            BuildCaches();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// 에셋 이름(<paramref name="assetId"/>) 으로 <see cref="EquipmentItemData"/> 를 조회한다.
        /// 등록되지 않은 ID 이면 <c>null</c> 을 반환한다.
        /// </summary>
        public EquipmentItemData FindEquipment(string assetId)
        {
            if (_equipmentCache == null) BuildCaches();
            if (string.IsNullOrEmpty(assetId)) return null;
            _equipmentCache.TryGetValue(assetId, out var result);
            return result;
        }

        /// <summary>
        /// 에셋 이름(<paramref name="assetId"/>) 으로 <see cref="RuneItemData"/> 를 조회한다.
        /// 등록되지 않은 ID 이면 <c>null</c> 을 반환한다.
        /// </summary>
        public RuneItemData FindRune(string assetId)
        {
            if (_runeCache == null) BuildCaches();
            if (string.IsNullOrEmpty(assetId)) return null;
            _runeCache.TryGetValue(assetId, out var result);
            return result;
        }

        // ── Private ────────────────────────────────────────────────────────────

        private void BuildCaches()
        {
            _equipmentCache = new Dictionary<string, EquipmentItemData>();
            _runeCache      = new Dictionary<string, RuneItemData>();

            if (_equipment != null)
            {
                foreach (var item in _equipment)
                {
                    if (item == null)
                    {
                        Debug.LogWarning("[EquipmentDatabase] _equipment 배열에 null 항목이 있습니다. 스킵합니다.");
                        continue;
                    }
                    if (_equipmentCache.ContainsKey(item.name))
                    {
                        Debug.LogWarning($"[EquipmentDatabase] 중복 EquipmentItemData 이름 '{item.name}' — 첫 번째 등록만 유효합니다.");
                        continue;
                    }
                    _equipmentCache[item.name] = item;
                }
            }

            if (_runes != null)
            {
                foreach (var rune in _runes)
                {
                    if (rune == null)
                    {
                        Debug.LogWarning("[EquipmentDatabase] _runes 배열에 null 항목이 있습니다. 스킵합니다.");
                        continue;
                    }
                    if (_runeCache.ContainsKey(rune.name))
                    {
                        Debug.LogWarning($"[EquipmentDatabase] 중복 RuneItemData 이름 '{rune.name}' — 첫 번째 등록만 유효합니다.");
                        continue;
                    }
                    _runeCache[rune.name] = rune;
                }
            }
        }
    }
}
