# UI 시스템 (UI)

---

## 1. 씬 구조

| 씬 | 역할 |
|----|------|
| Lobby | 5탭 로비. 메인/캐릭터/상점/미션/설정. |
| StageDemo | 스테이지 진행 씬. 8방 RoomNode(인터미션/보스 교차). 포탈 시스템 기반. |
| BossRushDemo | 보스 러시 씬. `BossRushManager` 가 공용 `BossHudView` 에 연결되어 동작 (Phase 13-A 통합). 12-C 신규 보스 배열 등록은 Phase 12-E 대기. |

- `SceneController.LoadLobby()` / `LoadGame()` 으로 전환. `LoadBossRush()` 는 레거시 BossRushDemo 전용.
- 씬 전환 전 `Time.timeScale = 1f` 복구 보장.
- 진행도/RunStat은 씬 전환 시 처리: 클리어/사망 후 능동 복귀 → 초기화. 자세한 규칙은 `PROGRESSION.md` 참조.

---

## 2. HUD 구성

### HudView (플레이어 HP/차지/회피 쿨)

- `Rapier_Player.prefab` 내장 **World Space** `PlayerHudCanvas` 에 올라감. 플레이어를 따라다닌다.
- 구성: `HpFill` (Image fillAmount, Horizontal) + `ChargeGaugeFill` (Radial360) + `DodgeCooldownFill` (Vertical).
- **HP 숫자 표기 (Phase 13-A)**: `HpFill` 내부 중앙에 `TextMeshProUGUI _hpText` 배치. **현재 HP 만** 정수로 표시 (`"{currentHp:F0}"`). 최대 HP 는 표시하지 않는다.
- 데이터 소스: `ServiceLocator.Get<IPlayerCharacter>().PublicModel` — `CharacterModel.OnHpChanged(float currentHp)` 이벤트 구독. 이벤트 파라미터가 절대값이므로 그대로 포맷에 사용.
- WorldSpace Canvas 이므로 Safe Area 영향권 밖.

### BossHudView

- ProgressionManager, BossRushManager 등 보스 스폰 드라이버가 공용으로 사용하는 HUD.
- 상단 `BossHpArea` (Screen Space Overlay). 보스 HP 바 + 보스 이름 + 페이즈 + 스테이지 번호.
- **보스 HP 숫자 표기 (Phase 13-A)**: `_bossHpFill` 옆에 `TextMeshProUGUI _bossHpText` 배치. **현재 HP 만** 정수로 표시.
- `EnemyModel.OnHpChanged(float ratio)` 이벤트는 비율만 주므로, View 에서 `_bossModel.CurrentHp` 를 직접 읽어 갱신.
- 결과 패널: ALL CLEAR(노랑) / GAME OVER(빨강).
- `_toLobbyButton` → `SceneController.LoadLobby()`.
- `Init(...)` 메서드로 BossHudSetup이 주입.
- `OnNextStageRequested` 이벤트로 다음 스테이지 요청을 외부에 위임 (HUD 는 매니저 타입을 모른다).
- 공개 메서드: `SetupBoss`, `UpdatePhase`, `ShowVictoryPanel`, `HideVictoryPanel`, `ShowResult`, `HideResultPanel`.

### Safe Area 대응 (Phase 13-A)

모바일 노치/펀치홀/제스처바를 고려해 모든 **Screen Space Overlay Canvas** 에 `SafeAreaFitter` 를 부착한다.

- 신규 컴포넌트: `Scripts/UI/Common/SafeAreaFitter.cs` — `Screen.safeArea` 를 읽어 대상 `RectTransform` 의 anchor 를 매 프레임(또는 해상도 변경 시) 재계산.
- 적용 대상:
  - `BossHudCanvas` (StageDemo, BossRushDemo 공통 — 상단 `BossHpArea` 노치 회피)
  - `[UI]` Canvas (StageDemo — `VirtualJoystick` 하단 제스처바 회피, Intermission/Death/StageClear 팝업 중앙 정렬)
  - `LobbyCanvas`
- **적용 제외**: `PlayerHudCanvas` (World Space, 무관).
- Portrait 고정 프로젝트이므로 회전 처리는 단순화 — 화면 회전 대응 로직 불필요.

### LobbyManager

- 5탭 구조. 화면 하단 가로 버튼으로 탭 전환.
- 탭 인덱스 (1-기준): 1=상점, 2=캐릭터 관리, 3=메인(홈), 4=미션, 5=설정.
- 진입 시 기본 표시 탭은 **3 (메인)**.
- 메인 탭의 하단 시작 버튼 → `SceneController.LoadGame()`.

#### 탭 책임 분리

