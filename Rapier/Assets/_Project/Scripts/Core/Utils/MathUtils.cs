using UnityEngine;

namespace Game.Core.Utils
{
    /// <summary>
    /// 프로젝트 공용 수학 유틸리티.
    /// UnityEngine.Mathf / System.Math 와 이름 충돌 없이 Game.Core.Utils 네임스페이스로 격리.
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// 0.5 이상을 항상 올림하는 반올림 (표준 사사오입).
        /// UnityEngine.Mathf.Round 는 banker's rounding(0.5 → 짝수)을 사용하므로 이 메서드로 대체.
        /// HP, ATK, 데미지 등 정수 표기가 필요한 전역 계산에 사용.
        /// </summary>
        public static float RoundHalfUp(float value)
            => Mathf.Floor(value + 0.5f);
    }
}
