namespace Game.Input
{
    /// <summary>
    /// 현재 프레임의 입력 상태를 나타내는 enum.
    /// GestureRecognizer가 판별하여 C# event로 발행한다.
    /// </summary>
    public enum InputState
    {
        None,       // 입력 없음
        Drag,       // 이동 (거리 >= 20px, 지속 >= 0.25초)
        Tap,        // 공격 (거리 < 20px, 지속 < 0.2초)
        Swipe,      // 회피 (거리 >= 60px, 지속 < 0.25초)
        Hold,       // 차지 (정지 상태, 지속 >= 0.3초)
        JustDodge,  // 저스트 회피 (적 공격 타이밍에 정확히 Swipe)
    }
}
