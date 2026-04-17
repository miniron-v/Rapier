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
            // BALANCE §2-1 / EQUIPMENT.md §2 : Normal=0 / Rare=1 / Epic=2 / Unique=3
            // 실제 드랍 롤 로직(EquipmentInstance)도 (int)grade 로 0/1/2/3 사용.
            return grade switch
            {
                EquipmentGrade.Normal => 0,
                EquipmentGrade.Rare   => 1,
                EquipmentGrade.Epic   => 2,
                EquipmentGrade.Unique => 3,
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

        /// <summary>등급에 대응하는 UnityEngine.Color를 반환한다.</summary>
        public static UnityEngine.Color GetGradeColor(EquipmentGrade grade)
        {
            return UnityEngine.ColorUtility.TryParseHtmlString(GetGradeColorHex(grade), out var c)
                ? c
                : UnityEngine.Color.white;
        }

        /// <summary>
        /// 등급에 대응하는 최대 강화 단계를 반환한다.
        /// Normal=6, Rare=9, Epic=12, Unique=15
        /// </summary>
        public static int GetMaxEnhance(EquipmentGrade grade)
        {
            return grade switch
            {
                EquipmentGrade.Normal => 6,
                EquipmentGrade.Rare   => 9,
                EquipmentGrade.Epic   => 12,
                EquipmentGrade.Unique => 15,
                _                    => 6
            };
        }

        /// <summary>
        /// 강화 레벨 기준 메인스탯 배율을 반환한다 (BALANCE §6-1 구간별 가속 공식).
        /// level=0 → 1.0, level=15 → 4.48. 음수는 1.0.
        ///
        /// 구간별 단계당 증가:
        ///   +1~+3  : +0.08  (누적 1.00 → 1.24)
        ///   +4~+6  : +0.12  (누적 1.24 → 1.60)
        ///   +7~+9  : +0.18  (누적 1.60 → 2.14)
        ///   +10~+12: +0.28  (누적 2.14 → 2.98)
        ///   +13~+15: +0.50  (누적 2.98 → 4.48)
        ///
        /// 검증: GetEnhanceMultiplier(0)=1.00 / (3)=1.24 / (6)=1.60 / (9)=2.14 / (12)=2.98 / (15)=4.48
        /// </summary>
        public static float GetEnhanceMultiplier(int level)
        {
            if (level <= 0) return 1.0f;

            // 각 구간: [stepEnd, stepInc]
            // +1~+3: +0.08 / +4~+6: +0.12 / +7~+9: +0.18 / +10~+12: +0.28 / +13~+15: +0.50
            int[]   stepEnd = {  3,     6,     9,    12,    15 };
            float[] stepInc = { 0.08f, 0.12f, 0.18f, 0.28f, 0.50f };

            float m         = 1.0f;
            int   prev      = 0;
            int   remaining = level;

            for (int i = 0; i < stepEnd.Length && remaining > 0; i++)
            {
                int bucket = stepEnd[i] - prev;
                int take   = UnityEngine.Mathf.Min(remaining, bucket);
                m         += take * stepInc[i];
                remaining -= take;
                prev       = stepEnd[i];
            }

            return m;
        }
    }
}
