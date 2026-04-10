namespace Game.Data.Equipment
{
    /// <summary>
    /// 등급별 파생 수치를 한 곳에서 계산한다. OCP — 등급 추가 시 이 파일만 수정.
    /// </summary>
    public static class EquipmentGradeHelper
    {
        /// <summary>등급에 대응하는 룬 소켓 수를 반환한다.</summary>
        public static int GetRuneSocketCount(EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Normal => 1,
                EquipmentGrade.Rare   => 1,
                EquipmentGrade.Epic   => 2,
                EquipmentGrade.Unique => 3,
                _                    => 0
            };
        }

        /// <summary>등급에 대응하는 서브 스탯 슬롯 수를 반환한다.</summary>
        public static int GetSubStatSlotCount(EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Normal => 1,
                EquipmentGrade.Rare   => 2,
                EquipmentGrade.Epic   => 3,
                EquipmentGrade.Unique => 4,
                _                    => 0
            };
        }

        /// <summary>등급에 대응하는 UI 색상 코드(HEX)를 반환한다.</summary>
        public static string GetGradeColorHex(EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Normal => "#AAAAAA",  // 회색
                EquipmentGrade.Rare   => "#4A90E2",  // 파랑
                EquipmentGrade.Epic   => "#9B59B6",  // 보라
                EquipmentGrade.Unique => "#E67E22",  // 주황
                _                    => "#FFFFFF"
            };
        }
    }
}