| 탭 | View | Presenter | 비고 |
|----|------|-----------|------|
| 1 상점 | `ShopTabView` | **미구현** | 가챠/충전/상품 |
| 2 캐릭터 | `CharacterTabView` | `CharacterTabPresenter` | 장비 영역 + 레벨/스킬 영역 (서브 토글) |
| 3 메인 | `HomeTabView` | `HomeTabPresenter` | 스테이지 표시 + 진입 버튼 + 우편함 아이콘(플레이스홀더) |
| 4 미션 | `MissionTabView` | `MissionPanelPresenter` (`Mission/` 서브폴더) | 일일/주간 미션 진행 + 보상 수령. 탭 수준 Presenter 미구현 |
| 5 설정 | `SettingsTabView` | `SettingsTabPresenter` | BGM/SFX/진동/밝기 (PlayerPrefs 임시 구현, TODO B3: SaveManager 전환). 약관 UI 미구현 |

각 탭 Presenter는 LobbyManager가 주입한다. Tab 전환은 LobbyManager가 중재.

### Intermission / Death / StageClear (StageDemo 씬)

| 컴포넌트 | 역할 |
|----------|------|
| `IntermissionManager` + `IntermissionView` | 보스 처치 후 스탯 카드 2장 표시 + 선택 처리 |
| `DeathPopupView` | 사망 시 이어하기 / 로비 복귀 선택 |
| `StageClearView` | 전체 클리어 시 결과 화면 표시 |

- 모두 `[UI]` Canvas (Screen Space Overlay) 에 배치.
- `SafeAreaFitter` 적용 대상 (§Safe Area 참조).

### VirtualJoystick

- Drag 입력 시 가상 조이스틱 표시.

---

## 3. Setup 에디터 툴

| 툴 | 메뉴 |
|----|------|
| BossHudSetup | `Rapier/Boss HUD/Create Boss HUD`, `Rebuild Boss HUD`. 레거시 `BossRushHudCanvas` 도 Rebuild 시 자동 제거. BossRushManager + ProgressionManager 양쪽 발견 시 모두 와이어링. |
| LobbyHudSetup | `Rapier/Lobby/Create Lobby HUD`, `Rebuild Lobby HUD` |

### Setup 툴 작성 시 체크리스트

1. 생성한 컴포넌트의 모든 `[SerializeField]` → `Init()`으로 주입됐는가?
2. 씬 내 다른 컴포넌트가 참조할 필드 → 탐색 후 주입됐는가?
3. EventSystem이 필요한 씬인가? → `EnsureEventSystem()` 호출됐는가?
4. `SetDirty(컴포넌트)` + `MarkSceneDirty` + `SaveScene` 순서 준수.

---

## 4. UI 코드 생성 주의사항

- `CanvasScaler`: 기본 ConstantPixelSize → `ScaleWithScreenSize`, referenceResolution `(1080, 1920)`, Match 0.5 설정.
- `RectTransform`: Anchor와 Pivot을 반드시 일치시킬 것.
- EventSystem 생성 시 `InputSystemUIInputModule` 사용 (StandaloneInputModule 금지).
- `Image.Type.Filled` 사용 시 Sprite 반드시 할당 — None이면 fillAmount 무시됨.
- Screen Space Overlay Canvas 생성 시 `SafeAreaFitter` 부착을 기본으로 고려 (모바일 대응).

---

## 5. UI sortingOrder / Canvas 우선순위 (Phase 25 정합)

| 레이어 | sortingOrder | 비고 |
|---|---|---|
| LobbyCanvas (기본) | 0 | |
| ItemDetailPopup | 200 | 인벤토리 슬롯 탭/롱프레스 진입 |
| EnhanceModal | 300 | 상세 페이지 [강화] 진입, 불투명 배경 |
| DismantleResultModal | 400 | 분해 완료 결과, 불투명 배경 |
| PausePanel | (기존) | 인게임 — 별도 씬 |

뒤로가기/ESC: 가장 위 레이어만 닫는다. EnhanceModal 닫으면 ItemDetailPopup 유지.

---

## 6. 인벤토리 분해 모드 (Phase 25)

### 6-1. EquipmentActionBar

- 인벤토리(스크롤 영역)와 카테고리 3탭(무기/방어구/장신구) 사이에 신규 가로 바 (높이 ~80px).
- 3탭 영역은 그만큼 아래로 이동, 인벤토리 영역 자체는 유지.

레이아웃:
- **기본 상태**: 우측에 `[분해]` 버튼 1개.
- **분해 모드**: 좌측 `[돌아가기]`, 우측 `[일괄 선택 ▼]` `[분해하기]` (왼쪽이 일괄 선택).

### 6-2. 분해 모드 동작

- 진입 시 인벤토리 슬롯 시각화 변경:
  - 미선택 + 미장착: alpha 0.4
  - 선택됨: alpha 1.0 + 등급 색 외곽 테두리 (Outline 또는 별도 Image, 두께 2px)
  - 장착됨: alpha 0.4 + 자물쇠 아이콘 오버레이, 탭/롱프레스 무반응
