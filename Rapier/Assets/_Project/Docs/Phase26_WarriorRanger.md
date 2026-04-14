# Phase 26 — Warrior + Ranger 병렬 추가

**작성일**: 2026-04-14
**기준 커밋**: develop @ 77d3d69
**완료일**: 2026-04-14 (세션 15, develop @ d99820d)
**상태**: **코드 구현 전부 완료. Unity 플레이 테스트 + 비주얼 에셋 수동 할당 대기.**
**목표**: 기존 Rapier/Assassin 2종 라인업에 Warrior/Ranger 를 추가, 4종 체제 완성.
**참조 문서**: `Domains/CHARACTERS.md` §3 (갱신 완료), `Domains/INPUT.md` §3-1 (신설)

---

## 1. 전체 구조

| Phase | 범위 | 브랜치 | 커밋 | 상태 |
|---|---|---|---|---|
| 26-A | GestureRecognizer 확장 (Hold 후 Drag/Swipe 이벤트 3종) | `phase-26a-input-holdext` | c97268a | ff-merged (세션 14) |
| 26-B | CharacterModel 방향성 방어 API + Warrior 구현 | `phase-26b-warrior` | ca7680e | ff-merged (세션 15) |
| 26-C | Ranger 구현 (투사체/지뢰/차지 조준) | `phase-26c-ranger` | 08cb985 | ff-merged (세션 15, rebase onto 26-B 후) |
| 26-D | 캐릭터 선택 UI 활성화 + CharacterSpawner 등록 + 프리팹/Stat SO 에셋 생성 | `phase-26d-integration` | d99820d | ff-merged (세션 15) |

**병렬 포인트**: 26-A 를 먼저 ff-머지한 뒤, 26-B 와 26-C 를 **병렬 워크트리**로 동시 진행 가능. 충돌 영역은 26-D 에서 통합.

---

## 2. Phase 26-A — GestureRecognizer 확장 (선행, 단독)

### 범위
- `GestureRecognizer` 에 이벤트 3종 추가: `OnHoldDragUpdate(Vector2 fromStart)`, `OnHoldSwipe(Vector2 direction)`, `OnHoldRelease(Vector2 fromStart, bool chargedFull)`
- 기존 `OnRelease(InputState last)` 는 유지 (Rapier/Assassin 호환). 새 이벤트는 추가분.
- Hold 성립 이후의 손가락 이동/이탈을 새 경로로 라우팅:
  - Hold 성립 후 `FingerMove` 에서 거리 제한 해제 (기존 TAP_MAX_DISTANCE 판정 bypass)
  - 매 프레임 `OnHoldDragUpdate(_currentPos - _startPos)` 발행
  - Hold 중 **SWIPE_MIN_DISTANCE 이상 이동 + SWIPE_MAX_DURATION 이내** 완료 감지 → `OnHoldSwipe(direction)` 단발 발행, 이후 같은 터치는 "소비됨" 플래그로 Release 차단
  - 손가락 뗄 때 (소비되지 않았다면) `OnHoldRelease(fromStart, chargedFull)` 발행. `chargedFull` 은 Presenter 가 판단할 수 없으니 `GestureRecognizer.SetChargedFull(bool)` 세터를 Presenter 가 호출해 미리 세팅

### 구현 세부
- 내부 상태 추가: `_holdConsumed` (Swipe 발행 시 true), `_holdChargedFull` (Presenter 세팅), Swipe 감지용 `_holdSwipeStartTime`, `_holdSwipeStartPos`
- Hold 성립 순간(`CurrentState=Hold` 전환) 에 `_holdSwipeStartTime=now, _holdSwipeStartPos=_currentPos` 초기화
- 매 프레임 `dist(_currentPos, _holdSwipeStartPos)` 체크 — SWIPE_MIN_DISTANCE 도달 시 경과시간 확인 → 통과면 `OnHoldSwipe` 발행. 미통과 시 `_holdSwipeStartPos=_currentPos, _holdSwipeStartTime=now` 로 윈도우 리셋 (느린 Drag 는 Swipe 아님)
- `FingerUp` 시 `_holdConsumed==false && CurrentState==Hold` 면 `OnHoldRelease` 발행

