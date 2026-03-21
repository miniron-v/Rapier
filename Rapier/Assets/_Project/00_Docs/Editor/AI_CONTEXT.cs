// =============================================================
// AI_CONTEXT.cs  —  AI (Claude) 전용 컨텍스트 파일
// =============================================================
// 목적:
//   새 대화 세션 시작 시 이 파일 하나만 읽으면
//   프로젝트의 모든 컨텍스트를 복구할 수 있다.
//
// ── 갱신 규칙 ─────────────────────────────────────────────
//   - 매 작업 세션 종료 시 AI가 직접 갱신한다.
//   - 섹션 구조를 유지하면서 내용만 교체한다.
//   - 사람이 이 파일을 직접 편집하지 않는다.
//
// ── 운영 규칙 ─────────────────────────────────────────────
//   [문서 갱신]
//     AI_CONTEXT, 가이드라인 등 문서를 수정할 때는
//     수정할 내용을 채팅으로 먼저 보고하고 사용자 승인 후 작업한다.
//
//   [실수 기록]
//     작업 중 재작업이 발생한 경우, 어떤 내용을 [10]에 기록할지
//     채팅으로 먼저 보고하고 사용자 승인 후 추가한다.
//
//   [기능 구현 워크플로우]
//     1. 목표 확인
//     2. 설계 제작 — 채팅으로 작업 내용 보고 후 사용자와 합의.
//                    모호한 기획 요소가 있다면 작업 전 질문.
//     3. 설계도대로 제작
//     4. C# 클래스 직접 검증(validate_script) 및 오류 스스로 수정
//     5. 사용자 Play 모드 테스트
//     6. 테스트 결과 반영 수정
//     7. 기능 완성 후 발생한 실수/팁을 [10]에 기록 (사전 승인 후)
// =============================================================

// -------------------------------------------------------
// [1] 프로젝트 기본 정보
// -------------------------------------------------------
// 프로젝트명  : 미정 (가칭 Rapier)
// 장르        : 싱글 플레이 실시간 액션 RPG (모바일, 세로 모드)
// 플랫폼      : Android / iOS — PC 마우스로 병행 테스트
// 렌더        : URP 2D
// 입력        : New Input System (Touch + Mouse 듀얼 바인딩)
// 아키텍처    : MVP + 이벤트 기반 (C# Interface / C# event / SO 채널)
// 저장소      : 공개(GitHub) + 비공개(Rapier-Private) 이중 구조
//               공개  — 스크립트 공개, 피드백 수용
//               비공개 — Art, Audio, ThirdParty (Assets/Rapier-Private/, 별도 .git)
// 유니티 경로 : Rapier/ 가 공개 저장소 루트 (Assets/ 상위)

// -------------------------------------------------------
// [2] 캐릭터 목록 및 메커니즘
// -------------------------------------------------------
// 공통 입력 4가지: Drag(Move) / Tap(Attack) / Swipe(Dodge) / Hold→Release(Charge→Skill)
// 저스트 회피: 적 공격 타이밍에 회피 중 피격 시 발동 → 게임 슬로우 + 캐릭터 고유 스킬 즉시 발동
//
// Warrior  : Hold 중 방패 방어 (피해 감소/무효화) 추가. Hold+Swipe = 방패 밀쳐내기. 패링 성공 시 즉시 반격.
// Assassin : 저스트 회피 시 회피 전 위치에 잔상 생성 (피해/어그로 없음).
//            잔상 활성 중 본체의 모든 공격(Tap, 차지 스킬)에 잔상이 동시에 동참.
//            고유 스킬 / 차지 스킬 = 360도 광역 공격.
// Rapier   : 저스트 회피 후 고유 스킬 = 가장 가까운 적에게 대시 → 사각형 범위 내 전체 적에게
//            표식 부여 + 데미지 → 회피 목적지(DodgeDest)로 복귀. 표식 최대 5중첩.
//            차지 스킬 = 표식 있는 모든 적을 중첩 수 × (attackPower × chargeMarkMultiplier) 데미지.
// Ranger   : 원거리 사격(Tap). 일반 회피 시 지뢰 설치. 고유 스킬 = 강화 화살 발사.

