using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 런타임 상태. 순수 C# 클래스.
    /// EnemyPresenter가 생성하고 소유한다.
    ///
    /// [스테이지 스케일링]
    ///   <see cref="ScaleHp"/> 호출 시 EffectiveMaxHp 와 CurrentHp 모두 배율 적용.
    ///   TakeDamage/Reset 에서 HP 비율 계산 기준은 EffectiveMaxHp.
    /// </summary>
    public class EnemyModel
    {
        public EnemyStatData StatData       { get; }
        public float         CurrentHp      { get; private set; }
        public bool          IsAlive        => CurrentHp > 0f;

        /// <summary>
        /// 런타임 최대 HP. 기본값 = StatData.maxHp.
        /// ScaleHp() 호출 후 배율이 반영된다.
        /// HP 바 비율 계산 기준.
        /// </summary>
        [NonSerialized] public float EffectiveMaxHp;

        public event Action<float> OnHpChanged; // 0~1 비율
        public event Action        OnDeath;

        public EnemyModel(EnemyStatData statData)
        {
            StatData       = statData;
            EffectiveMaxHp = statData.maxHp;
            CurrentHp      = EffectiveMaxHp;
        }

        /// <summary>
        /// 스테이지 배율을 곱해 런타임 최대 HP와 현재 HP를 스케일한다.
        /// Spawn() 직후 ApplyStageMultipliers() 에서 1회 호출.
        /// </summary>
        /// <param name="multiplier">HP 배율.</param>
        public void ScaleHp(float multiplier)
        {
            if (multiplier <= 0f) return;
            EffectiveMaxHp = StatData.maxHp * multiplier;
            CurrentHp      = EffectiveMaxHp;
            OnHpChanged?.Invoke(1f);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            CurrentHp = Mathf.Max(0f, CurrentHp - amount);
            float ratio = EffectiveMaxHp > 0f ? CurrentHp / EffectiveMaxHp : 0f;
            OnHpChanged?.Invoke(ratio);
            if (!IsAlive) OnDeath?.Invoke();
        }

        public void Reset()
        {
            CurrentHp = EffectiveMaxHp;
        }
    }
}
