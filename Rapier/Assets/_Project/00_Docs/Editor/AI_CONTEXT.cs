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
// 저스트 회피: 적 공격 타이밍에 정확히 Swipe 시 발동 → 게임 슬로우 + 캐릭터 고유 스킬 즉시 발동
//
// Warrior  : Hold 중 방패 방어 (피해 감소/무효화) 추가. Hold+Swipe = 방패 밀쳐내기. 패링 성공 시 즉시 반격.
// Assassin : 저스트 회피 시 회피 전 위치에 잔상 생성 (피해/어그로 없음).
//            잔상 활성 중 본체의 모든 공격(Tap, 차지 스킬)에 잔상이 동시에 동참.
//            고유 스킬 / 차지 스킬 = 360도 광역 공격.
// Rapier   : 저스트 회피 후 고유 스킬 = 공격한 적에게 대시 → 표식 부여 + 데미지 → 원위치 복귀. 표식 최대 5중첩.
//            차지 스킬 = 표식 있는 모든 적을 고속 관통 공격. 각 적은 보유 표식 중첩 수만큼 피해.
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
// [폴더 인덱싱]
//   10 단위 숫자 접두사로 ABC 정렬 혼용 방지. Scripts 내부는 기능명만 사용.
//
// [단일 씬 전략]
//   프로토타입 단계. 기획 안정화 후 Bootstrap + Additive 분리 예정.
//
// [네임스페이스]
//   형식: Game.[시스템명]   예: Game.Core / Game.Input / Game.Combat / Game.Characters

// -------------------------------------------------------
// [4] 폴더 구조 스냅샷
// -------------------------------------------------------
// Assets/
// ├── Rapier-Private/
// └── _Project/
//     ├── 00_Docs/
//     │   ├── PROJECT_GUIDELINES.md          (팀 지침서 v0.2.1)
//     │   ├── Rapier_Prototype_DesignDoc.md  (기획서 원본 v1.3.0)
//     │   ├── Rapier_Prototype_DesignDoc.docx (팀원용 뷰어, 자동 생성)
//     │   └── Editor/
//     │       ├── DocSyncTool.cs
//     │       ├── ProjectFolderSetup.cs
//     │       ├── HudSetup.cs
//     │       └── AI_CONTEXT.cs         <- 이 파일
//     ├── 10_Scripts/
//     │   ├── Core/        (ServiceLocator, Interfaces, Utils, CameraFollow)
//     │   ├── Input/       (GestureRecognizer, InputSystemInitializer)
//     │   ├── Combat/      (IDamageable)
//     │   ├── Characters/  (Base, Warrior, Assassin, Rapier, Ranger)
//     │   ├── Enemies/     (EnemyModel, EnemyView, EnemyPresenter, WaveManager, EnemyHpBar)
//     │   ├── UI/HUD/      (HudView)
//     │   ├── DevTools/    (JustDodgeDebugger — #if UNITY_EDITOR 전용)
//     │   └── Data/        (CharacterStatData, EnemyStatData, NormalEnemyStatData.asset)
//     ├── 20_Prefabs/      (Enemy_Template.prefab)
//     ├── 30_ScriptableObjects/
//     └── 40_Scenes/

