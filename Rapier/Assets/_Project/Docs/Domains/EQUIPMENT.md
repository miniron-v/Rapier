# 장비 시스템 (Equipment)

장비, 등급, 룬, 인벤토리, 캐릭터별 장착 관리.

---

## 1. 장비 슬롯 (8 슬롯)

| 카테고리 | 슬롯 | 메인 능력치 |
|---------|------|------------|
| 무기 | 무기 | 공격력 |
| 방어구 | 모자 | 쿨타임 감소 |
| 방어구 | 상의 | HP |
| 방어구 | 하의 | HP |
| 방어구 | 신발 | 이동속도, HP |
| 방어구 | 장갑 | 크리티컬 데미지 |
| 장신구 | 목걸이 | (랜덤 풀: 쿨감/스킬뎀/크리확) |
| 장신구 | 반지 | (랜덤 풀: 쿨감/스킬뎀/크리확) |

### 캐릭터-장비 관계

- **캐릭터별 장착 상태**: 각 캐릭터가 자신의 8슬롯 장착 정보를 보유
- **착용 제한 없음**: 동일 장비를 다른 캐릭터에도 장착 가능 (자유 이동)
- 한 장비 인스턴스는 동시에 1 캐릭터만 장착 (이동 시 이전 캐릭터에서 자동 해제)

---

## 2. 등급 / 룬 소켓

| 등급 | 색상 | 룬 소켓 수 | 서브 스탯 슬롯 수 |
|------|------|-----------|-----------------|
| 노말 | 회색 | 1 | 1 |
| 레어 | 파랑 | 1 | 2 |
| 에픽 | 보라 | 2 | 3 |
| 유니크 | 주황 | 3 | 4 |

### 메인 스탯 vs 서브 스탯

- **메인 스탯**: 장비 카테고리에 따라 고정 (위 1번 표 참조). 등급에 따라 수치만 다름.
- **서브 스탯**: 정해진 풀에서 랜덤 부여. 깡 또는 % 형태.
- 서브 스탯 풀의 정확한 구성은 데이터 작업 시점에 결정 (장비 카테고리별로 풀이 다름).

---

## 3. 룬 시스템

룬은 캐릭터의 **공통 스탯과 별개**로 캐릭터 고유 메커니즘을 강화한다.

| 룬 종류 (예시) | 효과 |
|---------------|------|
| 표식 룬 | (Rapier) 표식 최대 중첩 +1 |
| 분쇄 룬 | (Warrior) 대지 분쇄 범위 +20% |
| 잔상 룬 | (Assassin) 잔상 지속 시간 +30% |
| 폭발 룬 | (Ranger) 강화 폭발 화살 데미지 +50% |
| 기본 쿨감 룬 | 차지 스킬 쿨다운 -15% (모든 캐릭터) |
| 회피 룬 | 회피 쿨다운 -20% (모든 캐릭터) |

### 룬 장착 규칙

- 룬은 장비의 룬 소켓에 장착
- 자유롭게 장착/해제 가능
- 캐릭터 고유 룬은 해당 캐릭터에만 효과 적용 (다른 캐릭터가 장착 시 효과 없음 또는 비활성)
- 룬은 장비와 별도의 인벤토리로 관리

---

## 4. 데이터 구조

| 객체 | 책임 |
|------|------|
| `EquipmentData` (SO) | 장비 SO 정의 (카테고리, 메인 스탯, 등급, 서브 스탯 풀 참조) |
| `EquipmentInstance` (런타임) | 실제 보유 장비 인스턴스. 서브 스탯 롤 결과, 룬 장착 상태 보유 |
| `RuneData` (SO) | 룬 정의 (효과, 적용 캐릭터) |
| `RuneInstance` (런타임) | 실제 보유 룬 인스턴스 |
| `EquipmentInventory` | 모든 보유 장비/룬 인스턴스 관리 |
| `CharacterEquipment` | 캐릭터별 8 슬롯 장착 상태 |

### 능력치 적용 (Phase 13-B 파이프라인)

장비/룬의 능력치는 모두 **MetaStat** 으로 분류되어 단일 경로로 캐릭터에 적용된다 (`STATS.md` 참조).

```
EquipmentManager (장착 상태)
   │ OnEquipped / OnUnequipped
   ▼
EquipmentMetaStatProvider  : IMetaStatProvider
   │ 장착 장비 8슬롯의 MainStat + SubStats + 각 소켓의 룬 StatEffect 를 순회
   │ StatEntry 를 누적하여 MetaStatContainer 구성
   ▼
MetaStatContainer
   │ (base + meta_flat) × (1 + meta%) 계산
   ▼
CharacterPresenterBase.Init(statData, view)
   │ 진입 시 ServiceLocator.Get<EquipmentManager>() 조회
   │ Provider.BuildContainer() → CharacterModel 의 최종 스탯에 주입
   ▼
CharacterModel (maxHp / atk / moveSpeed = 최종 스탯)
```

