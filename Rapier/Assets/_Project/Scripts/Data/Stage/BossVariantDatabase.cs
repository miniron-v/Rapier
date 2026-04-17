using System;
using UnityEngine;
using Game.Data.Equipment;
using Game.Enemies;

namespace Game.Data.Stage
{
    /// <summary>
    /// 보스 slot × tier 매핑 레지스트리 SO.
    ///
    /// slot 0~7  : BALANCE §3-3 보스 순서 (Titan, Specter, Pyromancer, Berserker,
    ///             Stormcaller, Gravekeeper, TwinPhantoms, Titan(임시 8번째))
    /// tier 0~5  : BALANCE §3-1 "차수 1~6" 의 0-based 표현
    ///             tier = min(5, (stageIndex-1) / 8)
    ///
    /// [사용 패턴]
    ///   var entry = bossVariantDb.GetVariant(slot, tier);
    ///   entry.statData   → BossStatData 참조
    ///   entry.dropTable  → DropTableData 참조 (BossStatData.dropTable 대신 이곳을 사용)
    ///
    /// [경로] Assets/_Project/Resources/BossVariantDatabase.asset
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/Stage/BossVariantDatabase", fileName = "BossVariantDatabase")]
    public class BossVariantDatabase : ScriptableObject
    {
        [SerializeField] private BossVariantEntry[] _variants;

        /// <summary>
        /// slot 의 tier 이하 variant 중 tier 가 가장 큰 것을 반환한다.
        /// 해당하는 variant 가 없으면 slot 의 첫 번째 variant 를 반환한다 (fallback).
        /// </summary>
        /// <param name="slot">보스 슬롯 인덱스 (0~7).</param>
        /// <param name="tier">차수 0-based (0~5).</param>
        public BossVariantEntry GetVariant(int slot, int tier)
        {
            if (_variants == null || _variants.Length == 0)
                return null;

            BossVariantEntry best     = null;
            int              bestTier = -1;

            foreach (var v in _variants)
            {
                if (v == null || v.bossSlot != slot) continue;
                if (v.tier <= tier && v.tier > bestTier)
                {
                    best     = v;
                    bestTier = v.tier;
                }
            }

            // fallback: slot 에 속한 tier=0 항목
            if (best == null)
            {
                foreach (var v in _variants)
                {
                    if (v != null && v.bossSlot == slot)
                        return v;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// 보스 슬롯·차수 조합 1개의 데이터.
    /// </summary>
    [Serializable]
    public class BossVariantEntry
    {
        [Tooltip("보스 슬롯 인덱스. 0=Titan, 1=Specter, 2=Pyromancer, 3=Berserker, " +
                 "4=Stormcaller, 5=Gravekeeper, 6=TwinPhantoms, 7=Titan(임시)")]
        public int bossSlot;

        [Tooltip("차수 0-based. 0=1차, 5=6차")]
        public int tier;

        [Tooltip("보스 스탯 SO 참조.")]
        public BossStatData statData;

        [Tooltip("보스 프리팹 참조. ProgressionManager 가 Instantiate 에 사용한다.")]
        public GameObject bossPrefab;

        [Tooltip("이 슬롯의 드롭 테이블. BossStatData.dropTable 대신 여기를 사용한다.")]
        public DropTableData dropTable;
    }
}
