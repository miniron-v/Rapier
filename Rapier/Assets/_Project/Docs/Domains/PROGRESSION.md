# 진행 시스템 (Progression)

스테이지, 인터미션, 사망/이어하기, 저장 시스템.

## 1. 스테이지 구조

`[인터미션] → [보스1] → [인터미션] → [보스2] → [인터미션] → [보스3] → [인터미션] → [보스4] → [클리어]`

1 스테이지 = 보스 4방 + 인터미션 4방 (매 보스 앞). 스테이지 내 같은 보스 재출현 금지. 다른 스테이지에서 강화 버전 재활용 가능.

| 객체 | 책임 |
|---|---|
| `StageData` (SO) | 보스 4종 순서, 스테이지 번호, 보상 |
| `StageProgress` (런타임) | 현재 위치 (1~4), 클리어 보스, RunStat 누적 |
| `ProgressionManager` | 방 전환, 사망 처리, 보상 계산, RunStat clear |

## 2. 인터미션 방

두 효과 동시: (1) **HP 100% 회복** (자동, 강제), (2) **스탯 선택** (능동, 2개 후보 중 1개).

**규칙**: 풀에서 매번 2개 랜덤 추출 (서로 다른 2개 보장, 같은 종류 동시 금지). 누적 가능 (같은 스탯 재등장 시 누적 적용). 풀 7종 + 강도는 `Rapier_Prototype_DesignDoc.md §8-2`.

| 객체 | 책임 |
|---|---|
| `IntermissionRoomPresenter` | 회복 트리거, 후보 추출, 선택 처리 |
| `RunStatPool` (SO) | 후보 풀 (스탯 종류 + 강도) |
| `RunStatModifier` | 선택 누적/저장/적용 |

## 3. 사망 / 이어하기

| 상황 | 결과 |
|---|---|
| 보스 사망 | **그 보스부터 이어하기** 가능. RunStat + 진행도 유지. 보스/플레이어 HP 풀 복원. |
| 능동 로비 복귀 | **진행도 + RunStat 초기화.** 다음 진입 시 1번 보스부터. |
| 스테이지 클리어 | 보상 → 로비/다음 스테이지 선택. RunStat 초기화. |

## 4. 진행 표시

HUD 에 현재 위치 `N / 4` 표시. 다음 방 미리보기 없음 (긴장감 유지). 인터미션 방은 카운트 제외.

### BossHudView 통합

`ProgressionManager` 가 씬의 `BossHudView` (`[SerializeField]`) 를 구동:

| 시점 | 호출 |
|---|---|
| 보스 스폰 | `SetupBoss(bossName, bp, stageIndex, totalBossRooms)` |
| 페이즈 변경 | `UpdatePhase(phase)` |
| 마지막 보스 처치 | `ShowResult(true)` |
| 플레이어 사망 | `ShowResult(false)` |
| 이어하기 진입 | `HideVictoryPanel()` + `HideResultPanel()` |

`BossHudView` 는 씬 직접 연결 또는 `BossHudSetup` (`Rapier/Boss HUD/Rebuild Boss HUD`) 자동 와이어링.

## 5. 저장 시스템 (JSON)

PlayerPrefs 금지. `Application.persistentDataPath/save.json`. 구현: `Game.Data.Save.SaveManager`.

### 저장 항목

| 카테고리 | 내용 | 시점 |
|---|---|---|
| 메타 | version, userId, deviceId, lastSavedAt, schemaCreatedAt | 모든 Save() |
| 캐릭터 | 보유, 마지막 선택, 레벨/스킬 | 변경/강화 |
| 장비 | 보유 인스턴스, 캐릭터별 장착, 룬 보유/장착 | 장비 변경 |
| 진행 | 최고 도달 스테이지, 클리어 목록 | 클리어 |
| 재화 | 골드, 가챠 티켓, 강화 재료, 룬 가챠 티켓 | 변동 |
| 미션 | 일일/주간 진행, 마지막 리셋 시각 | 진행/리셋 |
| 설정 | 사운드/진동/그래픽 | 변경 |

### 계정 연동 필드

