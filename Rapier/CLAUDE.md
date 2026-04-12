# Rapier — 프로젝트 핵심 규칙

> 이 파일은 Claude Code가 자동으로 로드합니다. 모든 에이전트는 이 규칙을 준수해야 합니다.

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 장르 | 싱글 플레이 실시간 액션 RPG |
| 플랫폼 | 모바일 (Android / iOS), PC 테스트 지원 |
| 화면 | 세로 모드 (Portrait) |
| 렌더 | URP 2D |
| 엔진 | Unity (New Input System, .NET Standard 2.1) |
| 조작 | 전체 화면, 단일 손가락(엄지) / PC 마우스 |

---

## 2. 아키텍처

### MVP 패턴

```
[Model] <--Interface--> [Presenter] <--직접참조--> [View]
```

- **Model**: 순수 데이터/상태. MonoBehaviour 금지. SO 또는 순수 C# 클래스.
- **View**: 화면 표시만. 로직 금지. 위치 결정 금지 — Presenter가 계산 후 `View.SetPosition()`으로 전달.
- **Presenter**: Model-View 중재. 게임 로직의 핵심. MonoBehaviour.

### DI 전략

- 수동 DI: `Init()` 메서드로 의존성 명시 주입.
- 전역 시스템: ServiceLocator 허용 (남용 금지).

### SOLID 원칙

| 원칙 | 적용 |
|------|------|
| SRP | 클래스 하나 = 책임 하나 |
| OCP | 새 캐릭터 추가 시 기존 코드 수정 없이 확장 |
| LSP | 자식은 부모를 완전히 대체 가능 |
| ISP | IAttackable, IDodgeable 등 작은 인터페이스 |
| DIP | Presenter는 IView 인터페이스에 의존 |

자식 고유 상태는 자식 안에서만 처리. Base와의 결합은 virtual/override 계약으로만 수행.

### 금지 패턴

- Singleton 남용, View에서 로직 처리, `GameObject.Find()`, `SendMessage()`

---

## 3. 코딩 컨벤션

### 명명 규칙

| 대상 | 규칙 | 예시 |
|------|------|------|
| 클래스 | PascalCase | `PlayerPresenter` |
| 인터페이스 | I + PascalCase | `ICharacterView` |
| Public 메서드 | PascalCase | `TakeDamage()` |
| Private 필드 | _camelCase | `_currentHP` |
| SerializeField | _camelCase | `_moveSpeed` |
| 상수 | UPPER_SNAKE_CASE | `MAX_CHARGE_TIME` |
| 이벤트 | On + PascalCase | `OnTapPerformed` |
| SO 클래스 | PascalCase + Data/Config | `WarriorData` |
| Enum 값 | PascalCase | `InputState.Move` |

### 파일 내부 순서

1. Serialized Fields → 2. Private Fields → 3. Properties
4. Unity Lifecycle (Awake→OnEnable→Start→Update→OnDisable→OnDestroy)
5. Public Methods → 6. Private Methods → 7. Event Handlers (On~)
8. `#if UNITY_EDITOR` 블록

### 주석

- 공개 API: XML 문서 주석 `///` 필수
- 복잡한 로직: 인라인 주석으로 의도 설명
- 금지: 코드를 그대로 설명하는 주석

---

## 4. 네임스페이스

형식: `Game.[시스템명]` — 예: `Game.Core`, `Game.Characters.Warrior`

| 네임스페이스 | 폴더 |
|-------------|------|
| Game.Core | Scripts/Core/ |
| Game.Input | Scripts/Input/ |
| Game.Combat | Scripts/Combat/ |
| Game.Characters | Scripts/Characters/ |
| Game.Characters.[이름] | Scripts/Characters/[이름]/ |
| Game.Enemies | Scripts/Enemies/ |
| Game.UI | Scripts/UI/ |
| Game.Data | Scripts/Data/ |

---

## 5. 폴더 구조

