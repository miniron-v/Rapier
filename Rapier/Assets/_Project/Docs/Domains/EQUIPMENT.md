# 장비 시스템 (Equipment)

장비, 등급, 룬, 인벤토리, 캐릭터별 장착 관리.

## 1. 장비 슬롯 (8 슬롯)

| 카테고리 | 슬롯 | 메인 능력치 |
|---|---|---|
| 무기 | 무기 | 공격력 |
| 방어구 | 모자 | 쿨타임 감소 |
| 방어구 | 상의 | HP |
| 방어구 | 하의 | HP |
| 방어구 | 신발 | 이동속도, HP |
| 방어구 | 장갑 | 크리티컬 데미지 |
| 장신구 | 목걸이 | 랜덤: 쿨감/스킬뎀/크리확 |
| 장신구 | 반지 | 랜덤: 쿨감/스킬뎀/크리확 |

**캐릭터 관계**: 각 캐릭터가 자신의 8슬롯 상태 보유. 착용 제한 없음 — 동일 장비 다른 캐릭터로 이동 가능 (이전 캐릭터에서 자동 해제). 한 인스턴스는 동시에 1 캐릭터만.

## 2. 등급 / 룬 소켓

| 등급 | 색상 | 룬 소켓 | 서브 스탯 |
|---|---|---|---|
| 노말 | 회색 | 1 | 1 |
| 레어 | 파랑 | 1 | 2 |
| 에픽 | 보라 | 2 | 3 |
| 유니크 | 주황 | 3 | 4 |

- **메인 스탯**: 카테고리 고정 (§1), 등급에 따라 수치만 다름.
- **서브 스탯**: 정해진 풀에서 랜덤 부여 (깡/%). 풀 구성은 카테고리별·데이터 작업 시점 결정.

## 3. 룬 시스템

캐릭터 공통 스탯과 별개로 캐릭터 고유 메커니즘을 강화.

| 룬 예시 | 효과 |
|---|---|
| 표식 룬 | (Rapier) 표식 최대 중첩 +1 |
| 분쇄 룬 | (Warrior) 대지 분쇄 범위 +20% |
| 잔상 룬 | (Assassin) 잔상 지속 +30% |
| 폭발 룬 | (Ranger) 강화 폭발 화살 데미지 +50% |
| 기본 쿨감 룬 | 차지 스킬 쿨 -15% (모든 캐릭터) |
| 회피 룬 | 회피 쿨 -20% (모든 캐릭터) |

- 장비 룬 소켓에 장착/해제 자유. 캐릭터 고유 룬은 해당 캐릭터에만 효과 (다른 캐릭터 장착 시 무효). 장비와 별도 인벤토리.

## 4. 데이터 구조

| 객체 | 책임 |
|---|---|
| `EquipmentData` (SO) | 카테고리, 메인 스탯, 등급, 서브 스탯 풀 참조 |
| `EquipmentInstance` (런타임) | 서브 스탯 롤 결과, 룬 장착 상태 |
| `RuneData` (SO) | 룬 효과, 적용 캐릭터 |
| `RuneInstance` (런타임) | 룬 인스턴스 |
| `EquipmentInventory` | 보유 장비/룬 인스턴스 관리 |
| `CharacterEquipment` | 캐릭터별 8슬롯 장착 상태 |

### 능력치 적용

장비/룬 능력치는 모두 **MetaStat** 단일 경로로 캐릭터에 적용 (`STATS.md`).

```
EquipmentManager ─OnEquipped/Unequipped→ EquipmentMetaStatProvider : IMetaStatProvider
  → 8슬롯 MainStat + SubStats + 룬 StatEffect 순회, StatEntry 누적 → MetaStatContainer
  → (base + meta_flat) × (1 + meta%) 계산
  → CharacterPresenterBase.Init(statData, view):
      ServiceLocator.Get<EquipmentManager>() → Provider.BuildContainer()
      → CharacterModel 최종 스탯 (maxHp/atk/moveSpeed)
```

- **씬 간 보존**: `EquipmentManager` 는 `DontDestroyOnLoad` + `ServiceLocator.Register(this)`.
- **재계산**: 스테이지 중 장비 변경 없음 → `Init` 시점 1회 계산. 로비 내 변경은 View 미리보기용만 이벤트 발행.
- **룬 처리**: 룬 `StatEffect` (StatEntry) 를 장비와 동일 파이프라인 합산. 캐릭터 전용 룬 (`_targetCharacterId` 불일치) 은 Provider 단계에서 필터링.

## 5. 프로토타입 데이터

가챠/드롭은 후순위. 사전 정의 SO + 디버그 메뉴 제공.

- 노말~유니크 각 카테고리당 1~2개, 룬 5~6종 (Rapier 고유 + 공통).
- 디버그 메뉴: `Rapier/Dev/Add Debug Equipment`, `Rapier/Dev/Add Debug Runes`.

## 6. UI

로비 **탭 2 (캐릭터 관리)** 장비 영역. 자세한 구성은 `UI.md` + `Rapier_Prototype_DesignDoc.md §9-2`.

## 7. 저장 / 복원 파이프라인

저장 → 종료 → 재시작 → 복원 왕복 지원.

### 7-1. EquipmentDatabase (SO 레지스트리)

