# 진행 시스템 (Progression)

스테이지, 인터미션 방, 사망/이어하기, 저장 시스템을 다룬다.

---

## 1. 스테이지 구조

```
[보스1] → [인터미션] → [보스2] → [인터미션] → [보스3] → [인터미션] → [보스4] → [클리어]
```

- 한 스테이지 = **4개의 보스 방** + 사이마다 **3개의 인터미션 방**
- 같은 보스는 한 스테이지 내 재출현 금지
- 다른 스테이지에서 같은 보스를 강화 버전으로 재활용 가능

### 핵심 데이터

| 객체 | 책임 |
|------|------|
| `StageData` (SO) | 스테이지 구성: 보스 4종 순서, 스테이지 번호, 보상 |
| `StageProgress` (런타임) | 현재 진행 위치 (1~4), 클리어한 보스 목록, RunStat 누적 상태 |
| `ProgressionManager` | 다음 방으로 넘기기, 사망 처리, 보상 계산, RunStat clear |

---

## 2. 인터미션 방

인터미션 방은 보스 사이마다 등장한다. 두 가지 효과를 동시에 제공:

1. **HP 100% 회복** — 자동, 강제 적용
2. **스탯 선택** — 능동, 사용자가 2개 후보 중 1개 선택

### 스탯 선택 규칙

- 풀에서 매번 **2개 랜덤 추출**
- **같은 종류는 한 후보 안에 동시에 안 나옴** (서로 다른 2개 보장)
- **누적 가능**: 다음 인터미션에서 동일 스탯이 다시 나오면 누적 적용
- 스탯 풀 (7종) 과 강도는 `Rapier_Prototype_DesignDoc.md §8-2` 참조

### 책임 분리

| 객체 | 책임 |
|------|------|
| `IntermissionRoomPresenter` | 진입 시 자동 회복 트리거, 스탯 후보 추출, 사용자 선택 처리 |
| `RunStatPool` (SO) | 후보 풀 정의 (스탯 종류 + 효과 강도) |
| `RunStatModifier` | 선택된 RunStat 누적/저장/적용 |

---

## 3. 사망 / 이어하기

| 상황 | 결과 |
|------|------|
| 보스에게 사망 | **그 보스부터 이어하기** 가능. RunStat 유지. 진행 상태 (현재 위치, 클리어 보스) 유지. |
| 사용자가 능동적으로 로비 복귀 | **진행도 + RunStat 모두 초기화.** 다음 진입 시 1번 보스부터. |
| 스테이지 클리어 | 보상 화면 → **로비 복귀 / 다음 스테이지 진입** 사용자 선택. RunStat 무조건 초기화. |

> 사용자가 사망 후 이어하기를 선택하면 현재 위치에서 재시작. 보스 HP는 풀로, 플레이어 HP도 풀로 복원.

---

## 4. 진행 표시

- HUD에 현재 위치 표시: 예 `1 / 4`, `2 / 4`
- 다음 방 미리보기는 **없음** (긴장감 유지)
- 인터미션 방은 진행 카운트에 포함되지 않음

### BossHudView 통합

`ProgressionManager` 는 보스 스폰 시 씬의 `BossHudView` (`[SerializeField] private BossHudView _bossHud`) 에 아래를 호출하여 HUD 를 구동한다.

| 시점 | 호출 |
|------|------|
| 보스 스폰 | `_bossHud.SetupBoss(bossName, bp, stageIndex, totalBossRooms)` |
| 페이즈 변경 | `_bossHud.UpdatePhase(phase)` |
| 마지막 보스 처치 | `_bossHud.ShowResult(true)` |
| 플레이어 사망 | `_bossHud.ShowResult(false)` |
| 이어하기 진입 | `_bossHud.HideVictoryPanel()` + `_bossHud.HideResultPanel()` |

`BossHudView` 는 씬 오브젝트에서 직접 연결하거나, `BossHudSetup` 에디터 툴(`Rapier/Boss HUD/Rebuild Boss HUD`)의 Rebuild 로 자동 와이어링된다.

---

## 5. 저장 시스템 (JSON)

PlayerPrefs 금지. JSON 파일을 `Application.persistentDataPath/save.json` 에 저장. 현재 구현은 `Game.Data.Save.SaveManager`.

### 저장 항목

