using System;
using UnityEngine;

namespace Game.Data.Equipment
{
    /// <summary>
    /// 런타임 장비 인스턴스. SO 정의(EquipmentItemData)를 참조하고,
    /// 룬 장착 상태를 런타임에 관리한다.
    /// </summary>
    [Serializable]
    public class EquipmentInstance
    {
        [SerializeField] private string _instanceId;
        [SerializeField] private EquipmentItemData _data;

        // 룬 슬롯은 SO 등급에서 결정된 개수만큼 허용. null = 비어있음.
        [NonSerialized] private RuneItemData[] _equippedRunes;

        /// <summary>인스턴스 고유 ID</summary>
        public string InstanceId => _instanceId;
        /// <summary>장비 SO 정의</summary>
        public EquipmentItemData Data => _data;
        /// <summary>현재 장착된 룬 배열 (인덱스 = 소켓 번호)</summary>
        public RuneItemData[] EquippedRunes => _equippedRunes;

        /// <summary>새 장비 인스턴스를 생성한다.</summary>
        public EquipmentInstance(EquipmentItemData data)
        {
            _instanceId    = Guid.NewGuid().ToString();
            _data          = data;
            _equippedRunes = new RuneItemData[data.RuneSocketCount];
        }

        /// <summary>지정 소켓에 룬을 장착한다. 범위 초과 시 false 반환.</summary>
        public bool EquipRune(int socketIndex, RuneItemData rune)
        {
            if (socketIndex < 0 || socketIndex >= _equippedRunes.Length)
            {
                Debug.LogWarning($"[EquipmentInstance] 소켓 인덱스 {socketIndex} 범위 초과 (최대 {_equippedRunes.Length - 1})");
                return false;
            }
            _equippedRunes[socketIndex] = rune;
            return true;
        }

        /// <summary>지정 소켓의 룬을 해제한다.</summary>
        public bool UnequipRune(int socketIndex)
        {
            if (socketIndex < 0 || socketIndex >= _equippedRunes.Length)
            {
                Debug.LogWarning($"[EquipmentInstance] 소켓 인덱스 {socketIndex} 범위 초과");
                return false;
            }
            _equippedRunes[socketIndex] = null;
            return true;
        }

        /// <summary>런타임 복원 시 룬 슬롯 배열을 초기화한다. (저장 로드 후 호출)</summary>
        public void InitRunes()
        {
            _equippedRunes = new RuneItemData[_data != null ? _data.RuneSocketCount : 0];
        }
    }
}
