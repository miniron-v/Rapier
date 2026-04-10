using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 장비 아이템 정의 ScriptableObject.
    /// 슬롯 타입, 등급, 메인 스탯, 서브 스탯 목록을 보유한다.
    /// SO 값은 런타임 불변. 외부에는 읽기 전용 프로퍼티로만 노출.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EquipmentItemData",
        menuName  = "Game/Data/Equipment/EquipmentItemData")]
    public class EquipmentItemData : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string _itemName = "장비 이름";
        [SerializeField] [TextArea] private string _description = "";
        [SerializeField] private Sprite _icon;

        [Header("장비 분류")]
        [SerializeField] private EquipmentSlotType _slotType;
        [SerializeField] private EquipmentGrade _grade;

        [Header("능력치")]
        [Tooltip("슬롯 카테고리에 따라 고정된 메인 스탯")]
        [SerializeField] private StatEntry _mainStat;

        [Tooltip("등급에 따른 서브 스탯 목록 (노말=1, 레어=2, 에픽=3, 유니크=4)")]
        [SerializeField] private StatEntry[] _subStats = System.Array.Empty<StatEntry>();

        // ── 읽기 전용 프로퍼티 ──────────────────────────────────────────────
        /// <summary>아이템 이름</summary>
        public string ItemName    => _itemName;
        /// <summary>설명 텍스트</summary>
        public string Description => _description;
        /// <summary>아이콘 스프라이트</summary>
        public Sprite Icon        => _icon;
        /// <summary>장비 슬롯 종류</summary>
        public EquipmentSlotType SlotType => _slotType;
        /// <summary>장비 등급</summary>
        public EquipmentGrade Grade       => _grade;
        /// <summary>메인 스탯</summary>
        public StatEntry MainStat         => _mainStat;
        /// <summary>서브 스탯 배열 (읽기 전용 스냅샷)</summary>
        public StatEntry[] SubStats       => (StatEntry[])_subStats.Clone();

        /// <summary>이 등급에서 허용되는 룬 소켓 수를 반환한다.</summary>
        public int RuneSocketCount => EquipmentGradeHelper.GetRuneSocketCount(_grade);

        /// <summary>이 등급에서 허용되는 서브 스탯 슬롯 수를 반환한다.</summary>
        public int SubStatSlotCount => EquipmentGradeHelper.GetSubStatSlotCount(_grade);
    }
}