// -------------------------------------------------------
// [3] 기술 결정 사항 및 배경
// -------------------------------------------------------
// [MVP]
//   캐릭터 4종의 고유 메커니즘으로 View/Logic 분리가 필수.
//   MVC는 Controller 비대화 위험. MVP는 Presenter 단위로 캐릭터별 분리 가능.
//   장기적으로 View 없이 Presenter 단독 테스트 가능.
//
// [통신 방식]
//   Presenter ↔ View     : C# Interface (명시적 계약)
//   시스템 간 통신        : C# event
//   씬 경계 글로벌 이벤트 : SO 이벤트 채널 (추후 씬 분리 시 도입)
//
// [씬 구조]
//   Lobby 씬  → BossRushDemo 씬 (게임) → 결과 후 Lobby로 복귀
//   SceneController.LoadLobby() / LoadGame() 으로 전환.
//   Time.timeScale은 씬 전환 전 항상 1f로 복구.
//
// [적 계층 구조]
//   EnemyPresenterBase (abstract) ← 모든 적의 공통 베이스 (Chase/Windup/Hit/PostAttack + 시퀀서)
//   ├── NormalEnemyPresenter       ← 일반 적 (WaveManager 오브젝트 풀)
//   └── BossPresenterBase (abstract) ← 2페이즈 시스템, 페이즈별 시퀀스 교체
//       ├── TitanBossPresenter     ← 공격 로직 없음. ChargeAttackAction SO에 위임.
//       └── SpecterBossPresenter   ← 공격 로직 없음. TeleportAttackAction SO에 위임.
//
// [적 공격 시스템 — AttackAction 구조]
//   모든 적의 공격은 EnemyAttackAction 파생 클래스로 정의된다.
//   EnemyStatData.attackSequence (List<EnemyAttackAction>, [SerializeReference]) 에 직렬화.
//   BossStatData.phase2Sequence 로 페이즈 전환 시 시퀀서가 자동 교체됨.
//
//   [흐름]
//     EnterWindupPhase() → PrepareWindup(ctx) 호출 (가변 범위 확정 + 인디케이터 목록 반환)
//                        → AttackIndicator.Play() (인디케이터 표시)
//     EnterHitPhase()   → Execute(ctx, onComplete) 코루틴 호출 (실제 공격 판정)
//                        → onComplete() → EnterPostAttackPhase()
//
//   [파생 클래스]
//     MeleeAttackAction    : 인디케이터 모양(Sector/Rectangle)에 맞는 히트 판정.
//                            Execute() 시점 실시간 위치로 판정 (Windup 중 회피 가능).
//     AoeAttackAction      : 범위 내 전체 히트 판정.
//     ChargeAttackAction   : PrepareWindup에서 RaycastToWall로 실제 wallDist 확정,
//                            인디케이터 range를 런타임 캐시(_resolvedWallDist)로 덮어씀.
//                            SO의 chargeMaxDistance는 기획 상한값으로만 사용.
//     TeleportAttackAction : 히트 판정 없음. 페이드아웃 → 순간이동 → 페이드인.
//
//   [인디케이터]
//     AttackIndicatorEntry: shape(Sector/Rectangle) + angleOffset + sectorData/rectData
//     angleOffset: 플레이어 방향 기준 회전 오프셋 (도). 여러 방향 동시 표시 가능.
//     방향 계산 기준: x축 기준 (Atan2 / Cos·Sin 순서). AttackIndicator와 MeleeAttackAction 통일.
//     인디케이터 루트 localScale: lossyScale 역수 적용 → 보스 bossScale 상속 취소.
//                                 메시 좌표 = 월드 단위와 항상 일치.
//     lockIndicatorDirection: true 시 Windup 시작 방향 고정.
//
//   [SO 데이터 원칙]
//     SO 값은 기획자가 런타임 전 설정하는 고정값.
//     런타임 가변값(wallDist 등)은 [NonSerialized] 필드에 캐싱. SO 원본 불변.
//
// [폴더 인덱싱]
//   10 단위 숫자 접두사로 ABC 정렬 혼용 방지. Scripts 내부는 기능명만 사용.
//
// [단일 씬 전략]
//   프로토타입 단계. 기획 안정화 후 Bootstrap + Additive 분리 예정.
//
// [네임스페이스]
//   형식: Game.[시스템명]   예: Game.Core / Game.Input / Game.Combat / Game.Characters / Game.UI

