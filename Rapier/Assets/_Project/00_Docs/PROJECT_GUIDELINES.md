# 프로젝트 개발 지침서 (Project Guidelines)

> **버전**: v0.1.0
> **최초 작성일**: 2026-03-05
> **목적**: 본 문서는 프로젝트 전반의 아키텍처, 코딩 컨벤션, 폴더 구조, 협업 규칙을 정의합니다.
> 모든 개발자(인간 및 AI)는 코드 작성 전 반드시 이 문서를 숙지하고, 작업 시 지침으로 삼아야 합니다.

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 장르 | 싱글 플레이 실시간 액션 RPG |
| 플랫폼 | 모바일 (Android / iOS), PC 테스트 지원 |
| 화면 방향 | 세로 모드 (Portrait) |
| 렌더 파이프라인 | URP 2D |
| 조작 | 화면 하단 40% 영역, 단일 손가락(엄지) 조작 / PC 마우스 지원 |
| 목표 | 프로토타입 → 안정적 서비스 → 수익화 |

### 핵심 조작 상태 (Input States)

| 입력 | 상태 | 설명 |
|------|------|------|
| Drag | Move | 순수 이동, 공격 판정 없음 |
| Tap | Attack | 제자리 정지 후 공격 |
| Swipe | Dodge/Dash | 방향 회피 |
| Hold → Release | Charge → Skill | 스킬 차지 및 발동 |

---

## 2. 기술 스택 및 환경

- Unity 버전: 프로젝트 생성 버전 고정 (변경 시 팀 전체 합의 필요)
- 렌더 파이프라인: Universal Render Pipeline (URP) 2D
- Input System: Unity New Input System (com.unity.inputsystem)
- 언어: C# (.NET Standard 2.1)
- 최소 타겟: Android API 28 / iOS 13
- 개발 테스트 환경: PC (마우스 입력으로 모바일 터치 에뮬레이션)

---

## 3. 아키텍처 설계 원칙

### 기본 패턴: MVP (Model - View - Presenter)

[Model] <--Interface--> [Presenter] <--직접참조--> [View]

- Model: 순수 데이터와 상태. MonoBehaviour 금지. SO 또는 순수 C# 클래스.
- View: 화면 표시와 애니메이션만. 로직 금지. MonoBehaviour.
- Presenter: Model과 View 중재. 게임 로직의 핵심. MonoBehaviour.

### DI (Dependency Injection) 전략

- 기본 원칙: 수동 DI 사용. Presenter 생성 시 Init() 메서드로 의존성을 명시적으로 주입.
- 전역 시스템(InputManager 등) 단일 접근점: ServiceLocator 패턴 허용 (남용 금지).
- DI 프레임워크(VContainer 등): 기획 안정화 및 씬 구조 확정 후 도입 검토.

### 테스트 전략

- 단위 테스트 대상: MonoBehaviour 의존성 없는 순수 로직.
  예) GestureRecognizer 판별 로직, 데미지 계산, SO 데이터 유효성 검사.
- 수동 테스트 대상: Presenter ↔ View 통합 동작, 플레이어 조작감.
- 테스트 ROI가 낮은 곳에 테스트 코드 강제 금지.

### 데이터 설계 원칙

- 캐릭터 스탯, 스킬 수치 등 순수 데이터는 ScriptableObject로 분리.
- 전투 상태(HP, 쿨타임 등) 런타임 데이터는 순수 C# 클래스 또는 구조체로 관리.
- Model은 MonoBehaviour를 상속하지 않으며 Unity 라이프사이클에 의존하지 않음.

### SOLID 원칙

| 원칙 | 적용 방법 |
|------|-----------|
| SRP | 클래스 하나는 하나의 책임만 |
| OCP | 캐릭터 추가 시 기존 코드 수정 없이 확장 |
| LSP | 자식 클래스는 부모를 완전히 대체 가능 |
| ISP | IAttackable, IDodgeable 등 작은 단위로 분리 |
| DIP | Presenter는 구체 View가 아닌 IView Interface에 의존 |

### 금지 패턴

- Singleton 남용 금지
- View에서 로직 처리 금지
- GameObject.Find(), SendMessage() 사용 금지

