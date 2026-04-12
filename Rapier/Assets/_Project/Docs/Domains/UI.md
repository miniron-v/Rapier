# UI 시스템 (UI)

---

## 1. 씬 구조

| 씬 | 역할 |
|----|------|
| Lobby | 5탭 로비. 메인/캐릭터/상점/미션/설정. |
| StageDemo | 스테이지 진행 씬. 8방 RoomNode(인터미션/보스 교차). 포탈 시스템 기반. |
| BossRushDemo | 보스 러시 씬. `BossRushManager` 가 공용 `BossHudView` 에 연결되어 동작 (Phase 13-A 통합). 12-C 신규 보스 배열 등록은 Phase 12-E 대기. |

- `SceneController.LoadLobby()` / `LoadGame()` 으로 전환. (`LoadStageDemo()` → `LoadGame()` 리네임 예정, BossRushDemo 레거시 삭제 예정)
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
- 메인 탭의 하단 시작 버튼 → `SceneController.LoadGame()`. (현재 코드는 `LoadStageDemo()` — 리네임 예정)

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