// -------------------------------------------------------
// [4] 폴더 구조 스냅샷
// -------------------------------------------------------
// Assets/
// ├── Rapier-Private/
// └── _Project/
//     ├── 00_Docs/
//     │   ├── PROJECT_GUIDELINES.md
//     │   ├── Rapier_Prototype_DesignDoc.md
//     │   ├── Rapier_Prototype_DesignDoc.docx
//     │   └── Editor/
//     │       ├── DocSyncTool.cs
//     │       ├── ProjectFolderSetup.cs
//     │       ├── HudSetup.cs
//     │       ├── BossRushHudSetup.cs        ← BossRushManager._hudView 자동 연결 + EventSystem 생성
//     │       ├── LobbyHudSetup.cs           ← 로비 Canvas 자동 생성 + EventSystem 생성
//     │       ├── BossStatDataCreator.cs
//     │       ├── TitanDataSetup.cs          ← 타이탄 공격 시퀀스 초기값 설정
//     │       ├── EnemyDataSetup.cs          ← 일반 적 / 스펙터 공격 시퀀스 초기값 설정
//     │       ├── PrefabMissingScriptCleaner.cs
//     │       └── AI_CONTEXT.cs
//     ├── 10_Scripts/
//     │   ├── Core/
//     │   │   └── SceneController.cs         ← LoadLobby() / LoadGame() 정적 유틸
//     │   ├── Input/       (GestureRecognizer)
//     │   ├── Combat/      (IDamageable)
//     │   ├── Characters/  (CharacterPresenterBase, CharacterView, CharacterModel,
//     │   │                  CharacterStatData, Rapier/RapierPresenter, Rapier/RapierStatData)
//     │   ├── Enemies/
//     │   │   ├── EnemyPresenterBase.cs
//     │   │   ├── NormalEnemyPresenter.cs
//     │   │   ├── EnemyModel.cs
//     │   │   ├── EnemyView.cs
//     │   │   ├── EnemyHpBar.cs
//     │   │   ├── AttackIndicatorData.cs
//     │   │   ├── AttackIndicator.cs
//     │   │   ├── EnemyAttackAction.cs
//     │   │   ├── EnemyAttackContext.cs
//     │   │   ├── EnemyAttackSequencer.cs
//     │   │   ├── MeleeAttackAction.cs
//     │   │   ├── AoeAttackAction.cs
//     │   │   ├── ChargeAttackAction.cs
//     │   │   ├── TeleportAttackAction.cs
//     │   │   ├── WaveManager.cs
//     │   │   ├── BossRushManager.cs         ← InitHudView() 추가, 플레이어 사망 구독
//     │   │   └── Boss/
//     │   │       ├── BossPresenterBase.cs
//     │   │       ├── TitanBossPresenter.cs
//     │   │       └── SpecterBossPresenter.cs
//     │   ├── UI/
//     │   │   ├── HUD/     (HudView, BossRushHudView)
//     │   │   ├── LobbyManager.cs            ← 시작 버튼 → SceneController.LoadGame()
//     │   │   └── VirtualJoystick.cs
//     │   └── Data/        (EnemyStatData, BossStatData)
//     ├── 20_Prefabs/
//     │   ├── Enemy_Template.prefab
//     │   ├── Rapier_Player.prefab
//     │   ├── Titan_Boss.prefab
//     │   └── Specter_Boss.prefab
//     ├── 30_ScriptableObjects/
//     │   ├── Characters/  (RapierStatData.asset)
//     │   └── Enemies/
//     │       ├── NormalEnemyStatData.asset
//     │       └── Boss/
//     │           ├── TitanStatData.asset
//     │           └── SpecterStatData.asset
//     └── 40_Scenes/
//         ├── SampleScene.unity
//         ├── BossRushDemo.unity
//         └── Lobby.unity

