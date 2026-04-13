# Rapier — 프로젝트 핵심 규칙

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 장르 | 싱글 플레이 실시간 액션 RPG |
| 플랫폼 | 모바일 (Android / iOS), PC 테스트 지원 |
| 화면 | 세로 모드 (Portrait) |
| 렌더 | URP 2D |
| 엔진 | Unity (New Input System, .NET Standard 2.1) |
| 조작 | 전체 화면, 단일 손가락(엄지) / PC 마우스 |

## 2. 아키텍처

### MVP

`Model ↔(Interface)↔ Presenter ↔(직접참조)↔ View`

- **Model**: 순수 데이터/상태. MonoBehaviour 금지. SO 또는 순수 C#.
- **View**: 화면 표시만. 로직/위치 결정 금지 — Presenter가 계산 후 `View.SetPosition()` 전달.
- **Presenter**: Model-View 중재, 게임 로직 핵심. MonoBehaviour.

### DI / SOLID

- 수동 DI: `Init()`로 의존성 명시 주입. 전역은 ServiceLocator (남용 금지).
- 자식 고유 상태는 자식 안에서만. Base 결합은 `virtual`/`override` 계약으로만.

| 원칙 | 적용 |
|------|------|
| SRP | 클래스 하나 = 책임 하나 |
| OCP | 새 캐릭터 추가 시 기존 코드 수정 없이 확장 |
| LSP | 자식은 부모를 완전히 대체 가능 |
| ISP | IAttackable, IDodgeable 등 작은 인터페이스 |
| DIP | Presenter는 IView 인터페이스에 의존 |

### 금지 패턴

Singleton 남용 · View에서 로직 처리 · `GameObject.Find()` · `SendMessage()`

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

Serialized → Private → Properties → Unity Lifecycle (Awake→OnEnable→Start→Update→OnDisable→OnDestroy) → Public → Private → Event Handlers (`On~`) → `#if UNITY_EDITOR`

### 주석

- 공개 API: XML 주석 `///` 필수
- 복잡한 로직: 의도 설명 인라인 주석
- 금지: 코드를 그대로 설명하는 주석

## 4. 네임스페이스

형식: `Game.[시스템명]` (예: `Game.Core`, `Game.Characters.Warrior`)

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

## 5. 폴더 구조

```
Assets/
├── Rapier-Private/          # 비공개 (Art, Audio, ThirdParty)
└── _Project/                # 공개 저장소
    ├── Docs/                # CLAUDE.md, TEAM_LEAD.md, Domains/, Editor/(일회성 툴)
    ├── Scripts/             # Characters/, Combat/, Core/, Data/, DevTools/, Enemies/, Input/, UI/
    ├── Prefabs/             # Boss/, Enemies/, Player/
    ├── ScriptableObjects/   # Characters/, Enemies/, Equipment/, Fonts/, Missions/, Skills/, Stats/
    └── Scenes/              # Lobby, StageDemo, BossRushDemo 등
```

모든 에셋은 `_Project/` 하위. 폴더명 숫자 prefix 금지.

## 6. 이벤트 통신

| 상황 | 방식 |
|------|------|
| Presenter ↔ View 계약 | C# Interface |
| 시스템 간 통신 | C# event |
| 전역 단일 접근점 | ServiceLocator (남용 금지) |

- 구독 `OnEnable` / 해제 `OnDisable` 반드시 쌍
- 핸들러 이름: `Handle + 동사` (예: `HandleTapPerformed`)

## 7. ScriptableObject

- 캐릭터 스탯, 스킬 설정값은 SO로 분리
- menuName: `Game/Data/[카테고리]/[이름]`
- 외부 노출은 읽기 전용 프로퍼티 `=>` 만, setter 금지
- SO 값은 런타임 불변. 가변 계산값은 `[NonSerialized]` 필드에 캐싱

## 8. 런타임 주의사항

- `AssetDatabase`는 에디터 전용 — 런타임 MonoBehaviour 사용 금지
- 런타임 Sprite: `Texture2D` 직접 생성 / `Resources.Load` / SO 레퍼런스
- 런타임 파일에 `#if UNITY_EDITOR` 분기가 있으면 즉시 의심

## 9. 피드백 루프 (커밋 전 필수)