### 검증
- Rapier/Assassin 회귀 테스트: 기존 Release 이벤트로 차지 스킬 정상 발동
- 새 이벤트 구독자 없는 상태에서 컴파일·런타임 오류 없음

### 커밋 형식
`[Phase 26-A] GestureRecognizer Hold 후 Drag/Swipe 확장 이벤트 추가`

---

## 3. Phase 26-B — Warrior

### 선행 의존
26-A ff-머지 완료 후 시작.

### 범위
1. **CharacterModel 방향성 방어 API**:
   - `SetDirectionalGuard(Vector2 shieldNormal, float halfAngleDeg, Action onParry)` / `ClearDirectionalGuard()`
   - `TakeDamage(float amount, Vector2 attackDir)` 경로에서, 방어 활성 상태면 `dot(-attackDir, shieldNormal) ≥ cos(halfAngle)` 판정 → 데미지 무효 + `onParry` 콜백 호출 (1회성)
   - 기존 `IsInvincible` 와 **독립적**으로 동작 (둘 중 하나만 true 여도 차단)

2. **WarriorPresenter** (`Scripts/Characters/Warrior/`):
   - `OnHoldDragUpdate` 무시, `OnHoldSwipe` + `OnHoldRelease` 구독
   - Hold 차지 중 데미지 감소 50% — `CharacterModel.SetDamageMultiplier(0.5f)` 같은 세터 추가 or Presenter 에서 TakeDamage 인터셉트
   - 차지 Full 판정 후 `GestureRecognizer.SetChargedFull(true)` 호출
   - `OnHoldSwipe(direction)`: chargedFull==true 면 방패 휘두르기 루틴 시작
     - `SetDirectionalGuard(direction, 60f, OnParry)` 세팅
     - 히트박스 1회 생성 (기존 `PerformSkillAttack` 패턴 재사용): `ATK × 150%` + 넉백 2.0
     - `dodgeDashDuration` 후 `ClearDirectionalGuard()`
   - `OnHoldRelease(fromStart, chargedFull)`: chargedFull==true 면 대지 분쇄 (전방 광역, `ATK × 350%`)
   - `OnParry` 콜백: 슬로우 진입 + 즉시 대지 분쇄 (`OnHoldRelease` chargedFull 분기 재사용)
   - **저스트 회피 비활성화**: `GestureRecognizer.OpenAttackWindow` 호출 생략 (회피 대시 중에도 AttackWindow 열지 않음)

3. **WarriorStatData** (`ScriptableObjects/Characters/` 아래에 SO 에셋은 26-D 에서 생성, 클래스는 여기서):
   - 필드는 `Domains/CHARACTERS.md` §3 Warrior 섹션 표 참조

### 잠금-해제 grep 검증
- `SetDirectionalGuard` → `ClearDirectionalGuard` 의 모든 종료 경로 (정상 완료 / 사망 / 씬 전환 / OnDisable) 페어링
- 데미지 감소 50% 진입 → 해제 (차지 취소 / Full 전환 / 사망 / OnDisable)
- `SetChargedFull(true)` → 소비(Release/Swipe) 시 내부 리셋 (`SetChargedFull(false)`)

### 커밋 형식
`[Phase 26-B] Warrior 추가 — 방향성 방어 API + 차지 후 Swipe 패링`

---

## 4. Phase 26-C — Ranger

### 선행 의존
26-A ff-머지 완료 후 시작. 26-B 와 병렬 가능 (수정 영역 미겹침).

### 범위