### 기본 패턴: MVP (Model - View - Presenter)

[Model] <--Interface--> [Presenter] <--직접참조--> [View]

- **Model**: 순수 데이터와 상태. MonoBehaviour 금지. SO 또는 순수 C# 클래스.
- **View**: 화면 표시와 애니메이션만. 로직 금지. MonoBehaviour.
- **Presenter**: Model과 View 중재. 게임 로직의 핵심. MonoBehaviour.

### SOLID 원칙

| 원칙 | 적용 방법 |
|------|-----------|
| SRP | 클래스 하나는 하나의 책임만 |
| OCP | 캐릭터 추가 시 기존 코드 수정 없이 확장 |
| LSP | 자식 클래스는 부모를 완전히 대체 가능 |
| ISP | IAttackable, IDodgeable 등 작은 단위로 분리 |
| DIP | Presenter는 구체 View가 아닌 IView Interface에 의존 |

### 금지 패턴

- Singleton 남용 금지
- View에서 로직 처리 금지
- GameObject.Find(), SendMessage() 사용 금지

---

## 4. 폴더 구조

Assets/
├── Rapier-Private/               # 비공개 (별도 Git repo, .gitignore 제외)
│   ├── Art/
│   ├── Audio/
│   └── ThirdParty/
│
└── _Project/                     # 공개 저장소 대상
    ├── 00_Docs/                  # 개발 문서, 지침서
    │   └── Editor/
    ├── 10_Scripts/
    │   ├── Core/                 # Interfaces, Base, Utils
    │   ├── Input/
    │   ├── Combat/               # Model, View, Presenter
    │   ├── Characters/           # Base, Warrior, Assassin, Rapier, Ranger
    │   ├── Enemies/
    │   ├── UI/                   # HUD, Common
    │   └── Data/                 # Characters, Skills
    ├── 20_Prefabs/               # Characters, Enemies, Skills, UI
    ├── 30_ScriptableObjects/     # Characters, Skills
    └── 40_Scenes/                # SampleScene, _Test/

규칙: 모든 프로젝트 에셋은 반드시 _Project/ 하위에 위치.
.gitignore 제외 대상: Assets/Rapier-Private/


## 5. 네임스페이스 규칙

기본 형식: `Game.[시스템명]` 또는 `Game.[시스템명].[서브시스템명]`

| 네임스페이스 | 폴더 |
|-------------|------|
| Game.Core | Scripts/Core/ |
| Game.Input | Scripts/Input/ |
| Game.Combat | Scripts/Combat/ |
| Game.Characters | Scripts/Characters/ |
| Game.Characters.Warrior | Scripts/Characters/Warrior/ |
| Game.Characters.Assassin | Scripts/Characters/Assassin/ |
| Game.Characters.Rapier | Scripts/Characters/Rapier/ |
| Game.Characters.Ranger | Scripts/Characters/Ranger/ |
| Game.UI | Scripts/UI/ |
| Game.Data | Scripts/Data/ |

---

## 6. 코딩 컨벤션

### 명명 규칙

| 대상 | 규칙 | 예시 |
|------|------|------|
| 클래스 | PascalCase | PlayerPresenter |
| 인터페이스 | I + PascalCase | ICharacterView |
| Public 메서드 | PascalCase | TakeDamage() |
| Private 필드 | _ + camelCase | _currentHP |
| SerializeField | _ + camelCase | _moveSpeed |
| 상수 | UPPER_SNAKE_CASE | MAX_CHARGE_TIME |
| 이벤트 | On + PascalCase | OnTapPerformed |
| SO 클래스 | PascalCase + Data/Config | WarriorData |
| Enum 값 | PascalCase | InputState.Move |

### 파일 내부 순서

1. Serialized Fields
2. Private Fields
3. Properties
4. Unity Lifecycle (Awake → OnEnable → Start → Update → OnDisable → OnDestroy)
5. Public Methods
6. Private Methods
7. Event Handlers (On~ 접두사)
8. #if UNITY_EDITOR 블록

### 주석 규칙