| 필드 | 타입 | 용도 |
|---|---|---|
| `version` | int | 스키마 버전, 마이그레이션 진입점 |
| `userId` | string | 계정 식별자 (미연동 시 빈 문자열) |
| `deviceId` | string | 기기 고유값 (충돌 해소) |
| `lastSavedAt` | long | Unix epoch(ms), 최신본 판정 |
| `schemaCreatedAt` | long | 최초 Save 시점, 디버깅용 |

### 저장 금지 (메모리 only)

`RunStat` 누적, `StageProgress` 현재 위치, 보스 HP. 스테이지 진행도 영구 저장은 추후 결정.

### 책임 분리

| 객체 | 책임 |
|---|---|
| `SaveData` | `[Serializable]` POCO, 직렬화 모델 |
| `SaveManager` | 인메모리 `Current` + 저장/로드 진입점, Provider 위임 |
| `SaveMigrator` | 버전별 체인, `Migrate(fromVersion, data)` |
| `ISaveSyncService` | 로컬/서버 동기화 추상 (`LocalOnlySaveSyncService` 단일 구현) |
| `IEquipmentSaveProvider` | 장비 섹션 직렬화 (EquipmentManager 직접 구현) |
| `GameBootstrap` | static 부트스트랩, SaveManager/EquipmentManager 생성·배선·등록 |

### 라이프사이클

`SaveManager` / `EquipmentManager` 는 POCO, `ServiceLocator` static 이 수명 유지 (`MonoBehaviour`/`DontDestroyOnLoad` 불필요).

- **진입점**: `Game.Core.Services.GameBootstrap`, `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 의 `Bootstrap()`.
- **시점**: 앱 기동 직후 첫 씬 로드 전 1회. 재호출 시 중복 가드.
- **배선 순서**:
  1. 중복 가드 (`ServiceLocator.Get<SaveManager>()` 존재 시 return).
  2. `var db = Resources.Load<EquipmentDatabase>("EquipmentDatabase");`
  3. `var em = new EquipmentManager();`
  4. `em.Init(saveProvider: em, database: db);` (`EquipmentManager` 가 `IEquipmentSaveProvider` 직접 구현 → 자기 자신. 내부 `ServiceLocator.Register(this)`).
  5. `var sm = new SaveManager();`
  6. `sm.SetEquipmentProvider(em);`
  7. `sm.Load();` (파일 존재 시 역직렬화 + 마이그레이션, 없으면 defaults).
  8. **파일 없었으면 즉시 `sm.Save()` 1회** — 최초 `save.json` 생성, 메타 필드 (deviceId/schemaCreatedAt) 디스크 고정. 팁: `Load()` 전 `File.Exists` 사전 판정.
  9. `ServiceLocator.Register(sm);`
- **지속성**: `ServiceLocator` static 참조 → GC 대상 아님. Lobby ↔ Stage/BossRush 전환 단일 인스턴스 유지, 교체 없음.
- **엔트리 씬 독립**: `BeforeSceneLoad` 라 어느 씬을 Play 해도 동일 초기화, 수동 배선 불필요.
- **접근**: `ServiceLocator.Get<SaveManager>()` / `ServiceLocator.Get<EquipmentManager>()`.
- **파괴**: 앱 종료까지, 명시 Dispose 없음.
- **배선 누락 감지**: `LobbyPresenter.Init` 가 `ServiceLocator.Get<SaveManager>()` 사용 → null 이면 `HomeTabPresenter` 등 폴백 + **에디터 경고 로그**.

### `IEquipmentSaveProvider` 구현

`EquipmentManager` 직접 구현 (별도 어댑터 없음). 이미 `_equipmentInventory` / `_characterSets` 를 보유하므로 직렬화 논리를 단일화.

- `SerializeOwnedEquipment()` → `List<EquipmentSaveEntry>`
- `SerializeEquippedMap()` → `Dictionary<string, List<string>>` (캐릭터 ID → 장착 instanceId)
- `DeserializeOwnedEquipment/EquippedMap(...)` — 저장 엔트리로 재구성. 역직렬화는 `SaveManager.Load()` 내부 호출.

### 마이그레이션

- `SaveData.version < CurrentSchemaVersion` 이면 `SaveMigrator.Migrate()` 체인 후 즉시 재저장.
- 버전 업 시 이 문서 + `SaveMigrator` 에 새 단계 **반드시 쌍으로** 추가.
- 필드 삭제는 `[Obsolete]` 2 단계 후 실제 제거 (로드 호환 보장).