```
Assets/
├── Rapier-Private/          # 비공개 (Art, Audio, ThirdParty)
└── _Project/                # 공개 저장소
    ├── Docs/                # 문서 (CLAUDE.md, TEAM_LEAD.md, Domains/, Editor/ 일회성 툴)
    ├── Scripts/             # 스크립트
    │   ├── Characters/      # Base/, Rapier/
    │   ├── Combat/          # 전투 공용 인터페이스
    │   ├── Core/            # Camera/, Interfaces/, Scene/, Services/, Stage/
    │   ├── Data/            # Characters/, Enemies/, Equipment/, Missions/, Save/, Stats/
    │   ├── DevTools/        # 런타임 디버그/셋업 유틸
    │   ├── Enemies/         # Base/, Attacks/, Managers/, Boss/
    │   ├── Input/
    │   └── UI/              # HUD/, Intermission/, Lobby/
    ├── Prefabs/             # Boss/, Enemies/, Player/
    ├── ScriptableObjects/   # SO 에셋 (Characters/, Enemies/, Equipment/, Fonts/, Missions/, Skills/, Stats/)
    └── Scenes/              # 씬 (Lobby, StageDemo, BossRushDemo 등)
```

모든 프로젝트 에셋은 `_Project/` 하위에 위치. 폴더명에 숫자 prefix는 쓰지 않는다.

---

## 6. 이벤트 통신

| 상황 | 방식 |
|------|------|
| Presenter ↔ View 계약 | C# Interface |
| 시스템 간 통신 | C# event |
| 전역 단일 접근점 | ServiceLocator (남용 금지) |

- 이벤트 구독: `OnEnable`, 해제: `OnDisable`에서 반드시 쌍으로 처리
- 핸들러 이름: `Handle + 동사` (예: `HandleTapPerformed`)

---

## 7. ScriptableObject 규칙

- 캐릭터 스탯, 스킬 설정값은 SO로 분리
- menuName: `Game/Data/[카테고리]/[이름]`
- 외부에는 읽기 전용 프로퍼티 `=>` 만 노출. setter 금지.
- SO 값은 런타임 불변. 가변 계산값은 `[NonSerialized]` 필드에 캐싱.

---

## 8. 런타임 주의사항

- `AssetDatabase`는 에디터 전용 API. 런타임 MonoBehaviour에서 사용 금지.
- 런타임 Sprite: `Texture2D` 직접 생성 / `Resources.Load` / SO 레퍼런스로 조달.
- `#if UNITY_EDITOR` 분기가 런타임 파일에 있으면 즉시 의심할 것.

---

## 9. 피드백 루프 (자체 검증)

모든 코드 작성/수정 후, 커밋 전에 반드시 아래 단계를 순서대로 수행한다.

### Step 1: 컴파일 검증

- 작성한 스크립트가 컴파일 오류 없이 통과하는지 확인한다.
- 오류 발생 시 즉시 수정 후 재검증.

### Step 2: 코드 리뷰 체크리스트

- [ ] 네임스페이스가 올바르게 지정되었는가?
- [ ] View에 로직이 없는가? (이동 계산 포함)
- [ ] Presenter가 IView Interface를 통해 View와 통신하는가?
- [ ] 이벤트 구독/해제가 OnEnable/OnDisable에 쌍으로 있는가?
- [ ] 공개 API에 XML 문서 주석이 있는가?
- [ ] Find(), SendMessage()를 사용하지 않았는가?
- [ ] SO 데이터는 읽기 전용 프로퍼티로만 외부 노출하는가?
- [ ] 새 캐릭터/적 추가 시 기존 코드를 수정하지 않아도 되는가? (OCP)
- [ ] 자식 고유 상태가 Base에 노출되지 않는가? (DIP/OCP)
- [ ] 런타임 가변값이 [NonSerialized] 필드에 캐싱되어 있는가?
- [ ] **`UnityEngine.Input`(구 입력 시스템)을 사용하지 않았는가? New Input System(`UnityEngine.InputSystem`)만 허용.**

### Step 3: 자체 테스트

- 자신이 작성한 코드의 주요 경로(정상 흐름, 경계 조건)를 머릿속으로 트레이스한다.
- null 참조, 무한 루프, 이벤트 미해제 등 흔한 버그 패턴을 점검한다.
- 기존 시스템과의 연결부(이벤트, 인터페이스)가 정상적으로 동작하는지 확인한다.

### Step 4: 커밋

- Step 1~3을 모두 통과한 경우에만 커밋한다.
- 커밋 메시지는 변경의 "왜"를 중심으로 간결하게 작성한다.

---

## 10. 역할 분기

