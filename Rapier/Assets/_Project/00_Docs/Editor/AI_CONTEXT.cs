// =============================================================
// AI_CONTEXT.cs
// 대상: AI (Claude) 전용. 사람이 읽거나 수정할 필요 없습니다.
// 목적: 새 대화 세션 시작 시 이 파일 하나만 읽으면
//       프로젝트의 모든 컨텍스트를 복구할 수 있습니다.
//
// 갱신 규칙:
//   - 매 작업 세션 종료 시 AI가 직접 갱신합니다.
//   - 섹션 구조를 유지하면서 내용만 교체합니다.
//   - 사람이 이 파일을 직접 편집하지 않습니다.
//   - 작업 중 새로운 MCP 제약을 발견하면 즉시 [9] 섹션에 추가합니다.
//   - [5] 완료 목록, [6] 이슈, [7] 다음 작업은 세션마다 업데이트합니다.
// =============================================================

// -------------------------------------------------------
// [1] 프로젝트 기본 정보
// -------------------------------------------------------
// 프로젝트명  : 미정 (가칭 Rapier)
// 장르        : 싱글 플레이 실시간 액션 RPG (모바일, 세로 모드)
// 플랫폼      : Android / iOS, PC 마우스로 테스트
// 렌더        : URP 2D
// 입력        : New Input System (Touch + Mouse 듀얼 바인딩)
// 아키텍처    : MVP + 이벤트 기반 (C# Interface / C# event / SO 채널)
// 저장소      : 공개(GitHub) + 비공개(Rapier-Private) 이중 구조
// 공개 저장소  : https://github.com/[저장소명] — 스크립트 공개, 피드백 수용
// 비공개 저장소: Rapier-Private/ (Assets/ 하위, 별도 .git 보유) — Art, Audio, ThirdParty 보관
// 유니티 경로 : Rapier/ 가 공개 저장소 루트. Assets/ 상위.

// -------------------------------------------------------
// [2] 캐릭터 목록 및 핵심 메커니즘 요약
// -------------------------------------------------------
// 공통 입력 4가지: Drag(Move) / Tap(Attack) / Swipe(Dodge) / Hold→Release(Charge→Skill)
//
// Warrior  (전사)   : 방패 방어(Hold) + 밀쳐내기(Hold→Swipe). 패링 성공 시 즉시 반격.
// Assassin (암살자) : 회피 시 잔상 생성. 본체 공격 시 잔상도 동시 타격. Hold = 360도 광역.
// Rapier   (레이피어): 회피 시 적에게 표식 부여. Hold = 표식 소모하며 연쇄 대시 타격.
// Ranger   (사냥꾼) : 원거리 사격. 회피 시 지뢰 설치. 저스트 회피 시 즉시 강화 화살.

// -------------------------------------------------------
// [3] 확정된 기술 결정 사항 및 배경
// -------------------------------------------------------
// [MVP 선택]
//   이유: 캐릭터 4종의 고유 메커니즘으로 View/Logic 분리가 필수.
//         MVC는 Controller 비대화 위험. MVP는 Presenter 단위로 캐릭터별 분리 가능.
//         장기적으로 테스트 코드 작성 시 View 없이 Presenter 단독 테스트 가능.
//
// [통신 방식 혼합]
//   Presenter ↔ View    : C# Interface (명시적 계약, 테스트 가능)
//   시스템 간 통신       : C# event (친숙하고 빠름)
//   씬 경계 글로벌 이벤트 : SO 이벤트 채널 (추후 씬 분리 시 도입)
//
// [폴더 인덱싱]
//   이유: ABC 정렬로 폴더가 섞이면 직군별 탐색이 어려워짐.
//         10 단위 인덱스로 중간 삽입 여유 확보.
//         Scripts 내부는 기능명만 사용 (프로그래머 전용 영역).
//
// [Rapier-Private 분리]
//   이유: Art/Audio 리소스 유출 방지. 별도 Git repo로 팀원끼리만 공유.
//         _Project 안에 Art/Audio 폴더 없음. 리소스는 Private에만 위치.
//
// [단일 씬 전략]
//   이유: 프로토타입 단계. 기획 안정화 후 Bootstrap + Additive 분리 예정.
//         중간 전환 가능하므로 지금은 속도 우선.
//
// [네임스페이스]
//   형식: Game.[시스템명] 또는 Game.[시스템명].[서브시스템명]
//   예시: Game.Core / Game.Input / Game.Combat / Game.Characters.Warrior