| 카테고리 | 내용 | 저장 시점 |
|---------|------|---------|
| 메타 | `version`, `userId`, `deviceId`, `lastSavedAt`, `schemaCreatedAt` | 모든 Save() 호출 시 |
| 캐릭터 | 보유 캐릭터, 마지막 선택 캐릭터, 캐릭터별 레벨/스킬 | 캐릭터 변경/강화 시 |
| 장비 | 보유 장비 인스턴스, 캐릭터별 장착 상태, 룬 보유/장착 | 장비 변경 시 |
| 진행 | 최고 도달 스테이지, 클리어한 스테이지 목록 | 스테이지 클리어 시 |
| 재화 | 골드, 가챠 티켓, 강화 재료, 룬 가챠 티켓 | 재화 변동 시 |
| 미션 | 일일/주간 미션 진행, 마지막 리셋 시각 | 미션 진행/리셋 시 |
| 설정 | 사운드/진동/그래픽 옵션 | 설정 변경 시 |

### 계정 연동 대비 필드 (Phase 13-B)

| 필드 | 타입 | 용도 |
|------|------|------|
| `version` | int | 스키마 버전. 로드 시 마이그레이션 진입점 |
| `userId` | string | 계정 연동 후 서버 식별자. 미연동 시 빈 문자열 |
| `deviceId` | string | 기기 고유값 (충돌 해소, 신규 계정 생성 hint) |
| `lastSavedAt` | long | Unix epoch(ms). 서버 동기화 시 최신본 판정 기준 |
| `schemaCreatedAt` | long | 최초 Save 시점. 마이그레이션 디버깅용 |

### 저장 금지 항목 (메모리 only)

| 항목 | 이유 |
|------|------|
| `RunStat` 누적 상태 | 일회성. 스테이지 종료 시 소멸 |
| `StageProgress` (현재 위치) | 사망 이어하기는 메모리에서만 처리. 앱 종료 시 진행도 소실 OK (프로토타입 단계) |
| 보스 HP | 일회성 |

> 스테이지 진행 상태의 영구 저장 (앱 종료 후에도 이어하기) 은 추후 결정 사항.

### 책임 분리 (Phase 13-B 구조)

| 객체 | 책임 |
|------|------|
| `SaveData` | 저장 가능한 모든 데이터의 직렬화 모델 (`[Serializable]` POCO) |
| `SaveManager` | 인메모리 `Current` 보유 + 저장/로드 진입점. Provider 조합으로 섹션별 직렬화 위임 |
| `SaveMigrator` | 버전별 마이그레이션 단계 체인. `Migrate(int fromVersion, SaveData data)` 진입점 |
| `ISaveSyncService` | 로컬/서버 동기화 추상 인터페이스. 현재는 `LocalOnlySaveSyncService` 단일 구현 |
| `IEquipmentSaveProvider` | 장비 섹션 직렬화 위임. `EquipmentManager` 가 직접 구현 (별도 어댑터 없음) |
| `GameBootstrap` | 앱 전역 서비스 진입점 (static). `SaveManager`/`EquipmentManager` 인스턴스를 생성·배선·`ServiceLocator` 에 등록한다 |

### 라이프사이클 (Phase 13-B)

`SaveManager` 와 `EquipmentManager` 는 POCO 이며, `ServiceLocator` 는 static 이라 등록된 인스턴스는 앱 수명 동안 유지된다. 별도의 `MonoBehaviour` / `DontDestroyOnLoad` 없이도 씬 전환에 의해 파괴되지 않는다.

- **부트스트랩 진입점**: `Game.Core.Services.GameBootstrap` static 클래스. `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` 로 장식된 `Bootstrap()` 이 자동 호출된다.
- **생성 시점**: 앱 기동 직후, **첫 씬 로드 전** 1회. 이후 호출 없음. 재호출 시 중복 가드로 조기 return.
- **배선 순서**:
  1. 중복 가드 — `ServiceLocator.Get<SaveManager>()` 가 이미 존재하면 return.
  2. `var em = new EquipmentManager();`
  3. `em.Init(saveProvider: em);` — `EquipmentManager` 가 `IEquipmentSaveProvider` 를 직접 구현하므로 자기 자신을 전달. 내부에서 `ServiceLocator.Register(this)`.
  4. `var sm = new SaveManager();`
  5. `sm.SetEquipmentProvider(em);`
  6. `sm.Load();` — 파일 존재 시 역직렬화 + 마이그레이션, 부재 시 defaults.
  7. **파일이 없었던 경우 즉시 `sm.Save()` 1회 호출** — 최초 `save.json` 을 생성하여 첫 실행 증거와 메타 필드(deviceId/schemaCreatedAt) 를 디스크에 고정. 구현 팁: `sm.Load()` 전에 `File.Exists(Application.persistentDataPath + "/save.json")` 로 사전 판정.
  8. `ServiceLocator.Register(sm);`
