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

Hold 가 성립된 시점(=차지 시작, `_gestureCommitted && CurrentState == Hold`)부터, 일반 Hold/Swipe 배타 규칙이 풀리고 **손가락을 떼지 않은 채로 이동** 을 허용한다. 이 모드에서 발행되는 이벤트 3종은 Ranger/Warrior 가 각자 구독해 해석한다.

| 이벤트 | 발행 시점 | 페이로드 |
|---|---|---|
| `OnHoldDragUpdate(Vector2 fromStart)` | Hold 성립 후 매 프레임, 시작점 대비 손가락 변위 벡터 | 스크린 좌표 벡터 (정규화 안 된 raw) |
| `OnHoldSwipe(Vector2 direction)` | Hold 중 손가락이 **SWIPE_MIN_DISTANCE 이상** 이동 + 해당 이동이 **SWIPE_MAX_DURATION 이내** 에 완료될 때 단발 발행. 이후 터치는 종료 처리, 같은 터치의 Release 는 무시 | 스와이프 방향 (정규화) |
| `OnHoldRelease(Vector2 fromStart, bool chargedFull)` | Hold 중 손가락을 뗀 순간 (`OnHoldSwipe` 가 이미 발행된 터치에서는 발행 안 됨) | 시작점 대비 현재 손가락 변위 + 풀차지 여부 |

### 캐릭터별 해석

| 캐릭터 | OnHoldDragUpdate | OnHoldSwipe | OnHoldRelease |
|---|---|---|---|
| Rapier | 무시 | 무시 | 기존 Release 와 동일 (fromStart 무시) |
| Assassin | 무시 | 무시 | 기존 Release 와 동일 |
| Warrior | **무시** (차지 중 드래그 무의미) | 차지 Full 상태에서만 수신 → 방패 휘두르기 (방향=Swipe 방향) | 기존 Release 와 동일. 차지 Full 이면 대지 분쇄 |
| Ranger | **매 프레임 조준 방향 갱신** (fromStart 사용) | 무시 (Ranger 는 Drag/Swipe 구분 없이 방향만 사용) | **차지량 + 현재 조준 방향으로 발사**. fromStart=0 이면 기본 전방 |

### 불변식

- `OnHoldSwipe` 와 `OnHoldRelease` 는 **같은 터치에서 동시에 발행되지 않는다** (Swipe 발행 시 터치 종료 처리, Release 차단).
- `OnHoldDragUpdate` 는 `OnHoldSwipe` 발행 후엔 더 이상 발행되지 않는다.
- 기존 `OnHold(float duration)` 는 Hold 중 매 프레임 계속 발행된다 (차지 게이지 UI 갱신용).
- `OnMoveDirection` 은 Drag 상태에서만 발행 — Hold 중 손가락 이동은 Drag 로 전환되지 않는다.

---

## 4. 저스트 회피 트리거

- 회피 대시 중(`CharacterPresenterBase.JustDodgeAvailable == true`) 피격 시 `GestureRecognizer.TriggerJustDodge(Vector2 direction)` 호출.
- 한 회피당 1회만 발동. `CharacterPresenterBase.ConsumeJustDodge()`로 소비.
- `TriggerJustDodge(Vector2)`가 유일한 발동 API.

---

## 5. 입력 차단 규칙

특정 액션 진행 중 일부 입력은 **즉시 무시**된다 (큐잉 없음).

| 진행 중 액션 | 차단되는 입력 |
|-------------|--------------|
| 회피 대시 | Tap |
| 저스트 회피 슬로우 | Tap |
| 고유 스킬 발동 ~ 복귀 | Tap |
| 차지 스킬 발동 | Tap |
| 공격 인디케이터 표시 중 (0.4초) | Tap |
| 회피 쿨다운 (2초) | Swipe |
| Ranger 차지 중 (Hold 성립 이후) | Tap, Swipe(일반), Drag (이동) — 본인 경직 |
| Warrior Hold 차지 중 (Full 전) | Release/Swipe 모두 무효 (아무 동작 없음, 차지만 유지) |

- 차단은 GestureRecognizer 또는 CharacterPresenterBase 레벨에서 처리한다. 공격 인디케이터 차단은 `_isAttacking` 플래그 경로로 별도 처리.
- 차단된 입력은 절대 큐잉되지 않으며, 상태 종료 후에도 자동 발동되지 않는다.
- 차지 스킬 발동 중 `ChargeReleased` 이벤트는 차단 대상이 아님 (이미 발동된 스킬의 종료 신호이므로).
- 캐릭터별 고유 메커니즘이 추가되어도 위 규칙은 일관되게 적용되어야 한다.

---

## 6. 주의사항

- 입력 유효 영역: **전체 화면** (제한 없음).
- `chargeRequiredTime`은 1.0f 이상 권장 — 짧으면 Hold 판정 직후 차지가 즉시 1로 보임.