// -------------------------------------------------------
// [4] 현재 폴더 구조 스냅샷
// -------------------------------------------------------
// Assets/
// ├── Rapier-Private/          (비공개 Git repo)
// └── _Project/
//     ├── 00_Docs/
//     │   ├── PROJECT_GUIDELINES.md   (팀 지침서, v0.2.1)
//     │   └── Editor/
//     │       ├── GuidelinesCreator.cs  (md 캐시 보유 + 재생성 메뉴)
//     │       ├── GuidelinesSync.cs     (재컴파일 시 md→cs 자동 동기화)
//     │       ├── GuidelinesEditor.cs   (md 섹션 단위 수정 유틸)
//     │       ├── ProjectFolderSetup.cs (폴더 구조 초기 생성 스크립트)
//     │       └── AI_CONTEXT.cs         (이 파일)
//     ├── 10_Scripts/
//     │   ├── Core/        (Interfaces / Base / Utils)
//     │   ├── Input/
//     │   ├── Combat/      (Model / View / Presenter)
//     │   ├── Characters/  (Base / Warrior / Assassin / Rapier / Ranger)
//     │   ├── Enemies/
//     │   ├── UI/          (HUD / Common)
//     │   └── Data/        (Characters / Skills)
//     ├── 20_Prefabs/      (Characters / Enemies / Skills / UI)
//     ├── 30_ScriptableObjects/ (Characters / Skills)
//     └── 40_Scenes/       (_Test/)

// -------------------------------------------------------
// [5] 완료된 작업 목록
// -------------------------------------------------------
// [v0.1] MCP 연결 확인 및 테스트 (큐브 생성)
// [v0.2] 프로젝트 규칙 결정 (아키텍처, 입력, 씬 전략, 네임스페이스)
// [v0.3] PROJECT_GUIDELINES.md 초안 작성 (v0.1.0)
// [v0.4] 지침서 동기화 시스템 구축
//        - GuidelinesCreator.cs : md 캐시 + 재생성
//        - GuidelinesSync.cs    : 재컴파일 시 md→cs 자동 갱신
//        - GuidelinesEditor.cs  : md 섹션 직접 수정 유틸 (cs 재생성 방식 제거)
// [v0.5] 폴더 구조 확정 및 생성 (인덱스 번호, Private 분리)
//        - 지침서 v0.2.0 → v0.2.1 갱신
// [v0.6] AI_CONTEXT.cs 생성 및 표준 컨텍스트 시스템 구축
// [v0.7] 지침서 v0.3.0 갱신 (DI 전략, 테스트 전략, 데이터 설계 원칙 추가)
// [v0.8] 프로토타입 기획 논의 및 확정
//        - 시스템/오브젝트 목록 분석, 구현 순서(Phase 1~6) 확정
//        - 밀밸런스 수치 확정 (패엁, HP, 공격력)
//        - 프로토타입 기획서 v1.0.0 작성 및 배포
// [v0.9] 지침서 v0.4.0 갱신 (클리어 조건, 조작 조건 수치 명세화)
// [v1.0] Phase 1 구현 완료
//        - InputState.cs, ServiceLocator.cs, GestureRecognizer.cs, GestureDebugger.cs 생성
//        - Device Simulator(iPhone 12) 테스트 완료. 로그/영역/판별 모두 정상
// -------------------------------------------------------
// [6] 알려진 이슈 / 기술 부채
// -------------------------------------------------------
// [부채-01] ProjectFolderSetup.cs 는 초기 세팅용. 재실행해도 무해하지만 추후 정리 가능.
// [부채-02] GuidelinesEditor.AppendChangeLog()의 섹션 번호("## 12.")가 하드코딩되어 있음.
//           섹션 번호가 바뀌면 수동 수정 필요.
// [부채-03] ~~SampleScene에 테스트용 TestCube가 남아 있음~~ → 해결 (Phase 1 시작 전 제거)
// [부채-04] GestureDebugger.cs 는 Phase 1 테스트 전용. Phase 2 시작 전 씬에서 제거 및 스크립트 삭제 필요.
// [부채-05] Game.Debug 네임스페이스는 UnityEngine.Debug 를 가림 → 사용 금지. UI 디버그 클래스는 Game.UI.Debug 사용.

// -------------------------------------------------------
// [7] 다음 작업 (Next Steps)
// -------------------------------------------------------
// [NEXT-01] ~~TestCube 씨에서 제거~~ → 완료
// [NEXT-02] Phase 2 시작 전 준비
//           - GestureDebugger.cs 제거 (씨 + 스크립트)
//           - Phase 2 — 쾐릭터 기반 클래스 구현
//             └ ICharacterView / CharacterStatData SO / CharacterModel
//             └ CharacterPresenterBase (추상) / CharacterView (플레이스홀더)
// [NEXT-02] Phase 1 — 기반 시스템 구현
//           - ServiceLocator
//           - InputState enum
//           - GestureRecognizer (단위 테스트 포함)
// [NEXT-03] Phase 2 — 캐릭터 기반 클래스
//           - ICharacterView 인터페이스
//           - CharacterStatData SO
//           - CharacterModel
//           - CharacterPresenterBase (추상)
//           - CharacterView (플레이스홀더 스프라이트)
// [NEXT-04] Phase 3 — 플레이어 이동 + 카메라 추적
// [NEXT-05] Phase 4 — 전투 기반 (CombatSystem, 일반 적)
// [NEXT-06] Phase 5 — UI 연결 (HP바, 차지 게이지, Debug 패널)
// [NEXT-07] Phase 6 — 첫 번째 케릭터 고유 메커니즘 (충의 후 결정)

