# 입력 시스템 (Input)

---

## 1. 아키텍처

```
New Input System → GestureRecognizer → InputState Enum → C# event → CharacterPresenter
```

- 네임스페이스: `Game.Input`
- InputActions 에셋에서 Mobile(Touch) + PC(Mouse) 바인딩 모두 등록.
- 로직 코드에서 플랫폼 분기(`#if MOBILE` 등) 금지.

---

## 2. 제스처 판별 기준

| 제스처 | 조건 |
|--------|------|
| Tap | 이동 거리 < 20px, 지속 < 0.2초 |
| Swipe | 이동 거리 ≥ 60px, 지속 < 0.25초 |
| Hold | 이동 없음, 지속 ≥ 0.3초 |
| Drag | 이동 거리 ≥ 20px, 지속 ≥ 0.25초 |

---

## 3. 입력 상태 매핑

| 제스처 | InputState | 캐릭터 동작 |
|--------|-----------|------------|
| Drag | Move | 이동 (가상 조이스틱) |
| Tap | Attack | 전방 광역 공격 |
| Swipe | Dodge | 방향 회피 대시 (무적, 쿨다운 2초) |
| Hold → Release | Charge → Skill | 차지 게이지 충전 → 스킬 발동 |

---

## 3-1. Hold 중 확장 제스처 (캐릭터 옵션)

Hold 가 성립된 시점(=차지 시작, `_gestureCommitted && CurrentState == Hold`)부터, 일반 Hold/Swipe 배타 규칙이 풀리고 **손가락을 떼지 않은 채로 이동** 을 허용한다. 이 모드에서 발행되는 이벤트 3종은 `CharacterPresenterBase`가 구독하여 virtual 훅(`OnHoldSwipe` / `OnHoldRelease` / `OnHoldDragUpdate`)으로 전달한다. 자식은 훅만 override하면 되며 직접 구독/해제할 필요가 없다.

| 이벤트 | 발행 시점 | 페이로드 |
|---|---|---|
| `OnHoldDragUpdate(Vector2 fromStart)` | Hold 성립 후 매 프레임, 시작점 대비 손가락 변위 벡터 | 스크린 좌표 벡터 (정규화 안 된 raw) |
| `OnHoldSwipe(Vector2 direction)` | Hold 중 손가락이 **SWIPE_MIN_DISTANCE 이상** 이동 + 해당 이동이 **SWIPE_MAX_DURATION 이내** 에 완료될 때 단발 발행. 이후 터치는 종료 처리, 같은 터치의 Release 는 무시 | 스와이프 방향 (정규화) |
| `OnHoldRelease(Vector2 fromStart)` | Hold 중 손가락을 뗄 때 발행 (`OnHoldSwipe` 가 이미 발행된 터치에서는 발행 안 됨) | 시작점 대비 현재 손가락 변위 |

> 차지 풀(chargedFull) 여부는 각 자식 Presenter 가 자신의 로컬 플래그(`_isChargedFull` 등)로 판단한다. 입력 이벤트 페이로드에 전투 상태를 실어 보내지 않는다.

### 캐릭터별 해석

| 캐릭터 | OnHoldDragUpdate | OnHoldSwipe | OnHoldRelease |
|---|---|---|---|
| Rapier | 무시 | 무시 | 기존 Release 와 동일 (fromStart 무시) |
| Assassin | 무시 | 무시 | 기존 Release 와 동일 |
| Warrior | **무시** (차지 중 드래그 무의미) | 차지 Full 상태에서만 수신 → 방패 휘두르기 (방향=Swipe 방향) | 로컬 `_isChargedFull` 이 true 이면 대지 분쇄 |
| Ranger | **매 프레임 조준 방향 갱신** (fromStart 사용) | 무시 | **차지량 + 현재 조준 방향으로 발사**. fromStart=0 이면 기본 전방 |

### 불변식