- **지속성**: `ServiceLocator` (static) 가 참조를 보유하므로 GC 대상이 되지 않는다. Lobby ↔ Stage/BossRush 전환 간 단일 인스턴스 유지. 인스턴스 교체는 일어나지 않는다.
- **엔트리 씬 독립성**: `BeforeSceneLoad` 타이밍이라 Lobby / StageDemo / BossRushDemo 어느 씬을 에디터에서 직접 Play 로 시작하더라도 동일하게 초기화된다. 씬별 수동 배선이 필요 없다.
- **접근**: 어디서든 `ServiceLocator.Get<SaveManager>()` / `ServiceLocator.Get<EquipmentManager>()` 로 조회.
- **파괴**: 앱 종료까지 유지. 명시적 `Dispose` 없음.
- **배선 누락 감지**: `LobbyPresenter.Init` 는 `ServiceLocator.Get<SaveManager>()` 결과를 사용한다. 부트스트랩이 실패해 `SaveManager` 가 null 이면 `HomeTabPresenter` 등이 폴백으로 진입한다 — 이 경우 **에디터 경고 로그** 를 남겨 배선 누락을 조기에 드러낸다.

### `IEquipmentSaveProvider` 구현 (Phase 13-B)

`EquipmentManager` 가 `IEquipmentSaveProvider` 를 직접 구현한다 (별도 어댑터 클래스 없음). 이유: EquipmentManager 가 이미 `_equipmentInventory` / `_characterSets` 상태를 보유하므로 직렬화 논리를 한 곳에 유지하는 편이 단순하다.

- `SerializeOwnedEquipment()` — `_equipmentInventory` 를 `List<EquipmentSaveEntry>` 로 변환
- `SerializeEquippedMap()` — `_characterSets` 를 `Dictionary<string, List<string>>` (캐릭터 ID → 장착 인스턴스 ID 목록) 로 변환
- `DeserializeOwnedEquipment(...)` — 저장된 엔트리로 인벤토리 재구성
- `DeserializeEquippedMap(...)` — 캐릭터 세트 재구성. 역직렬화는 `SaveManager.Load()` 내부에서 호출된다.

### 마이그레이션 규약

- 로드 시 `SaveData.version < CurrentSchemaVersion` 이면 `SaveMigrator.Migrate()` 체인 통과 후 즉시 재저장
- 버전 업 시 이 문서와 `SaveMigrator` 에 새 단계를 **반드시 쌍으로** 추가
- 필드 삭제는 `[Obsolete]` 2 단계 후 실제 제거 (로드 호환 보장)

---

## 6. 미션 시스템

### 일일 미션 (5종, 매일 04:00 리셋)

| # | 미션 | 추적 이벤트 | 보상 |
|---|------|------------|------|
| 1 | 스테이지 1회 클리어 | OnStageCleared | 골드 500 + 가챠 티켓 1 |
| 2 | 보스 5마리 처치 | OnBossKilled | 골드 300 |
| 3 | 저스트 회피 3회 성공 | OnJustDodgeTriggered | 골드 200 + 강화 재료 5 |
| 4 | 차지 스킬 10회 사용 | OnChargeSkillUsed | 골드 200 |
| 5 | 일일 미션 4개 완료 (메타) | OnDailyMissionCompleted | 가챠 티켓 1 |

### 주간 미션 (3종, 매주 월요일 04:00 리셋)

| # | 미션 | 추적 이벤트 | 보상 |
|---|------|------------|------|
| 1 | 보스 50마리 누적 처치 | OnBossKilled | 가챠 티켓 5 + 골드 3000 |
| 2 | 새 스테이지 도달 또는 최고 기록 갱신 | OnStageRecordUpdated | 룬 가챠 티켓 3 |
| 3 | 일일 미션 7일 모두 완료 | OnDailyAllCompleted | 에픽 장비 확정 1 |

### 책임 분리

| 객체 | 책임 |
|------|------|
| `MissionData` (SO) | 미션 정의 (목표 수치, 보상, 추적 이벤트) |
| `MissionProgressModel` | 진행 상태 (현재 수치, 완료 여부, 보상 수령 여부) |
| `MissionTracker` | 게임 이벤트 구독 → 미션 진행 갱신 |
| `MissionResetScheduler` | 일일/주간 리셋 시각 체크 및 초기화 |

---

## 7. 구현 순서 권장

1. `SaveDataModel` + `SaveDataRepository` (저장 인프라부터)
2. `RunStatPool` SO + `RunStatModifier` (런 스탯 컨테이너)
3. `IntermissionRoomPresenter` + UI
4. `StageData` SO + `ProgressionManager` (스테이지 흐름)
5. 사망/이어하기/진행도 초기화 처리
6. `MissionTracker` + 미션 데이터 정의