1. **RangerProjectile** (`Scripts/Characters/Ranger/RangerArrow.cs`):
   - MonoBehaviour, 직선 이동, 관통 적 `HashSet<EnemyModel>` 로 중복 타격 방지
   - 사거리 도달 / 스테이지 경계 이탈 시 despawn (기존 투사체 경계 despawn 패턴 재사용 — 세션 13 커밋 096f2b3 참고)
   - 파라미터: damage, speed, range, width, direction. `Physics2D.OverlapBoxAll` 매 프레임 히트 체크

2. **RangerMine** (`Scripts/Characters/Ranger/RangerMine.cs`):
   - 씬에 배치, 수명 5초, `OnTriggerEnter2D(enemy)` → 반경 1.5 unit `OverlapCircleAll` 광역 데미지 → 자폭
   - `RangerPresenter` 가 최대 6개 큐 관리 (Assassin 잔상 패턴 참조 — `AssassinPresenter` 의 phantom queue)

3. **RangerPresenter** (`Scripts/Characters/Ranger/RangerPresenter.cs`):
   - `OnHoldDragUpdate(fromStart)` 구독 → `_aimDirection = fromStart.normalized` 저장 (zero 면 전방 유지)
   - Hold 성립 시 본인 경직 진입 (`_isChargeLocked=true`). 이동/회피/Tap 전부 차단 (`CanAttack` / `CanDodge` override)
   - `OnHoldRelease(fromStart, chargedFull)`:
     - 차지량 t = `_chargeProgress` (0~1, 기존 차지 게이지 재사용)
     - damage% = lerp(100, 300, t), range = lerp(4, 14, t), widthMult = lerp(1, 3, t)
     - `_aimDirection` 으로 RangerArrow 생성 (관통 무제한)
     - 경직 해제
   - **Tap override**: 근접 공격 대신 RangerArrow 발사 (damage 100%, range 8, speed 25, width 기본)
   - **OnDodgeComplete override**: 회피 종료 지점에 RangerMine 설치. 큐에 push, max 6 초과 시 최고참 Destroy
   - **OnJustDodge** (저스트 회피) 후 Hold→Release: 강화 관통 화살 (damage 300%, range 10, width×3) — Rapier 패턴 재사용
   - **AimIndicatorView**: 차지 중 캐릭터→조준 방향, 길이=lerp(4, 14, t) 의 라인/화살표 렌더링. 기존 HudSetup 패턴 참조해 별도 GameObject

4. **RangerStatData** 클래스 정의. 필드 표는 CHARACTERS.md 참조.

### 잠금-해제 grep 검증
- 차지 경직 진입 (`_isChargeLocked=true`) → 해제 (Release / Hold 취소 / 사망 / OnDisable)
- RangerMine 큐 → 씬 전환 시 전부 Destroy
- AimIndicatorView 활성 → 비활성 (Release / 사망 / OnDisable)

### 커밋 형식
`[Phase 26-C] Ranger 추가 — 투사체/지뢰/바루스식 차지 조준`

---

## 5. Phase 26-D — 통합

### 선행 의존
26-B, 26-C 둘 다 ff-머지 완료 후 시작.

### 범위
1. **Stat SO 에셋 생성**: `ScriptableObjects/Characters/WarriorStatData.asset`, `RangerStatData.asset` — 기본값은 CHARACTERS.md 표대로. 일러스트는 Rapier-Private 에서 별도 수급 (현재 Rapier/Assassin 패턴 그대로 `illustSprite` 할당)
2. **프리팹 생성**: `Prefabs/Player/WarriorPlayer.prefab`, `RangerPlayer.prefab` — Rapier 프리팹 복제 후 Presenter 스왑
3. **CharacterSpawner / CharacterSelectModal / CharacterInfoPanel 등록**:
   - 현재 "Coming Soon" 잠금으로 표시되는 2슬롯을 Warrior/Ranger 에 할당
   - Lobby 에서 선택 가능하도록 CharacterSO 등록
4. **플레이 테스트 및 밸런스 1차 조정**

