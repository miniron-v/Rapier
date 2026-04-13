# 장비 시스템 (Equipment)

장비, 등급, 룬, 인벤토리, 캐릭터별 장착 관리.

## 1. 장비 슬롯 (8 슬롯)

| 카테고리 | 슬롯 | 메인 능력치 |
|---|---|---|
| 무기 | 무기 | 공격력 |
| 방어구 | 모자 | 차지 시간 단축 |
| 방어구 | 상의 | HP |
| 방어구 | 하의 | HP |
| 방어구 | 신발 | 이동속도 |
| 방어구 | 장갑 | 크리티컬 데미지 |
| 장신구 | 목걸이 | 스킬 데미지 증가 |
| 장신구 | 반지 | 크리티컬 데미지 |

**캐릭터 관계**: 각 캐릭터가 자신의 8슬롯 상태 보유. 착용 제한 없음 — 동일 장비 다른 캐릭터로 이동 가능 (이전 캐릭터에서 자동 해제). 한 인스턴스는 동시에 1 캐릭터만.

**characterId 규약**: PascalCase + 언더바 (예: `"Rapier"`). 코드·SO·세이브 전부 동일 리터럴. 대소문자 무시 비교자(`OrdinalIgnoreCase`) 금지 — 어긋나면 리터럴을 맞춘다.

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
| 차지 단축 룬 | 차지 시간 -15% (모든 캐릭터) |
| 회피 룬 | 회피 쿨 -20% (모든 캐릭터) |

- 장비 룬 소켓에 장착/해제 자유. 캐릭터 고유 룬은 해당 캐릭터에만 효과 (다른 캐릭터 장착 시 무효). 장비와 별도 인벤토리.

## 4. 데이터 구조

| 객체 | 책임 |
|---|---|
| `EquipmentItemData` (SO) | 카테고리, 메인 스탯, 등급, 서브 스탯 풀 참조 |
| `EquipmentInstance` (런타임) | 서브 스탯 롤 결과, 룬 장착 상태 |
| `RuneItemData` (SO) | 룬 효과, 적용 캐릭터 |
| `EquipmentManager._equipmentInventory` | 보유 장비 인스턴스 관리 (List) |
| `EquipmentManager.RuneInventory` | 보유 룬 관리 (List) |
| `CharacterEquipmentSet` | 캐릭터별 8슬롯 장착 상태 |

### 능력치 적용

장비/룬 능력치는 모두 **MetaStat** 단일 경로로 캐릭터에 적용 (`STATS.md`).

```
EquipmentManager ─OnEquipped/Unequipped→ EquipmentMetaStatProvider
  → 8슬롯 MainStat + SubStats + 룬 StatEffect 순회
    - 가산형 (HP/ATK/MS/SkillDamage/Crit~): StatEntry 누적 → flat 합 / % 합
    - 감소형 (DodgeCDR/ChargeTimeReduction/InvincibilityBonus): 소스별 (1 − p) 곱
  → MetaStatContainer
  → CharacterPresenterBase.Init(statData, view):
      ServiceLocator.Get<EquipmentManager>() → Provider.BuildContainer("Rapier")
      → CharacterModel 최종 스탯
         가산: (base + meta_flat) × (1 + meta%) × (1 + run%) + run_flat
         감소: base × Π_i(1 − metaP_i) × Π_j(1 − runP_j)
```

- **씬 간 보존**: `EquipmentManager` 는 `DontDestroyOnLoad` + `ServiceLocator.Register(this)`.
- **재계산**: 스테이지 중 장비 변경 없음 → `Init` 시점 1회 계산. 로비 내 변경은 View 미리보기용만 이벤트 발행.
- **룬 처리**: 룬 `StatEffect` (StatEntry) 를 장비와 동일 파이프라인. 감소형은 룬 하나가 하나의 독립 소스. 캐릭터 전용 룬 (`_targetCharacterId` 불일치) 은 Provider 단계에서 필터링.
- **저장 트리거**: `EquipmentManager.Equip/Unequip` 내부에서 `TrySave()` 호출 → `SaveManager.Save()` 로 체이닝 (`SaveManager` 는 `EquipmentManager.Init` 시 주입). 매 장착 변경 시 `save.json` 갱신. 레거시 `IEquipmentSaveProvider` 경로는 사용하지 않는다.

## 5. 보스 드롭 시스템

보스 처치 시 사망 연출 → 드롭 흩뿌림 → 플레이어 접촉 획득. 스테이지 클리어 후 획득 아이템 목록 표시.

### 신규 컴포넌트

