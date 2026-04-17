# 장비 시스템 (Equipment)

장비, 등급, 룬, 인벤토리, 캐릭터별 장착 관리.

> **수치는 이 문서에 없다.** 등급별 확률, 메인스탯 커브, 서브스탯 롤 범위, 강화 테이블, 분해 수익 등 밸런스 수치는 `BALANCE.md` 참조. 본 문서는 구조·스키마·규칙 정의.

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
| 노말 | 회색 | 1 | 0 |
| 레어 | 파랑 | 1 | 1 |
| 에픽 | 보라 | 2 | 2 |
| 유니크 | 주황 | 3 | 3 |

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
- **flatValue 정수화 규약**: `StatEntry.flatValue` (HP, ATK 등 깡 수치) 는 `EquipmentMetaStatProvider` 가 강화 배율 적용 직후 `MathUtils.RoundHalfUp` 으로 정수화. 소수 오차 누적 방지. `percentValue` (% 스탯) 는 소수 유지. UI (`ItemDetailPopupView`, `EnhanceModalView`) 도 동일 헬퍼 사용.
- **재계산**: 스테이지 중 장비 변경 없음 → `Init` 시점 1회 계산. 로비 내 변경은 View 미리보기용만 이벤트 발행.
- **룬 처리**: 룬 `StatEffect` (StatEntry) 를 장비와 동일 파이프라인. 감소형은 룬 하나가 하나의 독립 소스. 캐릭터 전용 룬 (`_targetCharacterId` 불일치) 은 Provider 단계에서 필터링.
- **저장 트리거**: `EquipmentManager.Equip/Unequip` 내부에서 `TrySave()` 호출 → `SaveManager.Save()` 로 체이닝 (`SaveManager` 는 `EquipmentManager.Init` 시 주입). 매 장착 변경 시 `save.json` 갱신. 레거시 `IEquipmentSaveProvider` 경로는 사용하지 않는다.

### 스탯 표기 규칙 (ItemDetailPopupView)

- 저장값(`percentValue`) 은 항상 0~100 **양수** (STATS.md 와 동일 단위).
- 표시 단계에서 **감소형(`DodgeCDR`, `ChargeTimeReduction`)** 만 `-` 부호로 반전. 그 외 가산형은 `+`. 내부 계산에는 영향 없음.
- 라벨: `DodgeCDR` → "회피 쿨타임", `ChargeTimeReduction` → "차지 시간". "감소/단축" 을 라벨에 넣지 않는다 (부호와 중복).
- 예: `DodgeCDR percentValue=5` → "회피 쿨타임 -5.0%".

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

구조:
- 드랍 개수를 먼저 가중 롤로 결정 (1~3개).
- 각 드랍 슬롯마다 등급을 독립 롤.
- 확정된 등급의 보스 전담 풀에서 1개 랜덤 선택.
- **공용 장비는 보스 드랍 풀에 포함되지 않는다.** 공용은 가챠 전용.

구체 수치 (개수 가중, 차수별 등급 확률) 는 `BALANCE.md §4` 참조.

**스테이지별 등급 드롭률 오버라이드**: `StageData._gradeDropRates` (GradeDropRate[]) 가 설정된 스테이지에서는 DropEntry.dropRate 대신 등급별 오버라이드 값을 사용. BossDeathSequencer → LootManager.RollDrop(dropTable, stageDropRates) 경로로 전달. Rate=0 이면 해당 등급 차단. 스테이지별 차수 진화 적용 경로.

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

### 보스별 테마 드롭 세트 (Phase 20)

각 보스는 고유 슬롯을 "전담"하며 4등급 변주를 갖는다. 공용 장비는 모든 보스의 드롭 풀에 해당 등급으로 포함된다.

| 보스 | 전담 슬롯 | Normal | Rare | Epic | Unique |
|---|---|---|---|---|---|
| Titan | Top | 거신의 갑주 | 거신의 판금 갑주 | 거신의 강철 갑주 | 거신의 불멸 갑주 |
| Specter | Shoes | 망령의 신발 | 망령의 발걸음 | 유령의 발걸음 | 황천의 발걸음 |
| Pyromancer | Gloves | 마법사의 장갑 | 불꽃술사의 장갑 | 화염술사의 장갑 | 화염군주의 장갑 |
| Berserker | Weapon | 전사의 검 | 광전사의 검 | 광전사의 대검 | 광전사의 분노 |
| Stormcaller | Hat | 마법사의 관 | 전격술사의 관 | 뇌전술사의 관 | 폭풍군주의 관 |
| Gravekeeper | Necklace | 망자의 목걸이 | 묘지기의 목걸이 | 영혼 수확자의 목걸이 | 평안한 안식 |
| TwinPhantoms | Ring | 영혼의 반지 | 쌍둥이 반지 | 쌍둥이 서약 | 영원한 우정 |

