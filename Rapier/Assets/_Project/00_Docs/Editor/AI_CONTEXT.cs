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
// [적 계층 구조]
//   EnemyPresenterBase (abstract) ← 모든 적의 공통 베이스 (Chase/Windup/Hit/PostAttack)
//   ├── NormalEnemyPresenter       ← 일반 적 (WaveManager 오브젝트 풀)
//   └── BossPresenterBase (abstract) ← 2페이즈 시스템, OnPhaseChanged 이벤트
//       ├── TitanBossPresenter     ← 직선 돌진 + 그로기
//       └── SpecterBossPresenter   ← 2페이즈 순간이동
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
//     │       ├── BossRushHudSetup.cs        ← 보스 러시 HUD Canvas 자동 생성
//     │       ├── BossStatDataCreator.cs     ← SO 에셋 + 씬 자동 조립
//     │       ├── PrefabMissingScriptCleaner.cs ← MissingScript 정리 유틸
//     │       └── AI_CONTEXT.cs              ← 이 파일
//     ├── 10_Scripts/
//     │   ├── Core/        (ServiceLocator, ICharacterView, IPlayerCharacter, Interfaces,
//     │   │                  Utils, CameraFollow, StageBuilder, VirtualJoystick,
//     │   │                  InputSystemInitializer)
//     │   ├── Input/       (GestureRecognizer)           ← UI 터치 필터링 포함
//     │   ├── Combat/      (IDamageable)
//     │   ├── Characters/  (CharacterPresenterBase, CharacterView, CharacterModel,
//     │   │                  CharacterStatData
//     │   │                  Rapier/RapierPresenter, Rapier/RapierStatData)
//     │   ├── Enemies/
//     │   │   ├── EnemyPresenterBase.cs      ← 모든 적 공통 베이스 (GetModel() 포함)
//     │   │   ├── NormalEnemyPresenter.cs    ← 일반 적 (기존 EnemyPresenter 대체)
//     │   │   ├── EnemyModel.cs
//     │   │   ├── EnemyView.cs
//     │   │   ├── EnemyHpBar.cs
//     │   │   ├── WaveManager.cs             ← NormalEnemyPresenter 풀 관리
//     │   │   ├── BossRushManager.cs         ← 보스 순서·사망 감지·스테이지 전환
//     │   │   └── Boss/
//     │   │       ├── BossPresenterBase.cs   ← 2페이즈 시스템, OnPhaseChanged
//     │   │       ├── TitanBossPresenter.cs  ← 직선 돌진·예고 인디케이터·그로기·벽 감지
//     │   │       └── SpecterBossPresenter.cs ← 순간이동
//     │   ├── UI/
//     │   │   ├── HUD/     (HudView, BossRushHudView)
//     │   │   └── VirtualJoystick.cs
//     │   └── Data/        (EnemyStatData, BossStatData, NormalEnemyStatData.asset)
//     ├── 20_Prefabs/
//     │   ├── Enemy_Template.prefab          ← NormalEnemyPresenter
//     │   ├── Rapier_Player.prefab
//     │   ├── Titan_Boss.prefab              ← TitanBossPresenter
//     │   └── Specter_Boss.prefab            ← SpecterBossPresenter
//     ├── 30_ScriptableObjects/
//     │   ├── Characters/  (RapierStatData.asset)
//     │   └── Enemies/
//     │       ├── NormalEnemyStatData.asset
//     │       └── Boss/
//     │           ├── TitanStatData.asset
//     │           └── SpecterStatData.asset
//     └── 40_Scenes/
//         ├── SampleScene.unity              ← 기존 웨이브 프로토타입
//         └── BossRushDemo.unity             ← 보스 러시 데모 (독립 씬)