// -------------------------------------------------------
// [5] 완료된 작업
// -------------------------------------------------------
// [Phase 1~8] 이전 세션 참고 (생략)
//
// [Phase 9] 적 공격 인디케이터 + 공격 시퀀서 시스템
//
//   [9-1] 공격 인디케이터 시스템
//     기존 원형 알파 보간 인디케이터 → 아웃라인(범위 고정) + 스캔라인(중심→경계 확장) 방식으로 교체.
//     스캔라인이 경계에 닿는 순간 = 공격 발동.
//     모양: Sector(부채꼴) / Rectangle(사각형).
//     angleOffset으로 여러 방향 인디케이터 동시 표시 가능 (예: 120도 간격 3개).
//     방향 계산 기준 통일: x축 기준 Atan2 / Cos·Sin 순서. AttackIndicator ↔ MeleeAttackAction 동일.
//     인디케이터 루트에 lossyScale 역수 적용 → 보스 bossScale 스케일 상속 취소.
//
//   [9-2] 공격 시퀀서 시스템
//     EnemyStatData에 [SerializeReference] List<EnemyAttackAction> attackSequence 추가.
//     BossStatData에 phase2Sequence 추가. HP 50% 이하 시 시퀀서 자동 교체.
//     상태머신(Chase→Windup→Hit→PostAttack) 유지.
//     EnterWindupPhase()에서 PrepareWindup() 호출 후 인디케이터 표시.
//     EnterHitPhase()에서 Execute() 코루틴 호출.
//     ChargeAttackAction: PrepareWindup에서 wallDist 계산, 인디케이터 range 확정.
//     TeleportAttackAction: 인디케이터 없음. 순간이동 자체가 연출.
//     TitanBossPresenter / SpecterBossPresenter: 공격 로직 전부 AttackAction으로 이전.
//
//   [9-3] 에디터 유틸
//     TitanDataSetup.cs: 메뉴 Rapier/Dev/Setup Titan Attack Sequence
//     EnemyDataSetup.cs: 메뉴 Rapier/Dev/Setup Normal Enemy Sequence
//                              Rapier/Dev/Setup Specter Sequence
//     [SerializeReference] 리스트 갱신 시:
//       null 초기화 → SetDirty → SaveAssets → ImportAsset → 재할당 순서 필수.
//
// [Phase 10] Android 빌드 버그 수정
//   StageBuilder.cs / VirtualJoystick.cs 의 #if UNITY_EDITOR 분기 제거.
//   런타임 Sprite를 Texture2D 직접 생성 방식으로 교체. (TIP-06 참고)
//
// [Phase 11] 게임 루프 구현 (결과 UI → 로비 씬 → 게임 씬)
//
//   [11-1] SceneController.cs 신규 생성 (Game.Core)
//     LoadLobby() / LoadGame() 정적 유틸.
//     씬 전환 전 Time.timeScale = 1f 복구 보장.
//     씬 이름 상수: LOBBY = "Lobby", BOSS_RUSH = "BossRushDemo"
//
//   [11-2] LobbyManager.cs 신규 생성 (Game.UI)
//     시작 버튼 → SceneController.LoadGame().
//     Init(Button startButton) 공개 메서드로 LobbyHudSetup이 주입.
//
//   [11-3] CharacterPresenterBase 수정
//     public event Action OnPlayerDeath 추가.
//     HandleDeath()에서 OnPlayerDeath 발행.
//
//   [11-4] BossRushManager 수정
//     SubscribePlayerDeath() — FindObjectOfType로 플레이어 사망 이벤트 구독.
//     HandlePlayerDeath() — _isGameOver 플래그 + ShowResult(false).
//     ShowAllClear() → ShowResult(true) 교체.
//     InitHudView(BossRushHudView) 공개 메서드 추가 — BossRushHudSetup이 주입.
//
//   [11-5] BossRushHudView 수정
//     _allClearPanel 제거 → _resultPanel + _resultText + _toLobbyButton 로 교체.
//     ShowResult(bool isCleared): true=ALL CLEAR(노랑), false=GAME OVER(빨강).
//     Init(...) 공개 메서드로 BossRushHudSetup이 주입.
//
//   [11-6] Setup 툴 직렬화 방식 개선
//     Reflection → Init() 직접 호출 방식으로 전환.
//     SetDirty(컴포넌트) + MarkSceneDirty + SaveScene 으로 씬 저장 자동화.
//     BossRushHudSetup: BossRushManager 탐색 → InitHudView() 주입.
//     BossRushHudSetup / LobbyHudSetup: EnsureEventSystem() 추가
//       — EventSystem 없을 때만 생성, InputSystemUIInputModule 사용.
//
//   [11-7] LobbyHudSetup.cs 신규 생성
//     메뉴: Rapier/Lobby/Create Lobby HUD / Rapier/Lobby/Rebuild Lobby HUD
//     배경 + 타이틀("RAPIER") + 시작 버튼 생성.
//     LobbyManager.Init(btn) 주입 + SetDirty + SaveScene.

// -------------------------------------------------------
// [6] 미해결 이슈
// -------------------------------------------------------
// [ISSUE-01] [SerializeReference] 리스트 인스펙터 NullReferenceException
//   null 초기화 → 재할당 과정에서 인스펙터 UI가 SerializedObjectList 참조를 잃어버림.
//   런타임 동작에는 영향 없음. 인스펙터를 닫았다 열면 사라짐.
//   CustomEditor 작업 시 함께 해결 예정.
//
// [ISSUE-02] EnemyStatData CustomEditor 미구현
//   [SerializeReference] 리스트에 + 버튼을 누를 때 타입 선택 드롭다운이 나오지 않음.
//   현재는 에디터 스크립트(TitanDataSetup 등)로 초기값 주입.
//   CustomEditor + 인디케이터 미리보기 패널 작업 시 함께 해결 예정.

// -------------------------------------------------------
// [7] 다음 작업
// -------------------------------------------------------
// Phase 11 완료 (게임 루프).
// Phase 12 후보:
//   - EnemyStatData CustomEditor
//       [SerializeReference] 타입 선택 드롭다운
//       공격 패턴별 인디케이터 미리보기 패널 (SO 하단 탭 형식)
//   - Warrior / Assassin / Ranger 캐릭터 구현
//   - 씬 전환 / Bootstrap 구조
//   - 스테이지 시스템 (웨이브 or 보스 러시 선택)
//   - 아트 교체

