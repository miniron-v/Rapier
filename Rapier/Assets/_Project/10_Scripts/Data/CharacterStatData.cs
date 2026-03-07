using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 캐릭터 기본 스탯 데이터.
    /// ScriptableObject로 분리해 런타임 로직과 독립적으로 관리한다.
    /// 경로: Assets/_Project/30_ScriptableObjects/Characters/
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterStatData",
        menuName  = "Rapier/Characters/CharacterStatData")]
    public class CharacterStatData : ScriptableObject
    {
        [Header("기본 정보")]
        public string characterName = "Unknown";

        [Header("전투 스탯")]
        [Min(1)] public float maxHp       = 500f;
        [Min(0)] public float attackPower = 50f;
        [Min(0)] public float moveSpeed   = 5f;

        [Header("회피")]
        [Min(0)] public float dodgeInvincibleDuration = 0.2f; // 무적 시간(초)

        [Header("스킬 차지")]
        [Min(0)] public float chargeRequiredTime = 0.3f; // 완충까지 필요한 Hold 시간(초)
    }
}