// -------------------------------------------------------
// [5] 완료된 작업
// -------------------------------------------------------
// [Phase 1] 입력 시스템
//   InputState.cs / ServiceLocator.cs
//   GestureRecognizer.cs — 하단 40% 유효영역, Move/Tap/Swipe/Hold/JustDodge
//   InputSystemInitializer.cs — ServiceLocator 등록
//
// [Phase 2] 캐릭터 베이스
//   ICharacterView.cs — PlayAttack/PlayHit/PlayDodge/PlayDeath/UpdateHpGauge/UpdateChargeGauge
//   CharacterStatData.cs (SO) / CharacterModel.cs / CharacterPresenterBase.cs / CharacterView.cs
//   ※ CharacterView.UpdateHpGauge/UpdateChargeGauge 는 캐릭터 오브젝트 고유 시각 연출용 훅.
//     HudView와 역할이 다르므로 제거하지 않는다 (Phase 6에서 캐릭터별 연출로 구현 예정).
//
// [Phase 3] 플레이어 / 씬 기반
//   PlayerPresenter.cs / CameraFollow.cs / StageBuilder.cs / VirtualJoystick.cs
//   GestureRecognizer 재설계:
//     Move  — dist >= 20px AND duration >= 0.25초
//     Swipe — dist >= 60px AND duration < 0.25초 (FingerUp 시)
//     이벤트 연결은 모두 Start()에서 수행 (Awake 순서 문제 해결)
//
// [Phase 4] 적 시스템
//   IDamageable.cs / EnemyStatData.cs / EnemyModel.cs / EnemyView.cs
//   EnemyPresenter.cs — 추적 AI, AttackWindow 카운터
//   WaveManager.cs    — 10초 웨이브, 오브젝트 풀, GetNearestEnemy
//   Enemy_Template.prefab — Enemy 레이어(8번), BoxCollider2D
//   GestureRecognizer: _attackWindowCount int 카운터 (bool→int)
//
// [Phase 5] UI 연결
//   HudView.cs — Player에 부착. Start()에서 자식 Canvas 내 이름으로 자동 탐색.
//                Model.OnHpChanged     → HpFill.fillAmount
//                Model.OnChargeChanged → ChargeGaugeFill.fillAmount
//   EnemyHpBar.cs — 적 위 World Space HP 바. EnemyModel.OnHpChanged 구독.
//   EnemyPresenter.cs 수정 — Awake에서 GetComponentInChildren<EnemyHpBar>, Spawn()에서 Init()
//   PlayerPresenter.cs 수정 — PublicModel 프로퍼티 추가 (HudView 접근용)
//   HudSetup.cs — Rapier/Setup 메뉴로 씬 HUD 자동 생성
//   씬 오브젝트 구조:
//     Player > PlayerHudCanvas (World Space, localScale=0.01, sizeDelta=200x200)
//       > HpBarBg      (Square 스프라이트) > HpFill (Horizontal fill, fillAmount=1)
//       > ChargeGaugeBg  (Circle 스프라이트, Radial360, fillAmount=1)
//       > ChargeGaugeFill (Circle 스프라이트, Radial360, fillAmount=0->1)
//       > DodgeCooldownBg (Square 스프라이트, 14x60유닛, 우측 +90)
//           > DodgeCooldownFill (Vertical fill, Bottom origin, 노랑, fillAmount=0->1)
//     Enemy_Template.prefab > EnemyHpBarCanvas (World Space)
//       > HpBg > HpFill (Horizontal fill) + EnemyHpBar 컴포넌트
//   프리팹 경로: Assets/_Project/20_Prefabs/Enemy_Template.prefab
//
// [Phase 6] 전투 고도화 + 저스트 회피 연출
//   [6-1] 저스트 회피 슬로우모션 (CharacterPresenterBase)
//     - AnimationCurve slowCurve (Inspector 조정, 기본: 0->1.0, 0.5->0.15, 0.75->0.05, 1.0->1.0)
//     - slowDuration = 3f (unscaledTime 기반)
//     - SlowMotionRoutine(): 중복 발동 시 재시작, 사망/비활성 시 timeScale 복구
//
//   [6-2] 무적 프레임 + 공격 딜레이 + 범위 가시화 (CharacterPresenterBase)
//     - Swipe 시 InvincibleRoutine(): 0.2초 무적 (unscaledTime)
//     - AttackRoutine(): WaitForSecondsRealtime(0.5초) 딜레이, 슬로우 영향 없음
//     - _isAttacking 플래그: 딜레이 중 연타 차단
//     - 공격 범위 인게임 가시화: 반투명 노란 사각형 스프라이트 (동적 생성)
//     - Gizmo 시각화 유지 (에디터 전용)
//
//   [6-3] 적 공격 예고 시스템 (EnemyView, EnemyPresenter, EnemyStatData)
//     - EnemyStatData: attackWindupDuration = 0.5f 추가
//     - EnemyView.PlayWindup(duration, attackRange): 색상 빨강 보간 + 원형 범위 스프라이트
//     - EnemyView.StopWindup(): 색상 복구 + 범위 스프라이트 숨김
//     - EnemyPresenter 공격 흐름: Windup -> Hit -> None
//
//   [6-4] PlayerPresenter TakeDamage 재설계
//     - IsInvincible == true  -> 피해 무시 + ForceJustDodge(knockbackDir * -1f)
//     - IsInvincible == false -> Model.TakeDamage() + View.PlayHit()
//
//   [6-5] 카메라 줌 펀치 (CameraFollow)
//     - ServiceLocator 등록/해제
//     - TriggerZoomPunch(): zoomCurve(Inspector) 기반 orthographicSize 변화
//     - unscaledTime 기반, 중복 발동 시 재시작
//     - HandleJustDodge()에서 호출
//
//   [6-6] 회피 쿨타임 (CharacterPresenterBase, CharacterStatData, CharacterModel)
//     - CharacterStatData: dodgeCooldown = 2f 추가
//     - CharacterModel: DodgeCooldownRatio 프로퍼티 + OnDodgeCooldownChanged 이벤트 추가
//     - DodgeCooldownRoutine(): unscaledTime 기반 0->1 비율 전달
//     - 쿨타임 중 HandleSwipe() 차단
//
//   [6-7] 회피 쿨타임 HUD (HudView, HudSetup)
//     - DodgeCooldownFill: Vertical fill, Bottom origin, 노랑
//     - DodgeCooldownBg: ratio >= 1f -> SetActive(false), ratio <= 0f -> SetActive(true)
//     - 시작 시 즉시 숨김 (사용 가능 상태)
//
//   [6-8] JustDodgeDebugger (DevTools/JustDodgeDebugger.cs)
//     - #if UNITY_EDITOR 전용, 씬 배치 컴포넌트
//     - Space 키 -> OpenAttackWindow() -> ForceJustDodge(Vector2.up) -> CloseAttackWindow()
//     - GestureRecognizer에 ForceJustDodge(Vector2 direction) 추가 (#if UNITY_EDITOR)