**씬 간 보존**: `EquipmentManager` 는 `DontDestroyOnLoad` + `ServiceLocator.Register(this)` 로 로비에서 인게임 씬으로 전달된다. 씬 전환 중 장착 상태 유실 없음.

**런타임 재계산**: 스테이지 중 장비 변경은 없으므로 `Init` 시점 1회 계산이면 충분. 로비 내 장착 변경 시에는 View 측 미리보기용으로만 재계산 이벤트 발행.

**룬 처리**: 룬의 `StatEffect` (StatEntry) 는 장비와 동일 파이프라인으로 합산. 단, 캐릭터 전용 룬(`_targetCharacterId` 불일치)은 Provider 단계에서 필터링.

---

## 5. 프로토타입 단계 데이터

가챠/드롭 시스템은 후순위. 우선 **사전 정의된 SO 몇 개**를 디버그 메뉴 또는 초기 인벤토리로 제공한다.

권장 디버그 데이터:
- 노말~유니크 등급 각 카테고리당 1~2개씩
- 룬 5~6종 (Rapier 고유 + 공통)
- 디버그 메뉴: `Rapier/Dev/Add Debug Equipment`, `Rapier/Dev/Add Debug Runes`

---

## 6. UI

장비 관리는 로비 **탭 2 (캐릭터 관리)** 의 장비 영역에서 이뤄진다. 자세한 UI 구성은 `UI.md` + `Rapier_Prototype_DesignDoc.md §9-2` 참조.

---

## 7. 저장 / 복원 파이프라인 (Phase 14)

Phase 13-B 에서 Serialize 경로가 완성되었으나 Deserialize 는 TODO 스텁이었다. Phase 14 에서 완전한 왕복(save → 종료 → 재시작 → load) 을 구현한다.

### 7-1. EquipmentDatabase (신규 SO 레지스트리)

- **목적**: `dataAssetId` / `runeAssetId` (= SO 에셋명) 로부터 런타임 SO 레퍼런스를 조회.
- **형태**: `EquipmentDatabase` (ScriptableObject). 필드:
  - `_equipment : EquipmentItemData[]`
  - `_runes : RuneItemData[]`
- **조회 API**:
  - `EquipmentItemData FindEquipment(string assetId)` — 미존재 시 `null`
  - `RuneItemData FindRune(string assetId)` — 미존재 시 `null`
  - 내부는 `Dictionary<string, ...>` 캐시 (`OnEnable` 시 빌드).
- **참조 경로**: `EquipmentManager` 가 Inspector 로 `EquipmentDatabase` 단 하나를 직접 참조. `GameBootstrap` 에서 `EquipmentManager` 를 생성/등록할 때 동일 DB 에셋이 로드되도록 초기화 흐름 유지.
- **신규 SO 추가 시 규칙**: 새 `EquipmentItemData` / `RuneItemData` 를 만들면 반드시 `EquipmentDatabase` 배열에 수동 등록. (자동 수집 없음 — 프로젝트 컨벤션.)
- **에셋 위치**: `Assets/_Project/ScriptableObjects/Equipment/EquipmentDatabase.asset`

### 7-2. 저장 스키마 (변경 없음)

Phase 13-B 의 `EquipmentSaveEntry` 를 그대로 사용.

```
EquipmentSaveEntry {
  instanceId   : GUID (런타임 인스턴스 식별자)
  dataAssetId  : EquipmentItemData.name
  grade        : int (런타임 등급 스냅샷, SO 값과 다를 수 있음 — §7-4 참조)
  runeAssetIds : string[] (소켓별 RuneItemData.name, 빈 소켓은 null 또는 빈 문자열)
}

equippedMap : Dictionary<characterId, List<instanceId>>
```

### 7-3. DeserializeOwnedEquipment 흐름

1. `_equipmentInventory.Clear()` 로 기존 상태 초기화 (빈 세이브 로드 포함 안전).
2. `entries` 순회, 각 엔트리에 대해:
   - `EquipmentDatabase.FindEquipment(entry.dataAssetId)` 조회.
   - **누락 시**: 해당 엔트리 스킵 + `Debug.LogWarning($"[EquipmentManager] Unknown equipment '{entry.dataAssetId}' — skipped.")`. 나머지 복원은 계속.
   - 찾았다면 `EquipmentInstance` 재구성:
     - `InstanceId = entry.instanceId` (덮어씀)
     - `Data = found SO`
     - **Grade = entry.grade** (SO 원본 값 무시 — §7-4)
     - `Runes[i] = EquipmentDatabase.FindRune(entry.runeAssetIds[i])`. 개별 룬 누락 시 해당 소켓만 null + 경고. 장비 자체는 유지.
