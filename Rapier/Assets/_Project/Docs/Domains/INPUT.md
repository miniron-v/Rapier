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

## 4. 저스트 회피 트리거

- 회피 대시 중(`JustDodgeAvailable == true`) 피격 시 `TriggerJustDodge()` 호출.
- 한 회피당 1회만 발동. `ConsumeJustDodge()`로 소비.
- `TriggerJustDodge()`가 유일한 발동 API.

---

## 5. 입력 차단 규칙

특정 액션 진행 중 일부 입력은 **즉시 무시**된다 (큐잉 없음).

| 진행 중 액션 | 차단되는 입력 |
|-------------|--------------|
| 회피 대시 | Tap |
| 저스트 회피 슬로우 | Tap |
| 고유 스킬 발동 ~ 복귀 | Tap |
| 차지 스킬 발동 | Tap |
| 회피 쿨다운 (2초) | Swipe |

- 차단은 GestureRecognizer 또는 CharacterPresenterBase 레벨에서 처리한다.
- 차단된 입력은 절대 큐잉되지 않으며, 상태 종료 후에도 자동 발동되지 않는다.
- 차지 스킬 발동 중 `ChargeReleased` 이벤트는 차단 대상이 아님 (이미 발동된 스킬의 종료 신호이므로).
- 캐릭터별 고유 메커니즘이 추가되어도 위 규칙은 일관되게 적용되어야 한다.

---

## 6. 주의사항

- 입력 유효 영역: **전체 화면** (제한 없음).
- `chargeRequiredTime`은 1.0f 이상 권장 — 짧으면 Hold 판정 직후 차지가 즉시 1로 보임.