// -------------------------------------------------------
// [5] 완료된 작업
// -------------------------------------------------------
// [Phase 1] 입력 시스템
//   InputState.cs / ServiceLocator.cs
//   GestureRecognizer.cs — Move/Tap/Swipe/Hold/JustDodge 판별
//   InputSystemInitializer.cs — ServiceLocator 등록
//
// [Phase 2] 캐릭터 베이스
//   ICharacterView.cs / CharacterStatData.cs / CharacterModel.cs
//   CharacterPresenterBase.cs / CharacterView.cs
//
// [Phase 3] 플레이어 / 씬 기반
//   CameraFollow.cs / StageBuilder.cs / VirtualJoystick.cs
//   GestureRecognizer 재설계 (Move/Swipe 판별 기준)
//
// [Phase 4] 적 시스템
//   IDamageable.cs / EnemyStatData.cs / EnemyModel.cs / EnemyView.cs
//   EnemyPresenter.cs / WaveManager.cs / Enemy_Template.prefab
//
// [Phase 5] UI 연결
//   HudView.cs / EnemyHpBar.cs (레이피어 표식 수 표시 포함)
//
// [Phase 6] 전투 고도화 + 저스트 회피 연출
//   저스트 회피 슬로우모션 / 공격 범위 인디케이터 / 적 공격 예고
//   무적 구간 / 카메라 줌 펀치 / 회피 쿨타임 HUD
//
// [Phase 7] 레이피어 캐릭터 고유 메커니즘 + 이동 시스템 리팩토링
//   이동 시스템 Presenter 주도 / MoveState / CanAttack / 무적 구간 재설계
//   저스트 회피 트리거 재설계 / 레이피어 스킬 시퀀스 / 스킬 공격 범위 독립화
//   공격 즉시 발동 / 입력 영역 제한 제거
//
// [Phase 8] 보스 러시 데모
//
//   [8-1] 적 계층 리팩토링
//     EnemyPresenter → EnemyPresenterBase(abstract) + NormalEnemyPresenter 로 분리.
//     RapierPresenter, EnemyHpBar, WaveManager 모두 EnemyPresenterBase 기반으로 수정.
//     RapierPresenter의 OnMarkChanged, 표식 테이블, 스킬 타겟이 EnemyPresenterBase 타입으로 통일.
//     PerformAttack/ShowAttackRangeIndicator — WaveManager 없을 시 BossRushManager 폴백 추가.
//
//   [8-2] 보스 시스템
//     BossStatData(SO) — EnemyStatData 상속, 2페이즈 스탯 배율·색상·스케일·전환 연출 시간.
//     BossPresenterBase — HP 50% 이하 진입 시 2페이즈 전환 루틴, OnPhaseChanged 이벤트.
//                         GetMoveSpeed/GetAttackPower override로 페이즈별 배율 자동 적용.
//     TitanBossPresenter:
//       1페이즈: 느린 근접 공격 (베이스 AI)
//       2페이즈: 일반 AI와 완전 교대로 직선 돌진 시퀀스 수행
//               ① 예고(WindupCharge): StageBuilder.RaycastToWall()로 벽까지 거리 계산,
//                  주황 선형 인디케이터를 벽까지 표시 (_chargeWindupDuration 초)
//               ② 직선 돌진: 고정 방향·고정 거리(벽까지). 플레이어 히트 시 데미지.
//               ③ 그로기: _grogyDuration(2.5초) 완전 정지
//               _isChargeSequence=true 동안 base.Update() 차단 → 일반 AI 중단
//     SpecterBossPresenter:
//       1페이즈: 빠른 추적
//       2페이즈: OnEnterWindup() override → 페이드 아웃/인 + 플레이어 주변 순간이동
//
//   [8-3] 보스 러시 매니저 & UI
//     BossRushManager — ServiceLocator 등록, 보스 배열 순서대로 Instantiate+Spawn,
//                       OnDeath → 승리 패널 or AllClear 패널 표시.
//                       GetCurrentBoss() — RapierPresenter의 스킬 타겟 폴백용.
//     BossRushHudView — 화면 상단 대형 HP바, 보스명·페이즈 텍스트, 스테이지 텍스트,
//                       승리 패널("다음 스테이지" 버튼), AllClear 패널.
//     BossRushHudSetup(Editor) — Screen Space Overlay Canvas 자동 생성 메뉴.
//
//   [8-4] BossRushDemo 씬
//     SampleScene과 독립. WaveManager 없이 BossRushManager 단독 운영.
//     EventSystem + InputSystemUIInputModule 포함 (UI 터치 필터링 필수).
//
//   [8-5] 버그 수정 목록
//     DodgeDashRoutine — MinSpeed(dashSpeed*0.05f) 보증 + 타임아웃으로 영구 잠금 방지.
//     DodgeDashCurve   — 끝값 0.00f → 0.50f (회피 후 즉시 이동 연결).
//     RapierPresenter  — _dashSkillStarted 플래그 분리:
//                         OnDodgeDashComplete는 _dashSkillStarted=true일 때만 억제.
//                         OnSlowMotionEnd에서 _isSkillSequenceActive도 함께 초기화
//                         (Hold 없이 슬로우 종료 시 영구 잠금 방지).
//     GestureRecognizer — IsPointerOverUI() 추가, HandleFingerDown에서 UI 위 터치 차단.
//     StageBuilder.RaycastToWall() — 타이탄 돌진 벽 감지용 AABB 교차 계산.

// -------------------------------------------------------
// [6] 미해결 이슈
// -------------------------------------------------------
// 현재 미해결 이슈 없음.

// -------------------------------------------------------
// [7] 다음 작업
// -------------------------------------------------------
// Phase 8 완료 (보스 러시 데모).
// Phase 9 후보:
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
// [MCP-04] batch_execute 는 manage_asset, execute_menu_item 미지원
//   순차 실행으로 대체.
//
// [MCP-05] 원인 미확정 상태에서 MCP 코드 수정 금지
//   에디터에서 직접 확인 가능한 사항(레이어, 콜라이더, 스프라이트 할당 등)은
//   사용자에게 먼저 질문하고, MCP 작업은 원인이 확정된 후에만 진행.
//
// [MCP-06] 구조적 파일은 delete -> create 전체 재생성
//   AI_CONTEXT, HudSetup 등 전체 구조가 중요한 파일은
//   apply_text_edits 부분 수정 대신 delete_script -> manage_script create 로 재생성.
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
//   교훈: 긴 메서드 교체 시 delete_script 후 manage_script create로 재생성하는 방식이 안전
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
//         delete_script 후 manage_script create로 전체 재생성하는 방식이 안전
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