// -------------------------------------------------------
// [6] 미해결 이슈
// -------------------------------------------------------
// 현재 미해결 이슈 없음.

// -------------------------------------------------------
// [7] 다음 작업
// -------------------------------------------------------
// [NEXT-01] 완료 — Phase 5 부록: 공격 범위 Gizmo 시각화 + ISSUE-01 해결
// [NEXT-02] 완료 — Phase 6 전투 고도화 + 저스트 회피 연출 (6-1 ~ 6-8)
// [NEXT-03] Phase 7 — 레이피어 캐릭터 고유 메커니즘
//   예정 순서:
//   1. EnemyModel에 Mark 시스템 추가 (MarkCount 0~5, AddMark, ConsumeAllMarks, OnMarkChanged)
//   2. RapierPresenter.cs 작성
//      - OnJustDodge: 공격한 적 저장 (스킬 타겟)
//      - OnSkillRelease(justDodgeReady=true): 타겟 적에게 대시 -> 표식 부여 + 데미지 -> 원위치 복귀
//      - OnSkillRelease(fullyCharged=true): 표식 보유 적 전체 고속 관통 공격 (중첩 수 x 데미지)
//   3. RapierStatData SO 생성 (표식 데미지, 차지 배율 등)
//   4. 씬 배치: PlayerPresenter -> RapierPresenter 교체
//   5. 표식 시각화: EnemyHpBar에 표식 개수 텍스트 표시

// -------------------------------------------------------
// [8] MCP 운영 제약 및 팁
// -------------------------------------------------------
// [MCP-01] Assets -> Reimport All 절대 실행 금지
//   Unity 재시작 -> MCP 연결 끊김. 재컴파일은 파일 저장으로 충분.
//
// [MCP-02] .md 파일 직접 편집 불가 — 템플릿 교체 방식으로 운영
//   mcpforunity는 확장자를 무조건 .cs로 치환한다.
//   apply_text_edits, manage_script 등 모든 MCP 도구는 .cs 외 파일에 접근 불가.
//   기획서(.md)의 실질적 원본은 DocSyncTool.cs 안의 GetDesignDocTemplate() 이다.
//   .md 파일은 해당 템플릿으로부터 덮어쓰기 생성되는 파생 파일이다.
//
//   기획서 수정 워크플로우:
//     1. DocSyncTool.cs 의 GetDesignDocTemplate() 내용을 수정
//     2. execute_menu_item('Rapier/Docs/Create DesignDoc MD') -> .md 덮어쓰기
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

// -------------------------------------------------------
// [10] AI 실수 기록
// -------------------------------------------------------
// 재작업이 발생한 경우 아래에 누적 기록한다.
// 기록 전 채팅으로 내용을 보고하고 사용자 승인 후 추가한다.
// 형식: [MISTAKE-N] 제목
//        상황: 언제/어디서
//        실수: 무엇을 잘못했는가
//        교훈: 앞으로 어떻게 할 것인가
//
// [MISTAKE-01] execute_menu_item 미사용으로 사용자에게 수동 실행 요청
//   상황: HUD 셋업 메뉴(Rapier/Setup/Rebuild HUD Canvas) 실행 시
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
