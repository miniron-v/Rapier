namespace Game.Combat
{
    /// <summary>
    /// 피해를 받을 수 있는 모든 객체의 계약.
    /// 플레이어, 적 모두 구현한다.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, UnityEngine.Vector2 knockbackDir);
    }
}
