namespace Game.Characters
{
    /// <summary>
    /// Presenter가 View에 요청하는 계약 인터페이스.
    /// View는 이 인터페이스를 구현하며, 로직을 포함하지 않는다.
    /// </summary>
    public interface ICharacterView
    {
        /// <summary>월드 좌표 이동.</summary>
        void MoveTo(UnityEngine.Vector2 position);

        /// <summary>공격 애니메이션 재생.</summary>
    void PlayAttack();
    void PlayHit();     // 피격 플래시

        /// <summary>회피 애니메이션 재생.</summary>
        void PlayDodge(UnityEngine.Vector2 direction);

        /// <summary>스킬 차지 게이지 갱신 (0~1).</summary>
        void UpdateChargeGauge(float ratio);

        /// <summary>HP 게이지 갱신 (0~1).</summary>
        void UpdateHpGauge(float ratio);

        /// <summary>사망 처리.</summary>
        void PlayDeath();
    }
}