| 컴포넌트 | 종류 | 책임 |
|------|------|------|
| `DropTableData` | SO | 보스별 드롭 테이블 (`List<DropEntry>`) |
| `DropEntry` | `[Serializable]` | `EquipmentGrade grade`, `float dropRate`(0~1), `EquipmentItemData[] pool` |
| `LootManager` | 순수 C# | 드롭 판정 + `EquipmentInstance` 생성 (`EquipmentManager` 미참조 — 결과 반환만) |
| `BossDeathSequencer` | MonoBehaviour (씬 배치) | 사망 연출 오케스트레이터 (슬로우모션·카메라·드롭 스폰) |
| `DroppedItemView` | MonoBehaviour (프리팹) | 월드 아이템 — 비주얼·충돌·획득·자동수거 |
| `RunDropListView` | MonoBehaviour (씬 배치) | 스테이지 클리어 시 획득 아이템 스크롤 목록 |

### 기존 코드 변경 최소화 원칙 (SOLID)

- `EnemyPresenterBase`, `CameraFollow`, `StageClearView`, `LootManager`, `RunStatContainer` — **무수정**.
- `BossStatData`: 필드 1개(`dropTable`) 추가.
- `ProgressionManager`: 3곳 추가 (SerializeField 2개, 메서드 2곳 최소 수정).
- `IntermissionManager`: HandleStageCleared 1줄 추가.

### 사망 연출 시퀀스

```
보스 HP = 0
  → EnemyPresenterBase.OnDeath 이벤트
  → ProgressionManager.HandleBossDeath()
    → _runDrops 초기화 (이번 보스 방 한정)
    → BossDeathSequencer.Execute(bossPos, bossTransform, statData, onComplete)

BossDeathSequencer.Execute():
  ① 슬로우모션 (총 2.0초 — 저스트 회피 동일 커브)
     Phase 1 (Hold):   holdCurve   → 1.4초, 최저 0.1배속
     Phase 2 (Exit):   exitCurve   → 0.6초, 1.0배속 복귀
  ② 카메라 (unscaledDeltaTime 기반, 슬로우모션 중에도 정상 동작)
     진입: CameraFollow.SetTarget(bossTransform) + TriggerZoomIn()
     Exit 시점: TriggerZoomReturn(0.6f) + SetTarget(playerTransform)
  ③ 보스 SpriteRenderer 페이드아웃 0.3초 (Exit 완료 후)
  ④ LootManager.RollDrop(statData.dropTable) → List<EquipmentInstance>
  ⑤ DroppedItemView 원형 흩뿌림
     foreach drop:
       angle  = Random.Range(0f, 360f)          // 랜덤 방향
       dist   = Random.Range(minDropDist, maxDropDist)  // 랜덤 거리
       target = bossPos + Rotate(Vector2.up, angle) * dist
       Instantiate(DroppedItemView prefab).Init(instance, bossPos, target)
         // scale 0→1, bossPos → target 이동
  ⑥ onComplete() 호출

ProgressionManager.onComplete():
  → SpawnPortal(bossPos + Vector2.up * portalOffset)
     portalOffset > maxDropDist (Inspector 설정, 겹침 방지)
```

### 드롭 판정 로직

```
foreach (DropEntry entry in dropTable.entries)  // 등급별 독립 판정 (높은 등급 먼저)
    if (Random.value <= entry.dropRate)
        pool에서 랜덤 1개 선택 → new EquipmentInstance(data)
최대 2개 제한 (상위 등급 우선, 초과분 버림)
```

기본 확률 (SO에서 조정): 노말 80% / 레어 30% / 에픽 10% / 유니크 2%.
스테이지 스케일링은 드롭 확률에 영향 없음.

### DroppedItemView 비주얼 및 감지

- **Inner**: SpriteRenderer (Circle 스프라이트, `EquipmentGradeHelper.GetGradeColor(grade)`)
- **Outer shimmer**: SpriteRenderer (같은 Circle, 동일 색 + alpha 펄스 코루틴 — "일렁이는 기운")
  - scale 1.2~1.5 고정, alpha 0.3↔0.7 sin 루프 (0.8초 주기)
- **충돌 없음**: 플레이어에 Collider2D/Rigidbody2D가 없으므로 OnTriggerEnter2D 미사용.
  Portal과 동일하게 **Update + sqrMagnitude 거리 폴링** 방식 채택.
  아이콘: 현재 Circle만 사용. SO에 Sprite가 추가되면 추후 Inner에 덮어씀.

```
DroppedItemView.Init(EquipmentInstance item, Vector2 from, Vector2 to):
  spawnAnim: 0.4초, from→to 이동 + scale 0→1
  완료 시 _isPickupEnabled = true  // 스폰 중 즉시 획득 방지

DroppedItemView.Update():
  if (!_isPickupEnabled || _player == null) return;
  if (sqrDist <= _pickupRadius²) → Collect()

DroppedItemView.Collect():
  OnCollected?.Invoke(item)
  Destroy(gameObject)
```