- **특정 기능 구현을 지시받았다면**: 해당 작업에만 집중하세요. 프롬프트에 명시된 도메인 문서(`Assets/_Project/Docs/Domains/*.md`)를 참조하세요.
- **그 외의 경우**: `Assets/_Project/Docs/TEAM_LEAD.md`를 읽고 프로젝트 팀장 역할을 수행하세요.

---

## 11. Bash / 터미널 운영 규칙

모든 Claude 인스턴스(팀장·작업 에이전트 공통)에 적용한다.

- 모든 작업은 현재 위치한 루트 폴더에서 수행하고, 파일에 접근할 때는 반드시 ./... 형태의 상대 경로만 사용해.
- **git은 반드시 `git -C "<절대경로>" <subcommand>` 형태**로 호출.
  - 나쁜 예: `cd C:/GitHub/Rapier && git status`
  - 좋은 예: `git -C "C:/GitHub/Rapier" status`
- 파일 도구(Read / Write / Edit / Grep / Glob)도 절대 경로 사용.

---

## 12. 백그라운드 Agent 에이전트 워크플로우

Phase 단위 작업은 팀장 세션의 `Agent` 도구를 `run_in_background: true` + `model: "sonnet"` 로 띄워 병렬 진행한다.

### 팀장 세션 역할
1. 관련 도메인 문서 + 탐색 에이전트(`subagent_type: "Explore"`, Read-only)로 현 상태 조사
2. 사용자와 기획 논의 → 합의 → 관련 도메인 문서 갱신
3. 워크트리 생성 (`git worktree add`, 각 병렬 작업마다)
4. Rapier-Private 폴더 Junction 연결 (비공개 에셋 필요 시)
5. `Agent` 도구로 구현 에이전트 실행
   - `subagent_type: "general-purpose"`
   - `model: "sonnet"` (반드시 명시, 비용 관리)
   - `run_in_background: true`
   - 자체완결적 프롬프트 (아래 필수 포함 항목)
6. 완료 알림 수신 후 §9 체크리스트 + 시나리오 트레이스 직접 검토
7. 사용자 직접 플레이 테스트 안내
8. develop ff-only 머지

### 구현 에이전트 역할
- 워크트리 내에서 **커밋까지만** 수행
- **금지**: push / merge / worktree remove / --amend / --no-ff / --no-verify / force push
- 사용자 실시간 개입이 어려우므로 프롬프트가 반드시 완결적이어야 한다
- 다른 병렬 Phase 의 워크트리는 절대 건드리지 않음

### 초기 프롬프트 필수 포함 항목
- 워크트리 절대 경로 및 브랜치
- `§11 Bash 운영 규칙` 인용 (cd 금지, `git -C` 사용)
- 작업 목표 (합의된 기획 원문 인용, 추측 금지)
- 참조 문서 목록 + 읽는 순서 (CLAUDE.md → 관련 `Domains/*.md` → DesignDoc)
- 현재 구현 상태 고지 (예: "Rapier 1종만 구현됨. Warrior/Assassin/Ranger 클래스는 미존재")
- 수정 허용 폴더 / **수정 금지 폴더** (병렬 Phase 충돌 방지)
- §9 피드백 루프 지시 (컴파일 검증 / 코드 리뷰 체크리스트 / 자체 트레이스)
- 잠금·플래그 짝 grep 검증 지시 (모든 `Begin*` / `Lock*` / `SetXxx(true)` / `StartCoroutine` 에 대응하는 해제가 정상/취소/사망/OnDisable 모든 종료 경로에 있는지)
- C# 정적 문법 grep 검증 지시 (Unity 실행 불가 환경이므로 누락 using, 접근 불가 멤버, 튜플 요소 수 등 사전 점검)
- "사용자 직접 플레이 테스트가 최종 검증 단계" 명시
- 커밋 형식: `[Phase 13-X] 한국어 설명` (본문에 근본 원인 + 수정 위치 + 검증 결과)
- **push / merge / worktree remove / --amend / --no-verify / force push 금지** 명시
- 보고 형식: 200~400자 (수정 요지 / 시나리오 트레이스 / 잠금-해제 매핑 표 / 커밋 SHA)

---

## 13. 과거 실수 기록 (Lessons Learned)

프로젝트 역사에서 실제 발생한 버그와 교훈. 모든 에이전트는 해당 상황에서 이 목록을 참조한다.