- `OnHoldSwipe` 와 `OnHoldRelease` 는 **같은 터치에서 동시에 발행되지 않는다** (Swipe 발행 시 터치 종료 처리, Release 차단).
- `OnHoldDragUpdate` 는 `OnHoldSwipe` 발행 후엔 더 이상 발행되지 않는다.
- 기존 `OnHold(float duration)` 는 Hold 중 매 프레임 계속 발행된다 (차지 게이지 UI 갱신용).
- `OnMoveDirection` 은 Drag 상태에서만 발행 — Hold 중 손가락 이동은 Drag 로 전환되지 않는다.

---

## 4. 저스트 회피 트리거

저스트 회피는 **전투 도메인 사건**이므로 `GestureRecognizer` 가 아닌 `CharacterPresenterBase` 가 소유한다.

- 자식 Presenter 가 `EnableJustDodge()` 를 호출해 발동 가능 상태(`JustDodgeAvailable = true`)로 진입. **호출 시점은 캐릭터별 기획에 따른다** (예: OnSwipe, 방향성 방어 성공 콜백 등).
- 발동 가능 상태에서 피격이 발생하면 `ProcessTakeDamage` 가 `JustDodgeAvailable` 을 소비하고 `TriggerJustDodge(direction)` 를 호출해 슬로우 진입 경로를 실행한다.
- 자식이 코드에서 직접 슬로우를 트리거하려면 `TriggerJustDodge(Vector2)` (Base protected 메서드) 를 사용한다 (Warrior 패링 콜백 등).
- 한 트리거당 1회만 발동. `ConsumeJustDodge()` 로 소비, `OnDodgeDashComplete` 에서도 만료.
- `GestureRecognizer` 는 JustDodge 를 판단하거나 발행하지 않는다.

---

## 5. 입력 차단 규칙

특정 액션 진행 중 일부 입력은 **즉시 무시**되거나 **별도 분기**로 처리된다 (큐잉 없음).

| 진행 중 액션 | Tap 처리 |
|-------------|---------|
| 회피 대시 중 | **차단** |
| 저스트 회피 슬로우 중 | **`OnJustDodgeTap()` 분기** — 캐릭터 고유 스킬 발동 |
| 고유 스킬 발동 ~ 복귀 | **차단** |
| 차지 스킬 발동 중 | **차단** |
| 공격 쿨다운 중 (`_isAttacking`) | **차단** |
| 평상시 | **`OnNormalAttack()` 분기** — 일반 공격 |

| 진행 중 액션 | Swipe 처리 |
|-------------|-----------|
| 회피 쿨다운 (2초) | **차단** |
| Ranger 차지 경직 중 | **차단** (`CanDodge = false`) |

| 캐릭터 고유 제한 |
|---------------|
| Ranger Hold 성립 이후: Tap, Swipe, 이동 전부 차단 (본인 경직) |
| Warrior Hold Full 전: Release/Swipe 무효 (차지만 유지) |

### HandleTap 분기 흐름

```
HandleTap
  ├── _isJustDodgeSlowActive(슬로우 Hold 구간) → OnJustDodgeTap()
  ├── IsTapBlocked(회피대시/고유스킬/차지스킬/슬로우 코루틴 진행 중) → return
  └── !_isAttacking && CanAttack
        ├── AttackRoutine() 시작
        └── OnNormalAttack()
```

> `_slowCoroutine != null` 이 `IsTapBlocked` 에 포함되므로 슬로우 Exit 구간(Time.timeScale 복귀 중)에도 Tap 이 차단된다.

- 차단은 `CharacterPresenterBase` 레벨에서 처리한다.
- 차단된 입력은 절대 큐잉되지 않으며, 상태 종료 후에도 자동 발동되지 않는다.
- 캐릭터별 고유 메커니즘이 추가되어도 위 규칙은 일관되게 적용되어야 한다.

---

## 6. 주의사항

- 입력 유효 영역: **전체 화면** (제한 없음).
- `chargeRequiredTime`은 1.0f 이상 권장 — 짧으면 Hold 판정 직후 차지가 즉시 1로 보임.