**메인스탯 커브** (카테고리별 등급별 수치): `BALANCE.md §5-2`.

**서브스탯 개수**: Normal 0 / Rare 1 / Epic 2 / Unique 3 (§2). 드롭 순간 풀에서 1회 랜덤 롤 (§5-B).

**보스 장비 배율**: 모든 등급에서 공용 대비 메인스탯 × 1.2, 고유 효과는 Unique 한정 (`BALANCE.md §2-2`).

**에셋명 규칙**: `{Slot}_{Grade}_{Name}.asset` (PascalCase).

**드롭 테이블 구성**: 보스별 테이블의 각 등급 엔트리 pool 에 **해당 보스 전담 아이템만** 포함. 공용 장비는 보스 드랍 풀에 포함되지 않으며, 가챠 배너에서만 등장한다. 드랍률·개수 가중은 `BALANCE.md §4`.

**아이콘**: 모든 장비 SO (신규 28 + 기존 8) 의 `_icon` 필드에 공용 Circle 스프라이트 연결. 런타임 안전 경로 (`Assets/_Project/Art/UI/Circle.png` 등) 에 단일 에셋으로 배치. 등급 색은 UI 측에서 tint 적용.

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

## 5-B. 서브스탯 / 장신구 메인 풀 시스템 (Phase 22-B)

장비 서브스탯과 장신구 메인스탯을 SO 고정값에서 **풀 기반 랜덤 롤**로 전환한다. 드롭 순간 1회 롤, 인스턴스에 고정 저장. 재롤은 추후 강화에서 도입.

### 5-B-1. 신규 SO

```csharp
// 단일 스탯 롤 엔트리: 등급별 범위 + 가중치
[Serializable] public struct StatRollRange { public float min; public float max; }
[Serializable] public class StatRollEntry {
  public StatType statType;
  public float weight;                 // 풀 내 상대 가중치 (합산 후 정규화)
  public StatRollRange normal, rare, epic, unique;   // 등급별 [min,max] 범위
}

// 슬롯별 서브스탯 풀
[CreateAssetMenu(menuName="Game/Data/Equipment/SubStatPoolData")]
public class SubStatPoolData : ScriptableObject {
  [SerializeField] private EquipmentSlotType _slot;
  [SerializeField] private List<StatRollEntry> _entries;
  public EquipmentSlotType Slot => _slot;
  public IReadOnlyList<StatRollEntry> Entries => _entries;
}

// 장신구 메인스탯 풀 (목걸이/반지 공용)
[CreateAssetMenu(menuName="Game/Data/Equipment/MainStatPoolData")]
public class MainStatPoolData : ScriptableObject {
  [SerializeField] private List<StatRollEntry> _entries;
  public IReadOnlyList<StatRollEntry> Entries => _entries;
}
```

### 5-B-2. EquipmentItemData 변경

- **deprecate** `_subStats` (List<StatEntry>) — 필드 제거, SO 전부에서 값 삭제.
- **add** `_subStatPool : SubStatPoolData` — 슬롯별 풀 참조. 필수.
- 장신구(Necklace/Ring) 전용:
  - **deprecate** `_mainStat` — 장신구 SO 에서 제거 (무기/방어구는 유지).
  - **add** `_mainStatPool : MainStatPoolData` — 장신구 SO 에서 필수.

슬롯별 풀은 `_subStatPool` 이 슬롯과 불일치하면 Import 경고.

### 5-B-3. 롤 로직

```
LootManager.RollDrop → new EquipmentInstance(data):
  1) 서브 개수 N = (int)grade  // Normal 0 / Rare 1 / Epic 2 / Unique 3
  2) 풀에서 중복 없이 StatType 기준 N개 추첨 (가중치 기반, 뽑힌 타입은 이후 제외)
  3) 각 추첨마다 등급 구간 [min,max] 에서 Random.Range → SubStats 에 추가
  4) 장신구면 _mainStatPool 에서 1회 추첨 → 등급 구간 롤 → RolledMainStat 에 저장
```

