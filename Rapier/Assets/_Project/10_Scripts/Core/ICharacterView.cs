namespace Game.Characters
{
    /// <summary>
    /// Presenter가 View에 요청하는 계약 인터페이스.
    /// View는 이 인터페이스를 구현하며, 시각 연출만 담당한다.
    ///
    /// [이동 책임 분리]
    ///   위치 계산은 Presenter가 수행하고, View.SetPosition()으로 즉시 반영한다.
    ///   MoveTo()는 제거됨 — View 내부 Lerp가 Presenter 이동 계산을 방해하기 때문.
    /// </summary>
    public interface ICharacterView
    {
        /// <summary>
        /// 캐릭터를 월드 좌표로 즉시 이동시킨다.
        /// Presenter가 계산한 위치를 View가 transform에 직접 반영한다.
        /// Lerp 없음 — 부드러운 이동은 Presenter가 매 프레임 작은 delta로 호출해 구현한다.
        /// </summary>
        void SetPosition(UnityEngine.Vector2 position);

        /// <summary>공격 애니메이션 재생.</summary>
        void PlayAttack();

        /// <summary>피격 플래시.</summary>
        void PlayHit();

        /// <summary>회피 애니메이션 재생.</summary>
        void PlayDodge(UnityEngine.Vector2 direction);

        /// <summary>스킬 차지 게이지 갱신 (0~1).</summary>
        void UpdateChargeGauge(float ratio);

        /// <summary>HP 게이지 갱신 (0~1).</summary>
        void UpdateHpGauge(float ratio);

        /// <summary>사망 처리.</summary>
        void PlayDeath();

        /// <summary>스프라이트 설정.</summary>
        void SetSprite(UnityEngine.Sprite sprite);
    }
}