1. **컴파일 검증** — 오류 없으면 통과, 있으면 즉시 수정 후 재검증
2. **코드 리뷰 체크리스트** (아래 전부)
3. **자체 트레이스** — 정상/경계 경로, null·무한루프·이벤트 미해제 점검, 인터페이스 연결부 확인
4. **커밋** — 1~3 통과 시에만, 메시지는 "왜" 중심

체크리스트:
- [ ] 네임스페이스 올바른가
- [ ] View에 로직 없는가 (이동 계산 포함)
- [ ] Presenter가 IView 인터페이스로 통신하는가
- [ ] 이벤트 구독/해제 OnEnable/OnDisable 쌍인가
- [ ] 공개 API XML 주석 있는가
- [ ] `Find()`, `SendMessage()` 미사용인가
- [ ] SO 데이터 외부 노출은 읽기 전용 프로퍼티인가
- [ ] 새 캐릭터/적 추가 시 기존 코드 수정 불필요한가 (OCP)
- [ ] 자식 고유 상태가 Base에 노출되지 않는가 (DIP/OCP)
- [ ] 런타임 가변값이 `[NonSerialized]` 필드 캐싱인가
- [ ] **`UnityEngine.Input`(구) 미사용, `UnityEngine.InputSystem`만 사용했는가**
- [ ] **단발성 에디터 메뉴(`[MenuItem]`) — 사용·테스트 완료 시 머지 직전 삭제했는가** (재사용 Create/Rebuild 페어는 유지)

## 10. 역할 분기

- **특정 기능 구현 지시**: 해당 작업만 집중, 프롬프트 명시 도메인 문서(`Assets/_Project/Docs/Domains/*.md`) 참조
- **그 외**: `Assets/_Project/Docs/TEAM_LEAD.md` 읽고 팀장 역할 수행

## 11. Bash / 터미널 운영 규칙

모든 인스턴스(팀장·작업 에이전트) 공통.

- 작업은 현재 루트 폴더에서 수행, 파일 접근은 `./...` 상대경로
- git은 반드시 `git -C "<절대경로>" <subcommand>` 형태 — `cd` 사용 금지
- 파일 도구(Read / Write / Edit / Grep / Glob)는 절대경로

## 12. 백그라운드 Agent 워크플로우

Phase 단위 작업은 팀장 세션의 `Agent` 도구를 `run_in_background: true` + `model: "sonnet"` 로 병렬 진행.

### 팀장 세션 단계

1. 도메인 문서 + 탐색 에이전트(`subagent_type: "Explore"`, Read-only)로 현 상태 조사
2. 사용자와 기획 논의 → 합의 → 도메인 문서 갱신
3. 워크트리 생성 (`git worktree add`, 병렬마다)
4. Rapier-Private Junction 연결 (비공개 에셋 필요 시)
5. `Agent` 실행 — `general-purpose` / `sonnet` 명시 / `run_in_background: true` / 자체완결 프롬프트
6. 완료 알림 후 §9 체크리스트 + 시나리오 트레이스 직접 검토
7. 사용자 직접 플레이 테스트 안내
8. develop ff-only 머지

### 구현 에이전트 규칙

- 워크트리 내 **커밋까지만** 수행
- **금지**: push / merge / worktree remove / --amend / --no-ff / --no-verify / force push
- 다른 병렬 Phase 워크트리 절대 미접근
- 사용자 실시간 개입 불가 → 프롬프트 자체완결 필수

### 초기 프롬프트 필수 포함 항목

- 워크트리 절대경로 및 브랜치
- §11 Bash 운영 규칙 인용 (cd 금지, `git -C` 사용)
- 작업 목표 (합의된 기획 원문 인용, 추측 금지)
- 참조 문서 + 읽는 순서 (CLAUDE.md → `Domains/*.md` → DesignDoc)
- 현재 구현 상태 고지 (예: "Rapier 1종만 구현, Warrior/Assassin/Ranger 미존재")
- 수정 허용/금지 폴더 (병렬 Phase 충돌 방지)
- §9 피드백 루프 지시 (컴파일 / 체크리스트 / 트레이스)
- 잠금·플래그 짝 grep 검증 지시 — 모든 `Begin*` / `Lock*` / `SetXxx(true)` / `StartCoroutine` 의 해제가 정상/취소/사망/OnDisable 모든 종료 경로에 있는지
- C# 정적 문법 grep 검증 — 누락 using, 접근 불가 멤버, 튜플 요소 수 등 (Unity 실행 불가 환경)
- "사용자 직접 플레이 테스트가 최종 검증" 명시
- 커밋 형식: `[Phase 13-X] 한국어 설명` (본문에 근본 원인 + 수정 위치 + 검증 결과)
- push/merge/worktree remove/--amend/--no-verify/force push 금지 명시
- 보고 형식: 200~400자 (수정 요지 / 트레이스 / 잠금-해제 매핑 / 커밋 SHA)