// -------------------------------------------------------
// [8] MCP 운영 제약 및 팁
// -------------------------------------------------------
// [MCP-01] Assets -> Reimport All 절대 실행 금지
//   Unity 재시작 -> MCP 연결 끊김. 재컴파일은 파일 저장으로 충분.
//
// [MCP-02] .md 파일 직접 편집 불가 — 템플릿 교체 방식으로 운영
//   mcpforunity는 확장자를 무조건 .cs로 치환한다.
//   두 문서의 실질적 원본은 DocSyncTool.cs 안의 템플릿 함수.
//   .md 파일은 해당 템플릿으로부터 덮어쓰기 생성되는 파생 파일이다.
//
//   기획서 수정 워크플로우:
//     1. DocSyncTool.cs 의 GetDesignDocTemplate() 내용을 수정
//     2. execute_menu_item('Rapier/Docs/Create DesignDoc MD') -> .md 덮어쓰기
//     3. execute_menu_item('Rapier/Docs/Sync to DOCX') -> .docx 자동 동기화
//
//   가이드라인 수정 워크플로우:
//     1. DocSyncTool.cs 의 GetGuidelinesTemplate() 내용을 수정
//     2. execute_menu_item('Rapier/Docs/Create Guidelines MD') -> .md 덮어쓰기
//     3. execute_menu_item('Rapier/Docs/Sync to DOCX') -> .docx 자동 동기화
//
// [MCP-03] apply_text_edits 한글 endCol 문제
//   한글 멀티바이트로 endCol 오류 발생 가능.
//   대안: endLine+1, endCol=1 로 다음 줄 첫 칸까지 포함.
//
// [MCP-04] batch_execute 는 manage_asset, execute_menu_item, validate_script,
//           manage_script, delete_script 미지원. 순차 실행으로 대체.
//
// [MCP-05] 원인 미확정 상태에서 MCP 코드 수정 금지
//   에디터에서 직접 확인 가능한 사항(레이어, 콜라이더, 스프라이트 할당 등)은
//   사용자에게 먼저 질문하고, MCP 작업은 원인이 확정된 후에만 진행.
//
// [MCP-06] 구조적 파일은 delete -> create 전체 재생성
//   AI_CONTEXT, HudSetup 등 전체 구조가 중요한 파일은
//   apply_text_edits 부분 수정 대신 delete_script -> create_script 로 재생성.
//
// [MCP-07] 코드로 생성한 Canvas — CanvasScaler 기본값 주의
//   기본값 ConstantPixelSize -> Device Simulator에서 UI 작게 보임.
//   필요 시: uiScaleMode = ScaleWithScreenSize, referenceResolution = (1080, 1920).
//
// [MCP-08] 코드로 생성한 RectTransform — Pivot 기본값(0.5, 0.5) 주의
//   anchoredPosition은 pivot 기준 계산. Anchor와 Pivot을 반드시 일치시킬 것.
//
// [MCP-09] EventSystem 생성 시 InputSystemUIInputModule 사용
//   New Input System 환경에서 EventSystem 생성 시
//   StandaloneInputModule 대신 InputSystemUIInputModule을 사용할 것.
//   StandaloneInputModule은 구 Input System 전용 → 런타임 에러 발생.
//
// [TIP-01] 2D Sprite 패키지 내장 스프라이트
//   경로: Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/
//   로드: AssetDatabase.LoadAssetAtPath<Sprite>(경로 + 파일명)
//   종류: Square / Circle / Capsule / Triangle /
//         9Sliced / HexagonFlatTop / HexagonPointTop / IsometricDiamond
//   용도 권장:
//     플레이어 -> Circle  /  일반 적 -> Square 또는 Capsule  /  보스 -> HexagonFlatTop
//   ※ 에디터 전용 API. 런타임 MonoBehaviour에서는 사용 금지. (TIP-06 참고)
//
// [TIP-02] SpriteRenderer / UI Image 생성 시 Sprite None 여부 반드시 확인
//   SpriteRenderer: sprite=None 이면 오브젝트가 보이지 않음.
//   UI Image: sprite=None 이면 Image.Type이 Simple로 강제 -> fillAmount 완전 무시됨.
//             Radial360은 Circle 없으면 사각형으로 렌더링됨.
//   코드로 생성 시 TIP-01 스프라이트를 항상 함께 로드하여 할당.
//     Horizontal fill -> Square.png  /  Radial360 -> Circle.png  /  Vertical fill -> Square.png
//
// [TIP-03] GestureRecognizer Hold 판정 타이밍 주의
//   Hold 이벤트는 판정 직후부터 발생. ChargeRequiredTime이 짧으면
//   최초 이벤트 수신 시점에 이미 duration 초과 -> 차지가 즉시 1로 보임.
//   프로토타입 단계에서는 ChargeRequiredTime >= 1.0f 권장.
//
// [TIP-04] BossRushManager는 WaveManager 없이 단독 운영
//   BossRushDemo 씬에는 WaveManager가 없다.
//   RapierPresenter/CharacterPresenterBase의 적 탐색 로직은
//   WaveManager 우선 → null이면 BossRushManager 폴백 구조로 작성되어 있음.
//
// [TIP-05] [SerializeReference] 리스트 에디터 스크립트 갱신 순서
//   기존 직렬화 데이터가 남아있으면 단순 재할당으로는 반영 안 됨.
//   null 초기화 → EditorUtility.SetDirty → AssetDatabase.SaveAssets
//   → AssetDatabase.ImportAsset → 재할당 → SetDirty → SaveAssets 순서 필수.
//
// [TIP-06] 런타임 Sprite 조달 원칙
//   AssetDatabase는 에디터 전용 API. 런타임 MonoBehaviour에서 사용 금지.
//   런타임 파일에 #if UNITY_EDITOR 분기가 보이면 즉시 의심할 것.
//   에디터에서는 정상 동작하므로 빌드 전까지 버그가 드러나지 않는다.
//     단순 사각형 → Texture2D(64×64) 직접 생성 후 Sprite.Create
//     단순 원형   → Texture2D(128×128) 픽셀 채우기 후 Sprite.Create
//     게임 아트   → Resources.Load<Sprite>() 또는 SO 레퍼런스
//
// [TIP-07] Setup 툴 작성 시 [SerializeField] 필드 전체 연결 체크리스트 준수
//   Setup 툴 완성 전, 해당 씬에 존재하는 모든 컴포넌트의 [SerializeField] 필드 목록과
//   Setup 툴의 주입 코드를 1:1로 대조할 것.
//   확인 항목:
//     1. 생성한 컴포넌트(HudView 등)의 모든 [SerializeField] → Init()으로 주입됐는가?
//     2. 씬의 다른 컴포넌트(Manager 등)가 참조해야 할 필드 → 탐색 후 주입됐는가?
//     3. EventSystem이 필요한 씬인가? → EnsureEventSystem() 호출됐는가?
//     4. SetDirty(컴포넌트) + MarkSceneDirty + SaveScene 순서가 지켜졌는가?

