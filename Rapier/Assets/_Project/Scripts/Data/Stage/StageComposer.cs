using UnityEngine;
using Game.Core.Stage;
using Game.Data.Equipment;

namespace Game.Data.Stage
{
    /// <summary>
    /// 키프레임 보간 결과 + BossVariantEntry 를 받아 런타임 StageData 를 조립한다.
    ///
    /// [사용처]
    ///   StageDatabase.GetStage(index) 가 호출하는 전용 정적 헬퍼.
    ///   반환된 StageData 는 HideFlags.DontSave 이므로 에셋 시스템에 등록되지 않는다.
    ///
    /// [등급 드랍률 테이블] BALANCE §3-2 차수별 확률 (tier 0-based = 차수-1):
    ///   tier 0 (1차): N=0.80, R=0.20, E=0.00, U=0.000
    ///   tier 1 (2차): N=0.50, R=0.40, E=0.10, U=0.000
    ///   tier 2 (3차): N=0.25, R=0.45, E=0.30, U=0.005
    ///   tier 3 (4차): N=0.10, R=0.35, E=0.54, U=0.010
    ///   tier 4 (5차): N=0.08, R=0.30, E=0.61, U=0.010
    ///   tier 5 (6차): N=0.05, R=0.25, E=0.68, U=0.020
    /// </summary>
    public static class StageComposer
    {
        // ── 등급 드랍률 테이블 (tier 0~5 × grade 0~3) ───────────────────
        // grade index: 0=Normal, 1=Rare, 2=Epic, 3=Unique
        private static readonly float[,] _gradeRateTable = new float[6, 4]
        {
            // N      R      E      U
            { 0.80f, 0.20f, 0.00f, 0.000f }, // tier 0 (1차): 스테이지 1~8
            { 0.50f, 0.40f, 0.10f, 0.000f }, // tier 1 (2차): 스테이지 9~16
            { 0.25f, 0.45f, 0.30f, 0.005f }, // tier 2 (3차): 스테이지 17~24
            { 0.10f, 0.35f, 0.54f, 0.010f }, // tier 3 (4차): 스테이지 25~32
            { 0.08f, 0.30f, 0.61f, 0.010f }, // tier 4 (5차): 스테이지 33~40
            { 0.05f, 0.25f, 0.68f, 0.020f }, // tier 5 (6차): 스테이지 41~104
        };

        /// <summary>
        /// 런타임 StageData 를 조립하여 반환한다.
        ///
        /// <para>
        /// - hpMultiplier / atkMultiplier : 호출자가 키프레임 보간으로 계산한 값.<br/>
        /// - variant : BossVariantDatabase.GetVariant(slot, tier) 반환값.
        ///   null 이면 방 배열은 비어있고 드랍테이블도 null 인 StageData 를 반환한다.<br/>
        /// - tier : 0-based 차수 (0~5 clamp 적용된 값을 넘길 것).
        /// </para>
        /// </summary>
        /// <param name="stageIndex">1-based 스테이지 번호.</param>
        /// <param name="hpMultiplier">보간된 HP 배율.</param>
        /// <param name="atkMultiplier">보간된 ATK 배율.</param>
        /// <param name="tier">0-based 차수 (0~5).</param>
        /// <param name="variant">BossVariantEntry. null 허용.</param>
        public static StageData Compose(
            int stageIndex, float hpMultiplier, float atkMultiplier,
            int tier, BossVariantEntry variant)
        {
            // 등급 드랍률 배열 생성
            int clampedTier = Mathf.Clamp(tier, 0, 5);
            var gradeDropRates = new GradeDropRate[4];
            for (int g = 0; g < 4; g++)
            {
                gradeDropRates[g] = new GradeDropRate
                {
                    grade    = (EquipmentGrade)g,
                    dropRate = _gradeRateTable[clampedTier, g],
                };
            }

            // 방 배열: variant 가 있으면 BossRoom 1개, 없으면 빈 배열
            RoomNode[] rooms;
            if (variant?.statData != null)
            {
                rooms = new RoomNode[]
                {
                    new RoomNode
                    {
                        roomType     = RoomType.BossRoom,
                        bossPrefab   = variant.bossPrefab, // BossVariantEntry 에서 프리팹 참조
                        bossStatData = variant.statData,
                        displayName  = variant.statData.enemyName,
                    }
                };
            }
            else
            {
                rooms = System.Array.Empty<RoomNode>();
            }

            DropTableData dropTable = variant?.dropTable;

            // in-memory StageData 생성 (DontSave — 에셋 DB 미등록)
            var composed = ScriptableObject.CreateInstance<StageData>();
            composed.hideFlags = HideFlags.DontSave;
            composed.InitForCompose(stageIndex, hpMultiplier, atkMultiplier, rooms, gradeDropRates, dropTable);

            return composed;
        }
    }
}
