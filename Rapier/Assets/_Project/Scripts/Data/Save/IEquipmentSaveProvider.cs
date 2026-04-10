using System.Collections.Generic;

namespace Game.Data.Save
{
    /// <summary>
    /// 장비 저장/로드 계약 인터페이스.
    /// B2의 동일 스텁과 이름·시그니처를 맞춘다.
    /// 장비 시스템 구현체(B2 영역)가 이를 구현한다.
    /// </summary>
    public interface IEquipmentSaveProvider
    {
        /// <summary>현재 보유 장비 목록을 직렬화 가능한 형태로 반환한다.</summary>
        List<EquipmentSaveEntry> SerializeOwnedEquipment();

        /// <summary>캐릭터별 장착 상태를 직렬화 가능한 형태로 반환한다.</summary>
        Dictionary<string, List<string>> SerializeEquippedMap();

        /// <summary>저장 데이터에서 장비 목록을 역직렬화하여 복원한다.</summary>
        void DeserializeOwnedEquipment(List<EquipmentSaveEntry> entries);

        /// <summary>저장 데이터에서 장착 상태를 역직렬화하여 복원한다.</summary>
        void DeserializeEquippedMap(Dictionary<string, List<string>> map);
    }
}