// -------------------------------------------------------
// [10] AI 실수 기록
// -------------------------------------------------------
// [MISTAKE-01] execute_menu_item 미사용으로 사용자에게 수동 실행 요청
//   상황: HUD 셋업 메뉴 실행 시
//   실수: execute_menu_item으로 직접 실행 가능한 메뉴를 사용자에게 넘김
//   교훈: Unity 에디터 메뉴는 항상 execute_menu_item으로 AI가 직접 실행할 것
//
// [MISTAKE-02] UI Image에 Sprite 미할당으로 Filled 모드 무시됨
//   상황: HudSetup.cs에서 ChargeGaugeFill, HpFill Image 코드 생성 시
//   실수: Sprite=None 상태로 Image.Type.Filled 설정 -> fillAmount 완전 무시됨
//   교훈: Filled 모드 Image 생성 시 TIP-01 스프라이트를 반드시 동시에 할당할 것
//
// [MISTAKE-03] apply_text_edits로 긴 메서드 범위를 잘못 산정해 중괄호 불균형 발생
//   상황: BuildHud 메서드를 apply_text_edits로 부분 교체 시도
//   실수: 기존 메서드의 끝 줄을 잘못 지정해 닫는 중괄호 누락
//   교훈: 긴 메서드 교체 시 delete_script 후 create_script로 재생성하는 방식이 안전
//
// [MISTAKE-04] ChargeRequiredTime을 짧게 설정해 차지가 즉시 1로 표시됨
//   상황: CharacterStatData.chargeRequiredTime = 0.3f 설정 시
//   실수: Hold 판정 직후 이미 duration이 초과 -> 최초 이벤트부터 ratio = 1
//   교훈: TIP-03 참고. 프로토타입 단계에서는 chargeRequiredTime >= 1.0f 권장
//
// [MISTAKE-05] 원인 미확정 상태에서 MCP로 코드 수정하여 토큰 낭비
//   상황: OverlapBoxAll hits.Length=0 디버깅 중 레이어/콜라이더 정상 여부 확인 시
//   실수: 에디터에서 바로 확인 가능한 사항을 사용자에게 먼저 질문하지 않고 MCP로 코드를 수정함
//   교훈: 에디터에서 직접 확인 가능한 사항은 사용자에게 먼저 질문하고,
//         MCP 작업은 원인이 확정된 후에만 진행할 것
//
// [MISTAKE-06] apply_text_edits 반복 사용으로 AI_CONTEXT 내용 오염
//   상황: AI_CONTEXT.cs를 여러 세션에 걸쳐 apply_text_edits로 부분 수정
//   실수: 라인 번호 오산으로 내용 중복, 누락, 엉뚱한 위치 삽입이 누적됨
//   교훈: AI_CONTEXT처럼 전체 구조가 중요한 파일은 부분 수정보다
//         delete_script 후 create_script로 전체 재생성하는 방식이 안전
//
// [MISTAKE-07] SpriteAtlasManager.LoadAtlas() 잘못된 코드 삽입
//   상황: CreateAttackRangeIndicator()에서 내장 스프라이트 로드 시도
//   실수: SpriteAtlasManager.LoadAtlas()는 해당 시그니처가 존재하지 않음 -> CS0117 컴파일 오류
//   교훈: 런타임에서 내장 스프라이트가 필요할 경우 Resources.Load 또는 SO 레퍼런스 사용.
//         에디터 스크립트가 아닌 런타임 스크립트에서는 AssetDatabase 사용 불가.
//
// [MISTAKE-08] Editor 폴더에 런타임 컴포넌트 배치
//   상황: JustDodgeDebugger.cs를 00_Docs/Editor/ 에 생성
//   실수: Editor 폴더 내 스크립트는 씬에 부착 불가 -> MonoBehaviour 컴포넌트로 쓸 수 없음
//   교훈: 씬에 부착하는 컴포넌트는 반드시 Editor 폴더 바깥(DevTools 등)에 배치할 것
//
// [MISTAKE-09] Game.Debug 네임스페이스 사용으로 UnityEngine.Debug 충돌
//   상황: JustDodgeDebugger.cs 최초 작성 시
//   실수: namespace Game.Debug 선언 -> UnityEngine.Debug와 이름 충돌로 컴파일 오류
//   교훈: 네임스페이스 이름은 UnityEngine 내장 클래스명과 겹치지 않도록 주의.
//         DevTools 계열은 Game.DevTools 네임스페이스 사용
//
// [MISTAKE-10] 승인 없이 작업 착수 시도
//   상황: Phase 7 작업 중 분석 완료 후
//   실수: 사용자 승인을 받지 않고 MCP 코드 수정을 시작하려 함
//   교훈: 설계/분석이 완료되더라도 반드시 채팅으로 보고 후 승인을 받은 뒤 착수할 것.
//         가이드라인 "설계 → 보고 → 합의 → 착수" 순서를 항상 준수
//
// [MISTAKE-11] SOLID 원칙 위배 코드 작성
//   상황: 스킬 발동 중 일반 공격 차단 로직 설계 시
//   실수: 자식(RapierPresenter)의 내부 상태(_isDashSkillActive)를
//         Base의 OnHitDamageable에서 간접 참조하는 구조 제안 → OCP/DIP 위반
//   교훈: 자식 고유 상태에 의존하는 로직은 반드시 자식 안에서만 처리.
//         Base와의 결합은 virtual/override 계약(CanAttack 등)으로만 수행할 것
//
// [MISTAKE-12] 컴파일 에러 잔존 상태에서 작업 완료 알림
//   상황: validate_script 에러 0건이나 콘솔에 이전 캐시 에러가 남아있던 상황
//   실수: read_console로 실제 에러 잔존 여부를 재확인하지 않고 완료 보고
//   교훈: 작업 완료 보고 전 반드시 read_console clear 후 재확인까지 완료할 것
//
// [MISTAKE-13] _isSkillSequenceActive 하나로 OnDodgeDashComplete 억제
//   상황: Phase 8 보스 러시 데모에서 저스트 회피 후 이동/공격 불가 버그 수정 시
//   실수: 스킬 대기 상태(_isSkillSequenceActive)와 스킬 대시 진행 상태를 구분하지 않아
//         보스가 있는 상황에서 일반 회피만 해도 OnDodgeDashComplete가 억제되어 영구 잠금 발생
//   교훈: 억제 조건은 실제 대시가 StartCoroutine된 _dashSkillStarted로만 판단할 것.
//         OnSlowMotionEnd에서도 _isSkillSequenceActive를 함께 초기화해야
//         Hold 없이 슬로우가 끝난 경우 잠금이 풀린다.
//
// [MISTAKE-14] DodgeDashCurve EaseOut 끝값 0으로 인한 영구 잠금
//   상황: 일반 회피 후 이동/공격 불가 버그 원인 분석 시
//   실수: AnimationCurve 끝값이 0.00f → MoveTowards 이동량이 0에 수렴하여
//         ARRIVE_THRESHOLD(0.05f)에 도달하지 못하고 while 루프 무한 반복.
//         결과적으로 OnDodgeDashComplete()가 호출되지 않아 _isDodging/MoveState 영구 잠금.
//   교훈: Ease 커브 끝값은 0보다 큰 값(0.50f 이상)으로 유지할 것.
//         DodgeDashRoutine에 MinSpeed 보증(dashSpeed*0.05f)과 타임아웃을 항상 포함할 것.
//
// [MISTAKE-15] StandaloneInputModule 사용으로 Input System 에러
//   상황: BossRushDemo 씬에 EventSystem 오브젝트 생성 시
//   실수: StandaloneInputModule 추가 → New Input System 환경에서
//         UnityEngine.Input.get_mousePosition() 런타임 에러 연속 발생
//   교훈: MCP-09 참고. New Input System 프로젝트에서 EventSystem 생성 시
//         반드시 InputSystemUIInputModule을 사용할 것.
//
// [MISTAKE-16] 인디케이터 방향 계산 기준 불일치
//   상황: AttackIndicator(사각형)와 MeleeAttackAction 히트 판정 간 방향이 달라 범위 불일치
//   실수: 부채꼴은 Cos/Sin(x축 기준), 사각형은 Sin/Cos(y축 기준)으로 혼용.
//         -90f 보정을 추가했으나 혼란 가중.
//   교훈: 모든 방향 계산은 x축 기준(Atan2 / Cos·Sin 순서)으로 통일.
//         AttackIndicator와 MeleeAttackAction이 동일한 forward 계산식을 사용해야 함.
//
// [MISTAKE-17] 보스 bossScale 상속으로 인디케이터 크기 불일치
//   상황: 인디케이터가 시각적으로 크게 표시되나 실제 판정 범위는 작아 불일치
//   실수: AttackIndicator 루트가 보스 자식으로 lossyScale(2.5)을 상속받아
//         메시 range=2.0이 월드에서 5.0으로 렌더링됨.
//         히트 판정은 월드 단위 2.0 기준이라 범위가 완전히 달랐음.
//   교훈: 인디케이터 루트 localScale에 lossyScale 역수를 항상 적용할 것.
//         메시 좌표 = 월드 단위가 되어야 히트 판정과 일치.
//
// [MISTAKE-18] 버그 원인 미확정 상태에서 잘못된 방향으로 수정
//   상황: MeleeAttackAction 히트 판정이 잘 안 되는 버그 디버깅 중
//   실수: 증상(움직이면 안 맞음)을 보고 LockedPlayerPosition(Windup 시작 위치)을 추가.
//         이는 "Windup 중 회피해도 항상 피격"되는 기획 역행 로직임.
//         실제 원인(bossScale 스케일 상속으로 인디케이터 크기 불일치)과 무관했음.
//   교훈: 버그 원인을 디버그 로그로 먼저 확정한 뒤 수정 방향을 결정할 것.
//         기획 의도에 반하는 수정은 사용자에게 먼저 확인 후 진행할 것.
//
// [MISTAKE-19] 런타임 MonoBehaviour에서 AssetDatabase + #if UNITY_EDITOR 우회 코드 사용
//   상황: StageBuilder.cs / VirtualJoystick.cs 에서 빌드용 #else 분기로
//         Sprite.Create(Texture2D.whiteTexture, 4×4 Rect, ...) 반환
//   실수: 에디터에서는 정상 동작. 빌드(Android)에서는 4×4 흰 점 Sprite가
//         극단적 스케일(20×30 등)로 늘어나 스테이지 미렌더링, 조이스틱 사각형 출력.
//   교훈: TIP-06 참고. 런타임 MonoBehaviour에 AssetDatabase / #if UNITY_EDITOR 사용 금지.
//         런타임 Sprite는 Texture2D 직접 생성 / Resources.Load / SO 레퍼런스로만 조달할 것.
//
// [MISTAKE-20] Setup 툴에서 [SerializeField] 필드 연결 누락
//   상황: Phase 11 게임 루프 구현 중 Setup 툴 작성 시
//   실수: BossRushHudSetup이 BossRushManager._hudView를 주입하는 코드 누락.
//         LobbyHudSetup이 EventSystem을 생성하지 않아 버튼 클릭 불가.
//         Init() 방식 도입 후에도 SetDirty(컴포넌트) 누락으로 직렬화 미보장.
//   교훈: TIP-07 참고. Setup 툴 완성 전 [SerializeField] 필드 전체 연결 체크리스트 준수.
//         생성 컴포넌트 필드 + 씬 내 다른 컴포넌트 참조 필드 + EventSystem 여부를
//         1:1로 대조한 뒤 코드 작성할 것.