### 5-B-4. EquipmentInstance 확장

```csharp
public List<StatEntry> SubStats { get; }          // 기존 의미 변경: 런타임 롤 결과 저장
public StatEntry? RolledMainStat { get; }          // 장신구만 non-null, 그 외 null → SO _mainStat 사용
```

- 무기/방어구 메인은 SO 고정 (등급별 커브는 §5 유지).
- Provider 는 `RolledMainStat ?? data._mainStat` 패턴으로 읽는다.

### 5-B-5. 저장 스키마 확장 (EquipmentSaveEntry)

```
subStats      : List<StatEntry>   // 롤 결과
rolledMain    : StatEntry?         // nullable — JsonUtility 호환 위해 hasRolledMain:bool + rolledMain:StatEntry 쌍
```

Deserialize 시 `SubStats`/`RolledMainStat` 를 저장값으로 복원. 풀 SO 가 바뀌어도 기존 인스턴스는 저장된 롤 값 유지.

### 5-B-6. 서브 롤 범위

스탯별 등급 구간 [min,max] 수치는 `BALANCE.md §5-3` 참조.

### 5-B-7. 장신구 메인 풀

- 구성: **HP/ATK 를 제외한 % 형 스탯** — MoveSpeed%, DodgeCDR%, ChargeTimeReduction%, InvincibilityBonus%, CritChance%, CritDamage%, SkillDamage%.
- 등급 범위 수치 및 가중치는 `BALANCE.md §5-4` 참조.

### 5-B-8. 풀 SO 생성 목록

- SubStatPool: 8개 (슬롯별) — `SubStatPool_Weapon`, `_Hat`, `_Top`, `_Bottom`, `_Shoes`, `_Gloves`, `_Necklace`, `_Ring`.
- MainStatPool: 1개 — `MainStatPool_Accessory` (목걸이/반지 공용).
- 위치: `Assets/_Project/ScriptableObjects/Equipment/Pools/`.

### 5-B-9. 마이그레이션 체크

1. 기존 36 장비 SO 모두 `_subStats` 비움 + `_subStatPool` 참조 할당.
2. 장신구 SO (공용 1 Necklace + 공용 1 Ring + 보스 Gravekeeper 4 + TwinPhantoms 4 = 10개) `_mainStat` 제거 + `_mainStatPool` 참조 할당.
3. 무기/방어구 SO `_mainStat` 수치를 §5 재조정 커브로 갱신.
4. `EquipmentDatabase` 재검증 (신규 풀 SO 는 미등록 — 직접 참조만).

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

## 8. 분해 / 강화 시스템 (Phase 25)

### 8-1. 재화: 강화의 가루 (Dust)

- 단일 재화. `SaveData.dust : int` 추가 (기본 0). 음수 금지, 클램프.
- 런타임 보유: `EquipmentManager.Dust` (int, 읽기) / `EquipmentManager.AddDust(int)` / `TryConsumeDust(int)`.
- 변경 시 `OnDustChanged(int newAmount)` 이벤트 발행 (UI 구독). `TrySave()` 자동 호출.

### 8-2. 분해 (Dismantle)

대상: 인벤토리 보유 + **미장착** 장비 인스턴스.
가루 산출 공식: `base + perEnhanceBonus × instance.EnhanceLevel`.

등급별 base/perEnhance 수치는 `BALANCE.md §6-5` 참조. 공용/보스 장비 구분 없이 등급에만 의존.

API:
```csharp
// EquipmentManager
int CalculateDismantleYield(EquipmentInstance inst);
int Dismantle(IEnumerable<EquipmentInstance> targets); // 총 획득 가루 반환
```

`Dismantle` 동작:
1. `targets` 중 장착 중인 항목은 스킵 + 경고
2. 각 인스턴스 yield 합산 → 인벤토리 제거 → `AddDust(total)` → `TrySave()`
3. `OnEquipmentInventoryChanged` 이벤트 발행 (있다면 — 없으면 신규)

### 8-3. 강화 (Enhance)

대상: 보유 + 장착 무관 (장착 중에도 강화 가능). 다만 본 Phase 에서는 단순화 — 분해와 동일하게 인벤토리/상세 페이지 진입 경로로 한정. 장착 슬롯에서 직접 강화는 미지원.

**최대 강화치** (등급 기준):
| 등급 | Max |
|---|---|
| Normal | +6 |
| Rare | +9 |
| Epic | +12 |
| Unique | +15 |

