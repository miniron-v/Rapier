using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 캐릭터 기본 스탯 데이터.
    /// ScriptableObject로 분리해 런타임 로직과 독립적으로 관리한다.
    /// 경로: Assets/_Project/ScriptableObjects/Characters/
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterStatData",
        menuName  = "Rapier/Characters/CharacterStatData")]
    public class CharacterStatData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("인게임 캐릭터 스프라이트 (SpriteRenderer 용).")]
        public UnityEngine.Sprite sprite;

        [Tooltip("캐릭터 선택창·인벤토리 상단에 표시할 일러스트 스프라이트 (UI Image 용).")]
        public UnityEngine.Sprite illustSprite;

        public string characterName = "Unknown";

        [TextArea(2, 4)]
        [Tooltip("캐릭터 설명 텍스트. 캐릭터 변경 모달 하단에 표시된다.")]
        public string description = "";

        [Header("전투 스탯")]
        [Min(1)] public float maxHp       = 500f;
        [Min(0)] public float attackPower = 50f;
        [Min(0)] public float moveSpeed   = 5f;

        [Header("회피")]
        [Min(0)] public float dodgeInvincibleDuration = 0.2f;
        [Tooltip("회피 쿨늤운 (초). 이 시간이 지나야 다시 회피 가능.")]
        [Min(0)] public float dodgeCooldown = 2f;


        [Header("대시 (Swipe 회피)")]
        [Tooltip("대시 이동 거리 (월드 단위)")]
        [Min(0)] public float dashDistance = 4f;
        [Tooltip("대시 속도 (시작부터 끝까지, 높을수록 빠름)")]
        [Min(0)] public float dashSpeed    = 20f;


        [Header("스킬 차지")]
        [Min(0)] public float chargeRequiredTime = 0.3f;

        [Header("데미지 배율")]
        [Tooltip("일반 공격 데미지 배율 (%). 100 = ×1.0, 150 = ×1.5")]
        public int normalAttackPercent = 100;

        [Header("공격 범위 (사각형)")]
        [Tooltip("공격 범위 가로 (월드 단위)")]
        [Min(0)] public float attackWidth  = 2.0f;
        [Tooltip("공격 범위 세로 (월드 단위)")]
        [Min(0)] public float attackHeight = 1.5f;
        [Tooltip("공격 범위 중심을 앞으로 얼마나 밀지 (월드 단위)")]
        [Min(0)] public float attackOffset = 1.0f;
    }
}