### 워크플로우

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-01 | **원인 미확정 시 코드 수정 금지.** 에디터에서 직접 확인 가능한 사항(레이어, 콜라이더, 스프라이트 할당 등)은 사용자에게 먼저 질문하고, 원인이 확정된 후에만 코드를 수정한다. | 버그 수정 착수 전 |
| L-02 | **승인 없이 작업 착수 금지.** 설계/분석이 완료되더라도 반드시 보고 → 합의 → 착수 순서를 준수한다. | 모든 작업 |

### 아키텍처 / SOLID

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-03 | **자식 고유 상태를 Base에서 참조 금지 (OCP/DIP).** 자식의 `_isDashSkillActive` 같은 상태에 의존하는 로직은 자식 안에서만 처리. Base와의 결합은 `virtual`/`override` 계약(`CanAttack` 등)으로만 수행. | 캐릭터 Presenter 수정 시 |
| L-04 | **단일 플래그로 복수 상태 억제 → 영구 잠금 위험.** 스킬 대기 상태와 스킬 진행 상태를 구분하지 않으면 일반 회피에서도 `OnDodgeDashComplete`가 억제되어 영구 잠금 발생. 억제 조건은 실제 진행 중인 상태만으로 판단할 것. | 잠금/플래그 코드 수정 시 |
| L-05 | **AnimationCurve 끝값 0 → 무한 루프.** `MoveTowards` 이동량이 0에 수렴하여 `ARRIVE_THRESHOLD`에 도달 불가. 속도 배율 커브 끝값은 0.50f 이상 유지. `DodgeDashRoutine`에 MinSpeed 보증 + 타임아웃 포함. | AnimationCurve 설정 시 |

### Unity 에디터 스크립트 / UI 코드 생성

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-06 | **Filled Image에 Sprite 필수 할당.** `sprite=None` 상태에서 `Image.Type.Filled` 설정 시 `fillAmount`가 완전 무시됨. Radial360은 Circle 없으면 사각형 렌더링. 코드로 Image 생성 시 TIP 스프라이트를 반드시 동시 할당. | HudSetup/LobbyHudSetup 등 UI 코드 생성 시 |
| L-07 | **CanvasScaler 기본값 주의.** 기본 `ConstantPixelSize` → Device Simulator에서 UI가 작게 보임. `ScaleWithScreenSize`, `referenceResolution = (1080, 1920)` 설정 필요. | Canvas 코드 생성 시 |
| L-08 | **RectTransform Pivot 기본값(0.5, 0.5) 주의.** `anchoredPosition`은 pivot 기준 계산. Anchor와 Pivot을 반드시 일치시킬 것. | UI 코드 생성 시 |
| L-09 | **EventSystem은 InputSystemUIInputModule 사용.** New Input System 환경에서 `StandaloneInputModule` 사용 시 `UnityEngine.Input.get_mousePosition()` 런타임 에러 발생. | EventSystem 생성 시 |
| L-10 | **2D Sprite 내장 경로** (에디터 전용): `Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/` — Square / Circle / Capsule / Triangle / 9Sliced / HexagonFlatTop 등. `AssetDatabase.LoadAssetAtPath<Sprite>()` 로 로드. | 에디터 스크립트에서 스프라이트 필요 시 |

### 런타임 제약

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-11 | **런타임에서 `AssetDatabase` 사용 금지.** 에디터 전용 API. 런타임 스프라이트는 `Texture2D` 직접 생성 / `Resources.Load` / SO 레퍼런스로 조달. (§8 참조) | 런타임 MonoBehaviour 작성 시 |
| L-12 | **Editor 폴더에 런타임 컴포넌트 배치 금지.** `Editor/` 하위 스크립트는 씬에 부착 불가. 씬 부착 컴포넌트는 `DevTools/` 등 Editor 바깥에 배치. | 새 스크립트 생성 시 |
| L-13 | **UnityEngine 내장 클래스명과 네임스페이스 충돌 금지.** `namespace Game.Debug` → `UnityEngine.Debug`와 충돌. DevTools 계열은 `Game.DevTools` 사용. (§4 참조) | 네임스페이스 명명 시 |

### 밸런스 / SO 설정

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-14 | **`ChargeRequiredTime` ≥ 1.0f 권장.** Hold 이벤트는 판정 직후부터 발생. 값이 짧으면 최초 이벤트 수신 시점에 이미 duration 초과 → 차지가 즉시 1로 표시됨. | CharacterStatData SO 값 설정 시 |