`EquipmentInstance.MaxEnhanceLevel => EquipmentGradeHelper.GetMaxEnhance(Grade)`.

**메인스탯 적용**: `EquipmentMetaStatProvider` 가 메인스탯 (SO `_mainStat` 또는 장신구 `RolledMainStat`) 의 flatValue/percentValue 양쪽에 **구간별 가속 곡선 배율**을 곱한다. 곡선 수치 (+1~+15 단계별 배율/증가폭) 는 `BALANCE.md §6-1, §6-2` 참조. 서브스탯은 강화 시점에 누적된 인스턴스 값을 그대로 사용 (Provider 변경 없음).

**서브스탯 강화** (단계 = 강화 후 목표가 3·6·9·12·15 일 때만):
- 인스턴스의 `SubStats` 중 1개를 무작위 선택 (중복 허용 — 이미 강화된 서브 재선택 가능).
- 해당 스탯의 등급 max 값 × 0.25 만큼 가산. 등급 max 는 `SubStatPoolData.Entries` 에서 `entry.statType == picked.statType` 의 `entry.Range(grade).max`.
- usePercent 여부에 따라 percentValue 또는 flatValue 에 가산.
- 서브 0개 (Normal) 면 서브스탯 강화 건너뛰고 메인만 적용.

**성공률 / 가루 비용 테이블**: `BALANCE.md §6-2` 참조. 등급별 상한은 §8-3 의 최대 강화치 표에 의해 자동 차단 (Normal +6 / Rare +9 / Epic +12 / Unique +15).

**테이블 SO**: `EnhanceTableData` (단일 인스턴스). 위치 `Assets/_Project/Resources/EnhanceTableData.asset`. `GameBootstrap` 이 `Resources.Load` 하여 `EquipmentManager.Init` 에 주입.

```csharp
[CreateAssetMenu(menuName="Game/Data/Equipment/EnhanceTableData")]
public class EnhanceTableData : ScriptableObject {
  [SerializeField] private int[] _successPercent;  // index 0 = +1, length 15
  [SerializeField] private int[] _dustCost;         // index 0 = +1, length 15
  public int GetSuccessPercent(int targetLevel);    // 1-based
  public int GetDustCost(int targetLevel);
  public int GetMaxEnhance(EquipmentGrade grade);   // 6/9/12/15
}
```

API:
```csharp
// EquipmentManager
public readonly struct EnhanceResult {
  public bool Success;
  public int  DustSpent;
  public int  PreviousLevel;
  public int  NewLevel;
  public StatType? UpgradedSubStat;     // 서브 강화 발동 시 어떤 스탯
  public float    UpgradedSubAmount;    // 가산량
}

EnhanceResult TryEnhance(EquipmentInstance inst);
// 실패 케이스 (예외 대신 결과로):
//   - 최대 도달 → DustSpent=0, Success=false, NewLevel==PreviousLevel
//   - 가루 부족 → 동일
// 정상 흐름: Dust 차감 → 확률 굴림 → Success 시 EnhanceLevel++ → 3/6/9/12/15 라면 서브 강화 → 이벤트 발행
// 확률 실패: Dust 만 소모, 단계 유지 (파괴/하향 없음). 상세는 BALANCE.md §6-4.
```

이벤트: `OnEquipmentEnhanced(EquipmentInstance, EnhanceResult)` (UI 갱신 + 연출 트리거).

### 8-4. 데이터 / 저장 스키마 변경

`EquipmentInstance`:
- 추가 필드: `private int _enhanceLevel`
- 프로퍼티: `int EnhanceLevel` (get) / `void SetEnhanceLevel(int)` (저장 복원·강화 API 전용 internal)
- 생성자: 신규 인스턴스 = 0. 복원 인스턴스 = 저장값.

`EquipmentSaveEntry`:
- 추가: `public int enhanceLevel = 0;`

`SaveData`:
- 추가: `public int dust = 0;`

마이그레이션: 기존 save.json 에 필드 없음 → 기본값 0 으로 자연 복원. 스키마 버전 승격 불필요 (필드 추가만, 의미 손실 없음).

### 8-5. UI 진입점

- 분해 흐름은 `UI.md §6` 참조 (분해 모드 / 결과 모달).
- 강화 흐름은 `UI.md §7` 참조 (상세 페이지 3버튼 / 강화 모달).
