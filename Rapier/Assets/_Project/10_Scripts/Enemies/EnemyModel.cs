using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 런타임 상태. 순수 C# 클래스.
    /// EnemyPresenter가 생성하고 소유한다.
    /// </summary>
    public class EnemyModel
    {
        public EnemyStatData StatData { get; }
        public float CurrentHp        { get; private set; }
        public bool  IsAlive          => CurrentHp > 0f;

        public event System.Action<float> OnHpChanged; // 0~1 비율
        public event System.Action        OnDeath;

        public EnemyModel(EnemyStatData statData)
        {
            StatData  = statData;
            CurrentHp = statData.maxHp;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
            OnHpChanged?.Invoke(CurrentHp / StatData.maxHp);
            if (!IsAlive) OnDeath?.Invoke();
        }

        public void Reset()
        {
            CurrentHp = StatData.maxHp;
        }
    }
}