## 13. 과거 실수 기록 (Lessons Learned)

### 워크플로우

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-01 | **원인 미확정 시 코드 수정 금지.** 에디터로 직접 확인 가능한 사항(레이어/콜라이더/스프라이트 할당 등)은 사용자에게 먼저 질문, 원인 확정 후에만 코드 수정 | 버그 수정 착수 전 |
| L-02 | **승인 없이 작업 착수 금지.** 설계/분석 완료해도 보고 → 합의 → 착수 순서 준수 | 모든 작업 |

### 아키텍처 / SOLID

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-03 | **자식 고유 상태를 Base에서 참조 금지 (OCP/DIP).** `_isDashSkillActive` 류 상태는 자식 내부에서만 처리. Base 결합은 `virtual`/`override` 계약(`CanAttack` 등)만 | 캐릭터 Presenter 수정 시 |
| L-04 | **단일 플래그로 복수 상태 억제 → 영구 잠금.** 스킬 대기/진행 미구분 시 일반 회피에서 `OnDodgeDashComplete` 억제 → 잠금. 억제 조건은 실제 진행 중 상태만 | 잠금/플래그 코드 수정 시 |
| L-05 | **AnimationCurve 끝값 0 → 무한 루프.** `MoveTowards` 이동량 0 수렴, `ARRIVE_THRESHOLD` 도달 불가. 끝값 0.50f 이상 + `DodgeDashRoutine`에 MinSpeed/타임아웃 보증 | AnimationCurve 설정 시 |

### Unity 에디터 / UI 코드 생성

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-06 | **Filled Image에 Sprite 필수.** `sprite=None` + `Type.Filled` 시 `fillAmount` 무시. Radial360은 Circle 없으면 사각형. 코드 생성 시 TIP 스프라이트 동시 할당 | HudSetup 등 UI 코드 생성 시 |
| L-07 | **CanvasScaler 기본 ConstantPixelSize 주의.** Device Simulator에서 UI 작게 보임. `ScaleWithScreenSize`, `referenceResolution=(1080,1920)` 설정 | Canvas 코드 생성 시 |
| L-08 | **RectTransform Pivot 기본(0.5,0.5) 주의.** `anchoredPosition`은 pivot 기준. Anchor와 Pivot 일치 필수 | UI 코드 생성 시 |
| L-09 | **EventSystem은 InputSystemUIInputModule 사용.** New Input System 환경에서 `StandaloneInputModule` 시 `UnityEngine.Input.get_mousePosition()` 런타임 에러 | EventSystem 생성 시 |
| L-10 | **2D Sprite 내장 경로**(에디터 전용): `Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/` (Square/Circle/Capsule/Triangle/9Sliced/HexagonFlatTop). `AssetDatabase.LoadAssetAtPath<Sprite>()` | 에디터 스크립트에서 스프라이트 필요 시 |

### 런타임 제약

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-11 | **런타임 `AssetDatabase` 금지.** 에디터 전용. 런타임 스프라이트는 `Texture2D` 생성 / `Resources.Load` / SO 레퍼런스 (§8) | 런타임 MonoBehaviour 작성 시 |
| L-12 | **Editor 폴더에 런타임 컴포넌트 금지.** `Editor/` 스크립트는 씬 부착 불가. 씬 부착물은 `DevTools/` 등 Editor 바깥 | 새 스크립트 생성 시 |
| L-13 | **UnityEngine 내장과 네임스페이스 충돌 금지.** `namespace Game.Debug` → `UnityEngine.Debug`와 충돌. DevTools 계열은 `Game.DevTools` 사용 (§4) | 네임스페이스 명명 시 |

### 밸런스 / SO 설정

| # | 교훈 | 적용 시점 |
|---|------|-----------|
| L-14 | **`ChargeRequiredTime` ≥ 1.0f 권장.** Hold 이벤트는 판정 직후부터. 값 짧으면 최초 수신 시 duration 초과 → 차지 즉시 1 표시 | CharacterStatData SO 값 설정 시 |