| 항목 | Portal | DroppedItemView |
|------|--------|-----------------|
| 감지 방식 | Update sqrMagnitude | 동일 |
| 플레이어 참조 | ServiceLocator | 동일 |
| 즉시발동 방지 | `_hasSeparated` | `_isPickupEnabled` (스폰 완료 후 true) |

### 아이템 획득 / 자동 수거

**플레이어 접근**: Update 거리 폴링 → `Collect()`

**포탈 진입 시 자동 수거** (`ProgressionManager.HandlePortalEntered()` 에 추가):
```
FindObjectsOfType<DroppedItemView>() → foreach → item.Collect()
```

수집 콜백(`OnCollected`):
```
ProgressionManager.HandleItemCollected(EquipmentInstance):
  → _runDrops.Add(instance)
  → EquipmentManager.AddEquipmentToInventory(instance)
  → SaveManager.Save()
```

### BossStatData 확장

```csharp
[Header("드롭")]
public DropTableData dropTable; // null = 드롭 없음
```

7종 보스 × 1 DropTable SO. DropTable이 null인 보스는 드롭 없이 포탈만 스폰.

### 스테이지 클리어 UI (RunDropListView)

**표시 시점**: 마지막 보스 포탈 진입 → StageManager가 클리어 판정 → IntermissionManager.HandleStageCleared()

```
IntermissionManager.HandleStageCleared():
  StageClearView.Show()                           // 기존 유지
  var drops = ServiceLocator.Get<ProgressionManager>()?.RunDrops;
  _runDropListView?.Show(drops)                   // 신규
```

**RunDropListView**: StageClearView와 동일 캔버스에 배치. 독립 패널.

- Scroll Rect (vertical)
- 각 슬롯: Circle 아이콘(등급 색상) + 아이템명 + 등급 텍스트
- `Show(IReadOnlyList<EquipmentInstance>)` / `Hide()`
- 드롭 없으면 "획득한 장비 없음" 안내 텍스트

**ProgressionManager 추가 사항**:
- `public IReadOnlyList<EquipmentInstance> RunDrops => _runDrops;`
- Awake에서 `ServiceLocator.Register(this)` (IntermissionManager가 ServiceLocator로 조회)
- `_runDrops.Clear()` 위치: 스테이지 진입 시 (StageManager 이벤트 구독 기존 코드 내)

### 타이밍 요약

| 단계 | 시간 | 비고 |
|------|------|------|
| SlowMotion Hold | 1.4초 | timeScale 0.1까지 |
| SlowMotion Exit | 0.6초 | timeScale 복귀 |
| 보스 페이드아웃 | 0.3초 | Exit 완료 직후 |
| 드롭 스폰 애니 | 0.4초 | 페이드와 겹침 가능 |
| **연출 합계** | **~2.7초** | 포탈은 스폰 애니 완료 후 |

### 디버그 메뉴 (기존 유지)

- 디버그 메뉴: `Rapier/Dev/Add Debug Equipment`, `Rapier/Dev/Add Debug Runes`.
- `Add Debug Equipment` 는 **8 슬롯 전체 자동 장착 + Save** 한 번으로 완결. 디버그 전용.

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
equippedMap: List<EquippedMapEntry> (JsonUtility 는 Dictionary 미지원 → List 로 저장, SaveManager 경계에서 Dict ↔ List 변환)
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
3. var sm = new SaveManager();
4. sm.SetEquipmentProvider(em);
5. em.Init(saveManager: sm, database: db);   // 내부에서 ServiceLocator.Register(em)
6. bool fileExisted 캡처
7. sm.Load()
   → save.json 존재: DeserializeOwnedEquipment → DeserializeEquippedMap 순 호출
   → 없음: 빈 인벤토리
8. 파일 없었으면 sm.Save() 로 최초 파일 생성
9. ServiceLocator.Register(sm);
10. 씬 로드
```

**순서 제약**:
- `sm` 이 `em.Init(saveManager: sm)` 에 주입되므로, `sm` 생성이 `em.Init` 보다 선행.
- `DeserializeOwnedEquipment` → `DeserializeEquippedMap` (후자가 instanceId 로 전자 참조). `SaveManager.Load()` 내부가 보장.
- `em`은 `Init()` 내부에서 `ServiceLocator.Register(this)` — 문서 step 5 시점에 등록됨.

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
- 저장 진입점은 `Game.Data.Save.IEquipmentSaveProvider` 단일. 레거시 `Game.Data.Equipment.IEquipmentSaveProvider` 는 폐기.
- `EquipmentDatabase` 는 런타임 SO 조회 전용. 에디터 툴/검증기 미포함 (향후 필요 시 별도).