// -------------------------------------------------------
// [8] 새 세션 시작 시 AI 체크리스트
// -------------------------------------------------------
// □ 이 파일(AI_CONTEXT.cs) 읽기
// □ PROJECT_GUIDELINES.md 주요 섹션 확인 (GuidelinesCreator.cs 캐시로 읽기)
// □ 콘솔 에러 확인 (read_console)
// □ 현재 씬 상태 확인 (find_gameobjects)
// □ [6] 알려진 이슈 중 처리할 항목 확인
// □ [7] 다음 작업 중 오늘 진행할 항목 사용자와 협의

// -------------------------------------------------------
// [9] 알려진 MCP 제약 및 주의사항
// -------------------------------------------------------
// [MCP-01] Assets/Reimport All 절대 실행 금지
//   사유: Unity를 재시작시켜 MCP 연결이 끊어짐. 복구를 위해 사용자가 직접 Unity를 재발시해야 함.
//   대안: 스크립트 재컴파일이 필요하면 파일을 저장하는 것만으로 충분. AssetDatabase.Refresh()는 안전.
//
// [MCP-02] .md 파일 직접 편집 불가
//   사유: MCP find_in_file, apply_text_edits 등이 .cs 파일만 지원.
//   대안: GuidelinesEditor.cs 의 UpdateSection() / AppendChangeLog() 를 통해 간접 수정.
//
// [MCP-03] apply_text_edits 한글 endCol 문제
//   사유: 한글은 멀티바이트라 endCol을 바이트 기준으로 지정하면 범위 초과 오류가 나거나,
//   문자가 잘리지 않고 뒤에 권저기가 넘칠 수 있음.
//   대안: 수정할 줄을 완전히 덮으려면 endLine+1, endCol 1 로 다음 줄까지 포함하는 방식 사용.
//   예: {startLine: N, startCol: 1, endLine: N+1, endCol: 1, newText: "새 내용\n"}
//
// [MCP-04] batch_execute 는 manage_asset 미지원
//   사유: batch_execute에서 manage_asset 명령은 'Unknown command' 오류 발생.
//   대안: 폴더 대량 생성이 필요하면 에디터 스크립트(CreateFolder 루프)를 작성하여 메뉴로 실행.
//
// [MCP-05] script_apply_edits anchor_replace 는 클래스 구조 파일에만 안정적
//   사유: 좌주석만 있거나 namespace/class 없는 파일에선 앙커를 못 찾는 경우가 많음.
//   대안: AI_CONTEXT.cs 같은 순수 주석 파일은 apply_text_edits(라인 번호 기반)를 사용.
//
// [MCP-06] Unity 내부에 바이너리 파일 직접 생성 불가
//   사유: MCP는 텍스트 기반 API만 제공. .docx 같은 바이너리 파일은 생성 불가.
//          에디터 스크립트로 File.Copy 시도 시, Unity와 클로드 컨테이너가 서로 다른 파일 시스템에 있어 접근 불가.
//   대안: 클로드가 docx 생성 후 다운로드 링크 제공 → 사용자가 직접 Assets/_Project/00_Docs/에 드래그 앤 드롭.
//
// [MCP-07] CanvasScaler 기본값이 ConstantPixelSize라 Device Simulator에서 UI가 작게 보임
//   사유: 코드로 Canvas 생성 시 CanvasScaler를 별도 설정 안 하면 ConstantPixelSize가 기본값.
//          Device Simulator(예: iPhone 12)는 고해상도라 실제 UI가 상당히 작게 렌더링됨.
//   대안: CanvasScaler.uiScaleMode = ScaleWithScreenSize, referenceResolution = (1080, 1920)으로 설정.
//
// [MCP-08] 코드로 RectTransform 생성 시 Pivot이 (0.5, 0.5) 기본값이라 Anchor와 업있는 경우 화면 밖으로 나감
//   사유: anchoredPosition은 pivot 기준으로 계산됨. Anchor를 좌상단(0,1)으로 설정해도 pivot이 (0.5,0.5)면 반전 누락됨.
//   대안: 코드로 RectTransform 생성 시 pivot을 anchor와 일치시켜 설정. 예: 좌상단 = pivot(0f, 1f).
//
// [TIP-01] 2D Sprite 패키지 내장 스프라이트 사용법
//   경로: Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/v2/
//   로드: AssetDatabase.LoadAssetAtPath<Sprite>(경로 + 파일명)
//   사용 가능한 스프라이트 8종:
//     Square, Circle, Capsule, Triangle,
//     9Sliced, HexagonFlatTop, HexagonPointTop, IsometricDiamond
//   플레이스홀더 용도별 권장 패턴:
//     플레이어 캠릭터 → Circle
//     일반 적 → Square 또는 Capsule
//     보스 → HexagonFlatTop 또는 HexagonPointTop