- **목적**: `dataAssetId` / `runeAssetId` (= SO 에셋명) → 런타임 SO 레퍼런스 조회.
- **필드**: `_equipment : EquipmentItemData[]`, `_runes : RuneItemData[]`.
- **API**: `FindEquipment(assetId)` / `FindRune(assetId)` — 미존재 시 null. 내부 `Dictionary` 캐시 (`OnEnable` 빌드).
- **배선**: `EquipmentManager` 는 순수 C# 라 Inspector 주입 불가 → `GameBootstrap` 이 `Resources.Load<EquipmentDatabase>("EquipmentDatabase")` 후 `EquipmentManager.Init(saveProvider, database)` 로 주입.
- **추가 규칙**: 신규 SO 는 반드시 `EquipmentDatabase` 배열에 수동 등록 (자동 수집 없음 — 프로젝트 컨벤션).
- **에셋 위치**: `Assets/_Project/Resources/EquipmentDatabase.asset`. 클래스: `Assets/_Project/Scripts/Data/Equipment/EquipmentDatabase.cs`.
- **로드 실패**: `Resources.Load` null 이면 `Debug.LogWarning` 후 빈 DB 로 진행 (기동 차단 금지, Deserialize 는 전부 스킵+경고).

### 7-2. 저장 스키마

```
EquipmentSaveEntry {
  instanceId   : GUID
  dataAssetId  : EquipmentItemData.name
  grade        : int (런타임 스냅샷, §7-4)
  runeAssetIds : string[] (빈 소켓 null/"")
}
equippedMap: Dictionary<characterId, List<instanceId>>
```

### 7-3. DeserializeOwnedEquipment 흐름

1. `_equipmentInventory.Clear()` (빈 세이브 로드 안전).
2. `entries` 순회:
   - `FindEquipment(dataAssetId)`. 누락 시 스킵 + `Debug.LogWarning("Unknown equipment '{id}' — skipped.")`.
   - 복원: `InstanceId=entry.instanceId`, `Data=SO`, **`Grade=entry.grade`** (SO 원본 무시, §7-4), `Runes[i]=FindRune(runeAssetIds[i])`. 개별 룬 누락 시 해당 소켓만 null + 경고, 장비 자체는 유지.
3. `_equipmentInventory.Add(instance)`.
4. 결과: `Debug.Log("Restored {restored}/{count} equipment items.")`.

### 7-4. Grade 별도 저장 이유

향후 강화/재감정 시 동일 SO 의 런타임 Grade 가 원본과 달라질 수 있음 → Deserialize 는 **저장값 Grade 최우선**. SO 는 기본값 + 시각/메인스탯 테이블 제공, 런타임 인스턴스가 현재 Grade 자체 보유.

### 7-5. DeserializeEquippedMap 흐름

1. 모든 `CharacterEquipmentSet` 초기화 (빈 슬롯).
2. `map` 순회:
   - **캐릭터 누락** (현재 Rapier 만 구현, 다른 characterId 저장 존재): 키 전체 스킵 + `Debug.LogWarning("Character '{id}' not implemented — equipped map entry skipped.")`.
   - 존재 시 `instanceId` 순회, `_equipmentInventory` 에서 찾아 `EquipInternal` 슬롯 배치. 미발견 시 슬롯만 스킵 + 경고 (owned/equipped 불일치 방어).
   - 초기화 phase 라 `OnEquipped` 이벤트 미발행 — MetaStatProvider 가 `Init` 시점 `BuildContainer` 1회.

### 7-6. 초기화 순서

`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` `GameBootstrap.Bootstrap()`:

```
1. var db = Resources.Load<EquipmentDatabase>("EquipmentDatabase");
2. var em = new EquipmentManager();
3. em.Init(saveProvider: em, database: db);
4. var sm = new SaveManager();
5. sm.SetEquipmentProvider(em);
6. sm.Load()
   → save.json 존재: DeserializeOwnedEquipment → DeserializeEquippedMap 순 호출
   → 없음: 빈 인벤토리, sm.Save() 로 최초 파일 생성
7. ServiceLocator.Register(sm);
8. 씬 로드
```

**순서 제약**: `DeserializeOwnedEquipment` → `DeserializeEquippedMap` (후자가 instanceId 로 전자 참조). `SaveManager.Load()` 내부가 보장.

### 7-7. 테스트 시나리오

1. **정상 왕복**: 장비/룬 장착 저장 → 재시작 → 인벤토리 + 장착 동일 복원.
2. **빈 세이브**: `save.json` 없음 → 빈 인벤토리 + 빈 슬롯 정상 진행.
3. **SO 누락**: 저장 후 DB 에서 특정 SO 제거 → 해당 장비만 스킵 + 경고, 나머지 정상, 크래시 없음.
4. **룬 단독 누락**: 장비 유효, 룬 SO 만 누락 → 장비 복원, 해당 소켓만 비어있음.
5. **미구현 캐릭터**: 저장에 `WarriorId` 키 → 엔트리 스킵 + 경고, Rapier 정상.
6. **Owned/Equipped 불일치**: `equippedMap` instanceId 가 ownedEquipment 에 없음 → 슬롯만 스킵 + 경고, 나머지 정상.
7. **MetaStat 주입**: 복원 후 로비 → 인게임 진입 시 `CharacterPresenterBase.Init` → `EquipmentMetaStatProvider.BuildContainer` 가 복원 상태 기반 최종 스탯 계산.

### 7-8. 제약 및 경계

- 복원 경로는 로드 1회 전제 (러닝 중 재로드/핫리로드 고려 불필요).
- 레거시 `Game.Data.Equipment.IEquipmentSaveProvider` 인터페이스 유지 (`Game.Data.Save.IEquipmentSaveProvider` 가 단일 진입점).
- `EquipmentDatabase` 는 런타임 SO 조회 전용. 에디터 툴/검증기 미포함 (향후 필요 시 별도).