- **탭**: 분해 선택 토글 (장착 제외)
- **롱프레스 0.5초**: 진행 게이지가 슬롯 위에 원형으로 채워짐. 다 차면 ItemDetailPopup 표시 — 분해 모드 진입 상태에서는 [장착]/[강화]/[해제] 모두 비활성, [닫기]만 활성.

### 6-3. 일괄 선택

- 드롭다운은 액션바 위쪽으로 펼침 (오버레이).
- 항목: 현재 인벤토리 카테고리 탭의 등급 4종 (Normal / Rare / Epic / Unique). 한 줄에 `[● Normal]` 처럼 등급 색 점 + 텍스트.
- 항목 선택 시: 현재 카테고리 탭의 미장착 + 등급 일치 인스턴스를 **기존 선택에 추가** (덮어쓰기 아님). 드롭다운 자동 닫힘.

### 6-4. 분해하기

- 선택 0개면 버튼 비활성 (회색).
- 클릭 시 `EquipmentManager.Dismantle(selected)` → 결과 모달.

### 6-5. DismantleResultModal

- sortingOrder 400, 전체화면 불투명 (배경 darken alpha 0.85).
- 내용:
  - 타이틀: "분해 완료"
  - 본문: "획득: 강화의 가루 ×{N}"
  - 버튼: `[닫기]`
- 닫기 → 모달 + 분해 모드 종료 → 일반 인벤토리.

---

## 7. 강화 UI (Phase 25)

### 7-1. ItemDetailPopup 변경

- 기존 2버튼 (`[장착/해제]` `[닫기]`) → 3버튼 (`[장착/해제]` `[강화]` `[닫기]`) 가로 균등 분할.
- `[강화]` 비활성 조건:
  - 인스턴스 `EnhanceLevel == MaxEnhanceLevel` (등급 최대)
  - 분해 모드에서 진입한 상세 페이지 (전체 비활성 정책)
- **표기 정책**:
  - 아이템 이름은 `EnhanceLevel > 0` 시 `"{ItemName} +{N}"` 로 표기. 0 이면 접미사 생략.
  - 메인 스탯은 `FormatStatEntryWithEnhance` 로 강화 배율 `(1 + 0.10 × EnhanceLevel)` 적용 후 표시 — UI 표시값과 실제 계산값이 일치해야 한다 (Phase 25 폴리시 사고 재발 방지).
  - `%` 표기: SO 가 0~100 컨벤션이므로 포맷 문자열은 `:F1}%` (직접 % 부호 부착) 사용. `:F1%` (×100 자동변환) 금지.

### 7-2. EnhanceModal

- sortingOrder 300, 전체화면 불투명 (darken alpha 0.85).
- 레이아웃은 ItemDetailPopup 기반, 설명 영역만 강화 정보로 교체.

표시 항목 (위에서부터):
1. 아이템 아이콘 + 이름 + 등급 색
2. **현재 강화: +N → +(N+1)** (성공 시 가정)
3. **메인 스탯**: `{statType} {currentValue:0.##} → {nextValue:0.##} (+{delta:0.##})`
4. **서브스탯 강화 단계!** — 목표가 3·6·9·12·15 일 때만 강조 표시 (등급 색 또는 노란 강조)
5. **성공 확률: {percent}%**
6. **필요 가루: {cost} / 보유 {dust}** — 부족 시 보유 수치 빨간색

버튼 (하단 가로 2등분):
- `[강화하기]` — 가루 부족 시 비활성 (회색)
- `[닫기]` — 항상 활성, 닫으면 ItemDetailPopup 으로 복귀

### 7-3. 강화 연출

`OnEquipmentEnhanced` 이벤트 수신 시 모달이 직접 연출:

- **성공**: 풀스크린 플래시(등급 색, 0.5초) + 파편 5개(0.7초) + 토스트 "강화 성공!"(1.0초) 를 **병렬 실행** — 총 지속 1.0초로 압축. 이후 데이터 자동 갱신.
- **실패**: 모달 좌우 흔들림 (±10px, 0.3초) + 회색 플래시 (0.3초) + 토스트 "강화 실패..." 병렬. 가루/확률만 갱신.

연출 중 `[강화하기]` 버튼 일시 비활성 (중복 호출 방지).

서브스탯 강화 발동 (3/6/9/12/15) 성공 시: **결과 표기 모달은 추가 안 함**. 대신 닫기 후 다음 모달 호출 시 갱신된 서브 영역에서 강조 색으로 표시 (별도 정책 — 사용자 결정: "위쪽 서브 스탯 강조"). 강조 표시 방식: 새로 강화된 서브 항목의 텍스트를 1.5초간 노란색 펄스.

### 7-4. 가루 표시 (로비 공통)

- 캐릭터 탭 상단 또는 액션바 영역에 보유 가루 표시 (위치는 작업 에이전트 재량 — 캐릭터 패널 이미 47:33:20 비율 확정이라 EquipmentActionBar 좌측 끝에 작은 텍스트 권장).
- `EquipmentManager.OnDustChanged` 구독으로 즉시 갱신.