3. `_equipmentInventory.Add(instance)`.
4. 복원 결과: `Debug.Log($"[EquipmentManager] Restored {restored}/{entries.Count} equipment items.")`.

### 7-4. Grade 를 별도 저장하는 이유

향후 강화/재감정 시스템에서 동일 SO 의 런타임 Grade 가 SO 원본 Grade 와 달라질 수 있다. Deserialize 는 항상 **저장값 Grade 를 최우선** 으로 사용한다. SO 는 "기본값 + 시각/메인스탯 테이블" 제공, 런타임 인스턴스는 현재 Grade 를 자체 보유.

### 7-5. DeserializeEquippedMap 흐름

1. 모든 `CharacterEquipmentSet` 초기화 (빈 슬롯으로).
2. `map` 순회:
   - **캐릭터 누락 시** (현재 Rapier 만 구현 → 다른 characterId 가 저장에 있으면): 해당 키 전체 스킵 + `Debug.LogWarning($"[EquipmentManager] Character '{characterId}' not implemented — equipped map entry skipped.")`.
   - 존재하면 `instanceId` 목록 순회:
     - `_equipmentInventory` 에서 해당 instance 찾기.
     - 못 찾으면 해당 슬롯만 스킵 + 경고 (owned 와 equipped 불일치 방어).
     - 찾으면 정상 장착 로직(`EquipInternal`) 으로 슬롯에 배치. 기존 OnEquipped 이벤트는 발행하지 않음 (초기화 phase — MetaStatProvider 가 Init 시점에 한 번 BuildContainer 호출).

### 7-6. 초기화 순서 (GameBootstrap)

Phase 13-B 의 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 흐름 유지:

```
1. EquipmentManager 생성 + EquipmentDatabase 로드 + ServiceLocator 등록
2. SaveManager 생성 + ServiceLocator 등록
3. SaveManager.Load()
   → SaveData 존재 시: EquipmentManager.DeserializeOwnedEquipment + DeserializeEquippedMap 순서로 호출
   → SaveData 없음: 아무것도 하지 않음 (빈 인벤토리로 시작)
4. 씬 로드
```

### 7-7. 테스트 시나리오 (필수)

Phase 14 구현 에이전트는 다음 왕복 시나리오를 모두 트레이스해야 한다:

1. **정상 왕복**: 장비/룬 장착 상태로 저장 → 앱 재시작 → 인벤토리 + 장착 상태 동일 복원.
2. **빈 세이브 로드**: `save.json` 없는 첫 실행 → 빈 인벤토리 + 빈 슬롯 정상 진행.
3. **SO 에셋 누락**: 저장 후 `EquipmentDatabase` 에서 특정 SO 제거 → 재시작 시 해당 장비만 스킵 + 경고, 나머지 정상 복원, 크래시 없음.
4. **룬 단독 누락**: 장비는 유효하지만 장착된 룬 SO 만 누락 → 장비는 복원, 해당 소켓만 비어있음.
5. **미구현 캐릭터 equippedMap 엔트리**: 저장에 `WarriorId` 키가 있음 → 해당 엔트리 스킵 + 경고, Rapier 는 정상 복원.
6. **Owned/Equipped 불일치**: `equippedMap` 에 있는 `instanceId` 가 `ownedEquipment` 에 없음 → 해당 슬롯만 스킵 + 경고, 나머지 정상.
7. **MetaStat 주입**: 복원 후 로비 → 인게임 진입 시 `CharacterPresenterBase.Init` 에서 `EquipmentMetaStatProvider.BuildContainer` 가 복원된 장착 상태 기반으로 최종 스탯을 정상 계산.

구현 에이전트가 자체 발굴한 추가 엣지 케이스가 있다면 같이 보고한다.

### 7-8. 제약 및 경계

- 복원 경로는 **로드 1회** 전제. 러닝 중 save 재로드/핫리로드 고려 불필요.
- 기존 레거시 `Game.Data.Equipment.IEquipmentSaveProvider` 인터페이스는 건드리지 않음 (Phase 13-B 에서 이미 `Game.Data.Save.IEquipmentSaveProvider` 가 단일 진입점).
- `EquipmentDatabase` 는 런타임 SO 조회 전용. 에디터 툴/검증기는 이 Phase 에 포함 안 함 (향후 필요 시 별도).
