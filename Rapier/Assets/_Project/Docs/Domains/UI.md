# UI 시스템 (UI)

---

## 1. 씬 구조

| 씬 | 역할 |
|----|------|
| Lobby | 5탭 로비. 메인/캐릭터/상점/미션/설정. |
| BossRushDemo | 스테이지 진행 씬. 보스방 + 인터미션 방. |

- `SceneController.LoadLobby()` / `LoadGame()` 으로 전환.
- 씬 전환 전 `Time.timeScale = 1f` 복구 보장.
- 진행도/RunStat은 씬 전환 시 처리: 클리어/사망 후 능동 복귀 → 초기화. 자세한 규칙은 `PROGRESSION.md` 참조.

---

## 2. HUD 구성

### BossRushHudView

- HP바, 차지 게이지, 보스 HP바.
- 결과 패널: ALL CLEAR(노랑) / GAME OVER(빨강).
- `_toLobbyButton` → `SceneController.LoadLobby()`.
- `Init(...)` 메서드로 BossRushHudSetup이 주입.

### LobbyManager

- 5탭 구조. 화면 하단 가로 버튼으로 탭 전환.
- 탭 인덱스 (1-기준): 1=상점, 2=캐릭터 관리, 3=메인(홈), 4=미션, 5=설정.
- 진입 시 기본 표시 탭은 **3 (메인)**.
- 메인 탭의 하단 시작 버튼 → `SceneController.LoadGame()`.

#### 탭 책임 분리

| 탭 | View | Presenter | 비고 |
|----|------|-----------|------|
| 1 상점 | `ShopTabView` | `ShopTabPresenter` | 가챠/충전/상품 |
| 2 캐릭터 | `CharacterTabView` | `CharacterTabPresenter` | 장비 영역 + 레벨/스킬 영역 (서브 토글) |
| 3 메인 | `HomeTabView` | `HomeTabPresenter` | 스테이지 표시 + 진입 버튼 + 우편함 아이콘 |
| 4 미션 | `MissionTabView` | `MissionTabPresenter` | 일일/주간 미션 진행 + 보상 수령 |
| 5 설정 | `SettingsTabView` | `SettingsTabPresenter` | 옵션/계정/약관 |

각 탭 Presenter는 LobbyManager가 주입한다. Tab 전환은 LobbyManager가 중재.

### VirtualJoystick

- Drag 입력 시 가상 조이스틱 표시.

---

## 3. Setup 에디터 툴

| 툴 | 메뉴 |
|----|------|
| BossRushHudSetup | 자동: BossRushManager._hudView 연결 + EventSystem 생성 |
| LobbyHudSetup | `Rapier/Lobby/Create Lobby HUD`, `Rebuild Lobby HUD` |

### Setup 툴 작성 시 체크리스트

1. 생성한 컴포넌트의 모든 `[SerializeField]` → `Init()`으로 주입됐는가?
2. 씬 내 다른 컴포넌트가 참조할 필드 → 탐색 후 주입됐는가?
3. EventSystem이 필요한 씬인가? → `EnsureEventSystem()` 호출됐는가?
4. `SetDirty(컴포넌트)` + `MarkSceneDirty` + `SaveScene` 순서 준수.

---

## 4. UI 코드 생성 주의사항

- `CanvasScaler`: 기본 ConstantPixelSize → `ScaleWithScreenSize`, referenceResolution `(1080, 1920)` 설정.
- `RectTransform`: Anchor와 Pivot을 반드시 일치시킬 것.
- EventSystem 생성 시 `InputSystemUIInputModule` 사용 (StandaloneInputModule 금지).
- `Image.Type.Filled` 사용 시 Sprite 반드시 할당 — None이면 fillAmount 무시됨.
