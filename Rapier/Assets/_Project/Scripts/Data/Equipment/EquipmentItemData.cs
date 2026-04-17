using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 장비 아이템 정의 ScriptableObject.
    /// 슬롯 타입, 등급, 메인 스탯, 서브 스탯 풀 참조를 보유한다.
    /// SO 값은 런타임 불변. 외부에는 읽기 전용 프로퍼티로만 노출.
    ///
    /// Phase 22-B:
    ///   - _subStats 제거 → _subStatPool (SubStatPoolData) 으로 교체.
    ///   - 장신구(Necklace/Ring)는 _mainStatPool (MainStatPoolData) 추가.
    ///     장신구 슬롯에서는 _mainStat 이 더 이상 사용되지 않는다.
    ///     런타임 우선순위: RolledMainStat (EquipmentInstance) → SO._mainStat (fallback).
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
        [Tooltip("무기/방어구의 고정 메인 스탯. 장신구 슬롯(Necklace/Ring)에서는 미사용 — MainStatPool 에서 드롭 시 롤.")]
        [SerializeField] public StatEntry _mainStat;

        [Tooltip("서브스탯 풀 SO. 모든 슬롯 필수. 드롭 시 등급에 따라 N개 추첨.")]
        [SerializeField] private SubStatPoolData _subStatPool;

        [Tooltip("장신구(Necklace/Ring) 전용 메인스탯 풀 SO. 드롭 시 1회 추첨.")]
        [SerializeField] private MainStatPoolData _mainStatPool;

        [Header("드랍 분류")]
        [Tooltip("보스 전담 드랍 장비 여부. 보스 드랍 풀에 포함되는 장비는 true. 공용 장비(가챠 전용)는 false.")]
        [SerializeField] private bool _isBossItem = false;

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
        /// <summary>
        /// 고정 메인 스탯. 무기/방어구에서 사용.
        /// 장신구(Necklace/Ring)는 EquipmentInstance.RolledMainStat 을 우선 사용한다.
        /// </summary>
        public StatEntry MainStat         => _mainStat;
        /// <summary>서브스탯 풀 (드롭 시 추첨에 사용)</summary>
        public SubStatPoolData SubStatPool => _subStatPool;
        /// <summary>장신구 메인스탯 풀 (Necklace/Ring 전용. 그 외 null)</summary>
        public MainStatPoolData MainStatPool => _mainStatPool;

        /// <summary>
        /// 보스 전담 드랍 장비 여부.
        /// true = 보스 드랍 풀 전용 (BALANCE §2-2 메인스탯 × 1.2 배율 적용 대상 — Phase E).
        /// false = 공용 장비 (가챠 전용 경로).
        /// </summary>
        public bool IsBossItem => _isBossItem;

        /// <summary>이 등급에서 허용되는 룬 소켓 수를 반환한다.</summary>
        public int RuneSocketCount => EquipmentGradeHelper.GetRuneSocketCount(_grade);

        /// <summary>이 등급에서 허용되는 서브 스탯 슬롯 수를 반환한다.</summary>
        public int SubStatSlotCount => EquipmentGradeHelper.GetSubStatSlotCount(_grade);
    }
}
