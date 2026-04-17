using System.Collections.Generic;
using Game.Data.Equipment;
using Game.Data.Stage;

namespace Game.Core.Stage
{
    /// <summary>
    /// 런타임에 조립된 한 스테이지의 컨텍스트. POCO (ScriptableObject 아님).
    ///
    /// StageDatabase.GetStage(index) 가 반환하며, StageManager · BossDeathSequencer 가 소비한다.
    /// 키프레임 SO 값은 읽기 전용이며, 본 POCO 는 그 값을 참조만 한다.
    ///
    /// SO 는 공유·읽기 전용 데이터 정의가 본질이므로, 스테이지마다 달라지는
    /// HP/ATK 배율·방 배열·드랍률 같은 런타임 합성 결과는 이 클래스에 담는다 (CLAUDE.md §7).
    /// </summary>
    public sealed class StageContext
    {
        /// <summary>1-based 스테이지 번호.</summary>
        public int StageIndex { get; }

        /// <summary>0-based 차수 (0~5). BALANCE §3-1.</summary>
        public int Tier { get; }

        /// <summary>0-based 보스 슬롯 (0~7). BALANCE §3-1.</summary>
        public int BossSlot { get; }

        /// <summary>보스 기본 HP에 곱하는 배율. 키프레임 로그 보간 결과.</summary>
        public float HpMultiplier { get; }

        /// <summary>보스 기본 ATK에 곱하는 배율. 키프레임 로그 보간 결과.</summary>
        public float AtkMultiplier { get; }

        /// <summary>방 배열 (BossRoom 1개 구성). 읽기 전용.</summary>
        public IReadOnlyList<RoomNode> Rooms { get; }

        /// <summary>차수별 등급 드랍률 목록. BALANCE §3-2.</summary>
        public IReadOnlyList<GradeDropRate> GradeDropRates { get; }

        /// <summary>보스 드랍 테이블 참조. BossVariantEntry.dropTable 에서 가져온다.</summary>
        public DropTableData DropTable { get; }

        /// <summary>스테이지 표시 이름 (예: "Stage 1").</summary>
        public string StageName => $"Stage {StageIndex}";

        public StageContext(
            int stageIndex, int tier, int bossSlot,
            float hpMul, float atkMul,
            IReadOnlyList<RoomNode> rooms,
            IReadOnlyList<GradeDropRate> gradeDropRates,
            DropTableData dropTable)
        {
            StageIndex     = stageIndex;
            Tier           = tier;
            BossSlot       = bossSlot;
            HpMultiplier   = hpMul;
            AtkMultiplier  = atkMul;
            Rooms          = rooms          ?? System.Array.Empty<RoomNode>();
            GradeDropRates = gradeDropRates ?? System.Array.Empty<GradeDropRate>();
            DropTable      = dropTable;
        }
    }
}