- 공개 API: XML 문서 주석 (///) 필수
- 복잡한 로직: 인라인 주석으로 의도 설명
- 금지: 코드를 그대로 설명하는 주석

---

## 7. 이벤트 통신 규칙

| 상황 | 방식 |
|------|------|
| Presenter ↔ View 계약 | C# Interface |
| 동일 씬 내 시스템 간 통신 | C# event |
| 전역 시스템 단일 접근점 | ServiceLocator (남용 금지, 등록 목록 관리 필요) |
| 씬 경계를 넘는 글로벌 이벤트 | SO 이벤트 채널 (추후 도입) |

- 이벤트 구독은 OnEnable, 해제는 OnDisable에서 반드시 쌍으로 처리
- 핸들러 이름: Handle + 동사 (HandleTapPerformed)

## 8. ScriptableObject 활용 규칙

- 캐릭터 스탯, 스킬 설정값은 SO로 분리
- menuName 형식: 'Game/Data/[카테고리]/[이름]'
- 외부에는 읽기 전용 프로퍼티(=>)만 노출. setter 금지.

---

## 9. 씬 구성 전략

- 현재: 단일 씬 (SampleScene) - 프로토타입 단계
- 추후: Bootstrap(영속) + Gameplay(Additive) + UI(Additive) 분리
- 씬 분리는 기획 안정화 후 진행

---

## 10. Input System 규칙

### 입력 아키텍처

New Input System → GestureRecognizer → InputState Enum → C# event → CharacterPresenter

### 플랫폼 처리

- InputActions 에셋에서 Mobile(Touch)과 PC(Mouse) 바인딩을 모두 등록
- 로직 코드에서 플랫폼 분기(#if MOBILE 등) 금지

### 입력 유효 영역

- 화면 하단 40% (정규화 Y좌표 0.0 ~ 0.4)

### 제스처 구분 기준 (초기값, 플레이테스트 후 조정)

| 제스처 | 판별 조건 |
|--------|-----------|
| Tap | 이동 거리 < 20px, 지속 시간 < 0.2초 |
| Swipe | 이동 거리 >= 20px, 지속 시간 < 0.3초 |
| Hold | 이동 없음, 지속 시간 >= 0.3초 |
| Drag | 이동 거리 >= 20px, 지속 시간 >= 0.3초 |

---

## 11. 작업 프로세스

### 기능 개발 사이클

1. 기획 확인 및 기술 설계 대화
2. Interface / Base 클래스 정의
3. Model → Presenter → View 순서로 구현
4. 에디터 테스트 씬에서 단독 검증
5. 코드 리뷰 (SOLID, 컨벤션, 확장성 체크)
6. 기획자용 Inspector 세팅 (SO, SerializeField)
7. 메인 씬 편입

### 코드 리뷰 체크리스트

- [ ] 네임스페이스가 올바르게 지정되었는가?
- [ ] View에 로직이 없는가?
- [ ] Presenter가 IView Interface를 통해 View와 통신하는가?
- [ ] 이벤트 구독/해제가 OnEnable/OnDisable에 쌍으로 있는가?
- [ ] SerializeField에 [Header]로 Inspector 그룹이 지정되었는가?
- [ ] 공개 API에 XML 문서 주석이 있는가?
- [ ] Find(), SendMessage()를 사용하지 않았는가?
- [ ] SO 데이터는 읽기 전용 프로퍼티로만 외부에 노출하는가?
- [ ] 새 캐릭터 추가 시 기존 코드를 수정하지 않아도 되는가? (OCP)

---

## 12. 변경 이력

| 버전 | 날짜 | 내용 |
|------|------|------|
| v0.1.0 | 2026-03-05 | 초안 작성. 기술 스택, 아키텍처, 컨벤션, 폴더 구조 확립 |
| v0.2.1 | 2026-03-05 | GuidelinesEditor.cs 추가. md 직접 수정 유틸 도입으로 cs 재생성 방식 제거 |
| v0.3.0 | 2026-03-05 | DI 전략, 테스트 전략, 데이터 설계 원칙 추가. 이벤트 통신 표에 ServiceLocator 항목 추가 |

---

> 이 문서는 살아있는 문서입니다.
> 기획 변경, 기술 부채, 팀 합의에 따라 지속적으로 업데이트됩니다.
> 변경 시 반드시 변경 이력 섹션을 갱신하세요.