### 커밋 형식
`[Phase 26-D] Warrior/Ranger 통합 — 프리팹/SO/선택 UI 등록`

---

## 6. 공통 주의사항 (위임 에이전트 프롬프트에 반드시 포함)

- 워크트리 절대경로, 브랜치명 명시
- `git -C "<절대경로>"` 사용, `cd` 금지
- 수정 허용 폴더: 26-B 는 `Scripts/Characters/Warrior/` + `Scripts/Characters/Base/CharacterModel.cs` (방향성 방어 API). 26-C 는 `Scripts/Characters/Ranger/` 만. 다른 캐릭터 폴더 수정 금지
- 공용 Base 수정 (CharacterPresenterBase) 는 **양쪽 Phase 모두 금지**. 기존 virtual/override 계약만 사용. 새 훅 필요 시 팀장에게 보고
- 컴파일 검증 불가 (Unity 환경) → C# 정적 문법 grep 검증: 누락 using, 접근 불가 멤버, 튜플 요소 수
- 잠금/플래그 페어링 grep 검증 필수
- push / merge / worktree remove / --amend / --no-verify / force push 금지
- 200~400자 보고 형식: 수정 요지 / 트레이스 / 잠금-해제 매핑 / 커밋 SHA

---

## 7. 오픈 이슈 (작업 중 확정 필요)

- AimIndicatorView 의 시각 에셋: 라인렌더러 / 스프라이트 / Mesh 중 어느 방식? (Rapier-Private 에셋 의존) → **LineRenderer 기본으로 구현됨, 스프라이트 필드 SerializeField 노출 (후속 교체 가능)**
- 방패 휘두르기 / 대지 분쇄 이펙트 에셋도 Phase 26-D 에서 플레이스홀더 → 후속 폴리시로 교체
- 지뢰/화살 프리팹 레이어: Projectile 레이어 재사용 가능한지 26-C 시작 시 확인 → **RangerPlayer.prefab `_arrowPrefab` / `_minePrefab` null 상태. Unity 에서 프리팹 생성 후 할당 필요**

---

## 8. 완료 후 잔여 과제 (세션 15 기준)

### Unity 인스펙터 수동 작업 (에이전트 수행 불가)

1. **WarriorStatData.illustSprite / RangerStatData.illustSprite** 할당 (Rapier-Private 에셋)
2. **RangerPlayer.prefab `_arrowPrefab` / `_minePrefab`** 프리팹 생성 후 드래그 할당
3. **WarriorPlayer / RangerPlayer 캐릭터 고유 시각 에셋** (현재 Rapier 기본값 복사) 교체
4. **LobbyHudSetup 에디터 메뉴 재실행** — 기존 씬 HUD 의 `_warriorData` / `_rangerData` 슬롯 자동 할당

### 식별된 기존 버그 (이번 범위 외)

- **Assassin characterId 기본값 버그**: `AssassinPresenter.Init` 이 Base.Init 의 characterId 기본값 `"Rapier"` 를 사용 → 장비 저장/조회 시 Rapier 와 동일 키로 처리됨. Unity 테스트 단계에서 재현 후 수정 예정.

### 다음 세션 플레이 테스트 시나리오

- Warrior/Ranger 로비 선택 → 게임 씬 진입 → 해당 프리팹 스폰 확인
- Warrior: 차지 중 데미지 50% 감소 / Full 전 무반응 / Full 후 Swipe 패링 / Full 후 Release 대지 분쇄 / 가드 방향 일치 피격 → 패링 콜백
- Ranger: Tap 화살 / Hold 중 이동·회피·Tap 차단 (CanDodge=false) / Release 차지량 lerp / DodgeComplete 지뢰 (max 6) / 저스트 회피 강화 화살
- 기존 Rapier/Assassin 회귀 없음 확인 (TakeDamage 시그니처 확장 + CanDodge 기본 true)
