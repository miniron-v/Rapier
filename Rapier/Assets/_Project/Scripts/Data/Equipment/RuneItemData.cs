using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 룬 아이템 정의 ScriptableObject.
    /// 룬 종류, 효과 설명, 대상 캐릭터 ID를 보유한다.
    /// characterId가 빈 문자열이면 모든 캐릭터에 적용 가능.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RuneItemData",
        menuName  = "Game/Data/Equipment/RuneItemData")]
    public class RuneItemData : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string _runeName = "룬 이름";
        [SerializeField] [TextArea] private string _effectDescription = "";
        [SerializeField] private Sprite _icon;

        [Header("적용 대상")]
        [Tooltip("효과를 받을 캐릭터 ID. 비워두면 모든 캐릭터에 적용.")]
        [SerializeField] private string _targetCharacterId = "";

        [Header("능력치 효과")]
        [Tooltip("룬이 부여하는 스탯 (공통 스탯인 경우)")]
        [SerializeField] private StatEntry _statEffect;

        // ── 읽기 전용 프로퍼티 ──────────────────────────────────────────────
        /// <summary>룬 이름</summary>
        public string RuneName         => _runeName;
        /// <summary>효과 설명 텍스트</summary>
        public string EffectDescription => _effectDescription;
        /// <summary>아이콘 스프라이트</summary>
        public Sprite Icon             => _icon;
        /// <summary>효과 적용 대상 캐릭터 ID. 빈 문자열이면 전체 공통.</summary>
        public string TargetCharacterId => _targetCharacterId;
        /// <summary>공통 스탯 효과</summary>
        public StatEntry StatEffect    => _statEffect;

        /// <summary>주어진 캐릭터 ID에 이 룬이 효과를 적용할 수 있는지 반환한다.</summary>
        public bool IsApplicableTo(string characterId)
        {
            return string.IsNullOrEmpty(_targetCharacterId)
                || _targetCharacterId == characterId;
        }
    }
}
