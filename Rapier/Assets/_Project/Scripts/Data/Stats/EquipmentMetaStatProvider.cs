using Game.Data.Equipment;
using UnityEngine;

namespace Game.Data.MetaStats
{
    /// <summary>
    /// <see cref="EquipmentManager"/> 의 장착 상태를 읽어 <see cref="MetaStatContainer"/> 를 구성하는 Provider.
    /// EQUIPMENT.md §4 파이프라인 기준.
    ///
    /// 룬 처리 규칙:
    ///   - targetCharacterId 가 빈 문자열 → 공통 룬, 모든 캐릭터에 적용
    ///   - targetCharacterId 가 일치하는 경우 → 캐릭터 전용 룬, 해당 캐릭터에만 적용
    ///   - 불일치 → 필터링(건너뜀)
    /// </summary>
    public sealed class EquipmentMetaStatProvider
    {
        private readonly EquipmentManager _equipmentManager;

        /// <summary>장비 관리자를 주입한다.</summary>
        public EquipmentMetaStatProvider(EquipmentManager equipmentManager)
        {
            _equipmentManager = equipmentManager;
        }

        /// <summary>
        /// <paramref name="characterId"/> 의 현재 장착 상태를 기반으로
        /// <see cref="MetaStatContainer"/> 를 빌드하여 반환한다.
        /// </summary>
        public MetaStatContainer BuildContainer(string characterId)
        {
            var container = new MetaStatContainer();

            if (_equipmentManager == null)
            {
                Debug.LogWarning("[EquipmentMetaStatProvider] EquipmentManager 가 null — 빈 컨테이너 반환");
                return container;
            }

            var set = _equipmentManager.GetCharacterSet(characterId);
            if (set == null) return container;

            foreach (var (_, instance) in set.GetAllEquipped())
            {
                if (instance?.Data == null) continue;

                // 메인 스탯 누산
                container.Apply(instance.Data.MainStat);

                // 서브 스탯 누산
                var subStats = instance.Data.SubStats;
                if (subStats != null)
                {
                    foreach (var sub in subStats)
                        container.Apply(sub);
                }

                // 룬 스탯 누산 (캐릭터 필터링 적용)
                var runes = instance.EquippedRunes;
                if (runes == null) continue;

                foreach (var rune in runes)
                {
                    if (rune == null) continue;
                    if (!rune.IsApplicableTo(characterId)) continue;
                    container.Apply(rune.StatEffect);
                }
            }

            return container;
        }
    }
}
