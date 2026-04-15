# 캐릭터 시스템 (Characters)

---

## 1. 클래스 계층

```
CharacterPresenterBase (abstract)   ← 공통 로직 (이동, 회피, 차지, Hold 확장 이벤트, TakeDamage)
├── MeleePresenterBase (abstract)   ← 근접 공통: 박스 히트 + 인디케이터 + Gizmo
│   ├── RapierPresenter             ← 표식 시스템 + 대시 스킬
│   ├── AssassinPresenter           ← 잔상 스태킹 + 360도 광역
│   └── WarriorPresenter            ← 방패 방어 + 패링
└── RangerPresenter                 ← 원거리 사격 + 지뢰 + 차지 조준
```

> **현재 구현 상태 (2026-04-15): 4종 전부 구현 완료 + GestureRecognizer 순수 입력 분리 리팩터링 완료. Unity 플레이 테스트 완료.**
> 신규 캐릭터 추가는 OCP를 만족해야 하며, 기존 코드 수정 없이 확장 가능해야 한다.

- `CharacterModel`: 순수 데이터 (HP, 상태 등). MonoBehaviour 아님.
- `CharacterView`: 시각 표현만. MonoBehaviour.
- `CharacterStatData`: SO. 캐릭터 공통 스탯.
- 각 캐릭터별 SO (예: `RapierStatData`)는 고유 수치 추가.

### MeleePresenterBase

`CharacterPresenterBase`를 상속하는 근접 캐릭터 전용 중간 클래스.

- `OnNormalAttack` 구현 → 박스 히트 + 공격 범위 인디케이터 (0.4초 표시)
- `PerformMeleeAttack()` protected 메서드 제공 → `OnPerformAttack` 훅 후 박스 히트 실행
- 공격 범위 Gizmo (에디터 전용)
- Rapier, Warrior, Assassin 이 상속

---

## 2. 공통 입력 매핑

| 입력 | 상태 | 동작 |
|------|------|------|
| Drag | Move | 이동 |
| Tap | Attack | 전방 광역 공격 |
| Swipe | Dodge | 방향 회피 (대시 전 구간 무적, 쿨다운 2초) |
| Hold → Release | Charge → Skill | 차지 후 스킬 발동 |

---

## 3. 캐릭터별 고유 메커니즘

### Warrior — 인내와 패링

**핵심 철학**: 일반 저스트 회피를 쓰지 않는 유일한 캐릭터. 모든 고유 스킬 트리거가 "차지 Full + 방패 휘두르기 → 패링" 경로로 집중된다.

#### 메커니즘

- **Hold 시작**: 차지 게이지 충전 시작. 충전 중 피격 데미지 **50% 감소**.
- **차지 게이지 Full 전**: Release/Swipe/Drag 등 모든 Hold 후 입력 **무시** (차지만 유지).
- **차지 중 Hold 취소 (손가락 뗌)**: 게이지·데미지 감소 즉시 소멸. 쿨다운 없음 (차지 중엔 공격 불가이므로 악용 불가).
- **차지 Full 후 Release (`OnHoldRelease`)**: **대지 분쇄** — 전방 광역 `ATK × 350% × SkillDmgMult`. 차지 풀 여부는 로컬 `_isChargedFull` 플래그로 판단.
- **차지 Full 후 `OnHoldSwipe` (Swipe 임계 통과)**: **방패 휘두르기** — Swipe 방향으로 근접 히트박스, `ATK × 150%` 데미지 + 넉백. 휘두르는 동안(=`dodgeDashDuration` 재활용) 해당 방향 ±60° 각도에서 오는 공격에 대해 무적.
- **방패 휘두르기 무적 중 해당 각도 공격 피격 → 패링 성립**: 슬로우 모션 + 즉시 대지 분쇄 발동 (Rapier 의 저스트 회피 후 고유 스킬 포지션).
- **방패 휘두르기 중 비방어 각 피격**: 정상 피격 (무적 아님).
- **일반 저스트 회피 (회피 대시 중 피격)**: **미발동**. Warrior는 `OnSwipe`에서 `EnableJustDodge()`를 호출하지 않으므로 `JustDodgeAvailable`이 항상 false — `ProcessTakeDamage`의 저스트 회피 경로에 진입하지 않는다.

#### 구현 구조

| 객체 | 책임 |
|------|------|
| `WarriorPresenter` | `CharacterPresenterBase` 상속. `OnHoldSwipe`/`OnHoldRelease` override, 방패 휘두르기 루틴 관리, 방향성 무적 창 오픈 |
| `WarriorStatData` (SO) | `CharacterStatData` 상속. 아래 고유 필드 |
| 방향성 무적 | `CharacterModel` 에 `SetDirectionalGuard(Vector2 normal, float halfAngleDeg)` / `ClearDirectionalGuard()` 추가. `TakeDamage` 경로에서 knockbackDir 비교 후 `dot(attackDir, -shieldNormal) ≥ cos(halfAngle)` 면 무효 + 패링 콜백 |

#### WarriorStatData 고유 필드

| 필드 | 기본값 | 설명 |
|---|---|---|
| `chargeDamagePercent` | 350 | 대지 분쇄 ATK % |
| `shieldSwingDamagePercent` | 150 | 방패 휘두르기 ATK % |
| `shieldSwingKnockback` | 2.0f | 방패 히트 넉백 거리 (unit) |
| `damageReductionPercent` | 50 | Hold 차지 중 피격 데미지 감소율 |
| `shieldGuardHalfAngle` | 60 | 방어 반각 (도). 전체 방어 범위는 120° |
| `shieldSwingDuration` | `dodgeDashDuration` 재활용 | 별도 필드 없음. 방패 휘두르기 지속 = 회피 대시 지속과 동일 |

### Ranger — 거리 조절과 화망

**핵심 철학**: 자유로운 조준. 모든 투사체/지뢰가 공간 제어 도구이며, 차지 스킬은 LoL 바루스 Q 스타일의 방향 드래그 조준.

#### 메커니즘

- **Tap (원거리 사격)**: 근접 광역 공격을 대체. 전방으로 투사체 발사. 발사 쿨다운은 기존 Tap 공격 쿨다운과 동일. `ATK × 100%`, 속도 25 unit/s, 사거리 8 unit.
- **Swipe (회피 대시 + 지뢰 투척)**: 회피 **시작** 순간 지뢰 3개(0°/+45°/−45° 방향)를 회피 반대 방향으로 투척. 모든 회피마다 발동되며 별도 쿨다운 없음.
- **저스트 회피 후 고유 스킬 (Hold → Release)**: 강화 관통 화살 — 일반 Tap 투사체 대비 **가로 너비 3배**, 사거리 10 unit, `ATK × 300%`, 관통 무제한 (감쇠 없음), 폭발 아님.
- **차지 스킬 (Hold → Drag → Release, 바루스 Q 스타일)**:
  - Hold 성립 순간부터 **본인 경직** (이동/회피/Tap 불가).
  - Hold 성립 이후 손가락 위치를 매 프레임 추적하여, 캐릭터 본체 → "Hold 시작점 → 현재 손가락 위치" 방향을 조준 방향으로 사용 (`OnHoldDragUpdate` 의 `fromStart` 벡터).
  - 차지량에 따라 **사거리/데미지/너비 모두 선형 보간**:
    - 차지 0 (Hold 성립 직후 즉시 뗌): 사거리 4 unit, `ATK × 100%`, 너비 = Tap 투사체와 동일
    - 차지 Full: 사거리 14 unit, `ATK × 300%`, 너비 Tap × 3 (= 저스트 회피 화살과 동일 스펙)
  - 조준 방향으로 사거리 표시기 렌더링 (차지량 비례 길이).
  - `OnHoldRelease` 발행 시 그 순간 조준 방향·차지량으로 발사. Hold 성립 직후 떼도 발사 (최소 차지 시간 없음). `fromStart == Vector2.zero` 이면 기본 전방 방향.
  - 관통 무제한, 감쇠 없음.

#### 구현 구조

| 객체 | 책임 |
|---|---|
| `RangerPresenter` | `CharacterPresenterBase` 상속. Tap/저스트 스킬/차지 발사 전부 override. `OnHoldDragUpdate` 구독해 조준 방향 갱신. 차지 중 경직 플래그 유지 |
| `RangerStatData` (SO) | `CharacterStatData` 상속. 아래 고유 필드 |
| `RangerArrow` (MonoBehaviour) | 투사체 — 직선 이동 + 관통 적 리스트 (중복 타격 방지) + 사거리 도달/경계 이탈 시 despawn |
| `RangerMine` (MonoBehaviour) | 지뢰 — 5초 수명, 적 접촉 시 즉발 (반경 1.5 unit 광역). 최대 동시 6개, Assassin 잔상 방식(초과 시 최고참 제거) |
| `AimIndicatorView` | 차지 중 사거리 표시기 (캐릭터→조준 방향, 차지량 비례 길이) |

#### RangerStatData 고유 필드

| 필드 | 기본값 | 설명 |
|---|---|---|
| `tapDamagePercent` | 100 | Tap 사격 ATK % |
| `tapProjectileSpeed` | 25.0f | Tap 투사체 속도 (unit/s) |
| `tapProjectileRange` | 8.0f | Tap 투사체 최대 사거리 |
| `tapProjectileWidth` | 0.4f | Tap 투사체 히트박스 폭 (참고 기본값, 에셋 맞춰 조정) |
| `justDodgeArrowDamagePercent` | 300 | 저스트 회피 후 강화 화살 ATK % |
| `justDodgeArrowRange` | 10.0f | 강화 화살 사거리 |
| `justDodgeArrowWidthMult` | 3.0f | 강화 화살 너비 = Tap 너비 × 배수 |
| `chargeArrowMinDamagePercent` | 100 | 차지 0 시점 ATK % |
| `chargeArrowMaxDamagePercent` | 300 | 차지 Full 시점 ATK % |
| `chargeArrowMinRange` | 4.0f | 차지 0 사거리 |
| `chargeArrowMaxRange` | 14.0f | 차지 Full 사거리 |
| `chargeArrowMaxWidthMult` | 3.0f | 차지 Full 너비 = Tap 너비 × 배수. 차지 0 시점 배수는 1.0f |
| `minePlaceOnDodge` | true | 회피 시 지뢰 자동 설치 |
| `mineDamagePercent` | 80 | 지뢰 폭발 ATK % |
| `mineExplosionRadius` | 1.5f | 지뢰 폭발 반경 |
| `mineLifetime` | 5.0f | 지뢰 수명 (초) |
| `maxActiveMines` | 6 | 동시 존재 최대 수 (초과 시 최고참 제거) |

### Rapier — 빌드업과 수확

### Assassin — 잔상 스태킹과 난무

- 저스트 회피 시: 회피 전 위치에 잔상(Phantom) 생성. 최대 3개 동시 유지. 초과 시 가장 오래된 것 제거.
- 잔상 지속: `phantomDuration`초 후 자동 페이드 아웃 소멸. 잔상 룬 장착 시 지속 +30%.
- 동참 공격: 본체 Tap 공격 시 활성 잔상들이 각자 위치에서 동일 공격 실행. 데미지 = `ATK × (phantomDamagePercent/100)`.
- 차지 스킬: 360도 원형 광역 베기 (잔상 수와 독립, 별도 메커니즘).
- 사망/씬 전환 시 모든 잔상 즉시 Destroy.

#### 구현 구조

| 객체 | 책임 |
|------|------|
| `AssassinPresenter` | `CharacterPresenterBase` 상속. 잔상 리스트 관리, OnJustDodge/OnSkillRelease override |
| `AssassinStatData` (SO) | `CharacterStatData` 상속. `phantomDuration`, `phantomDamagePercent`, `maxPhantoms`(=3) |
| `PhantomController` (MonoBehaviour) | 개별 잔상 오브젝트 — 수명 타이머, 공격 동참, 페이드 아웃 시각 효과 |

#### AssassinStatData 고유 필드

| 필드 | 기본값 | 설명 |
|------|--------|------|
| `phantomDuration` | 5.0f | 잔상 지속 시간 (초) |
| `phantomDamagePercent` | 50 | 잔상 공격 데미지 (ATK 대비 %) |
| `maxPhantoms` | 3 | 동시 최대 잔상 수 |

### Rapier — 빌드업과 수확

- 저스트 회피 후 슬로우 중 Hold → Release 시 고유 스킬 발동: 가장 가까운 적에게 대시 → 표식 1중첩 부여 + `ATK × (markDamagePercent/100) × SkillDmgMult` 데미지 → 복귀. 표식 최대 5중첩. (`markDamagePercent` 기본 70 = 70%)
- 차지 스킬: 표식 보유 적에게 `ATK × (chargeSkillPercent/100) × stacks × SkillDmgMult` 데미지. 표식 소비. (`chargeSkillPercent` 기본 100 = 100%)
- 스킬 대시~복귀 구간 전체 무적. 스킬/회피 중 일반 공격 차단.

---

## 4. 저스트 회피 (공통)

- 발동: 회피 대시 중 적 공격 피격 시. 한 회피당 1회.
- 효과: 슬로우 모션 + 카메라 줌 + 무적 유지.
- **슬로우 중 Tap → `OnJustDodgeTap()` 훅 호출** → 캐릭터별 고유 스킬 발동.
- `CharacterPresenterBase.TriggerJustDodge(Vector2)`가 코드 기반 발동 API (protected). `EnableJustDodge()` / `ConsumeJustDodge()`도 Base 소유. GestureRecognizer는 JustDodge를 판단하거나 발행하지 않는다.

| 캐릭터 | 저스트 회피 발동 조건 | OnJustDodgeTap 결과 |
|--------|---------------------|-------------------|
| Rapier | 회피 대시 중 피격 | 표식 대시 스킬 |
| Assassin | 회피 대시 중 피격 | (없음, 슬로우 중 Tap 만료) |
| Warrior | **패링 성립** (방패 방향 피격) | 대지 분쇄 |
| Ranger | 회피 대시 중 피격 | 강화 화살 발사 |

- Warrior는 패링 성립 시 `HandleParry` → `TriggerJustDodge()` 를 호출하여 Base 슬로우모션 시스템으로 진입한다. 슬로우 중 Tap으로 대지 분쇄를 발동한다.

---

## 5. 구현 시 주의사항

- 근접 캐릭터 추가 시 `MeleePresenterBase`를 상속할 것. 원거리 캐릭터는 `CharacterPresenterBase` 직접 상속.
- 자식 고유 상태(`_isDashSkillActive` 등)는 자식 안에서만 처리. Base에 노출 금지.
- 속도 배율로 사용되는 AnimationCurve(`dodgeDashCurve` 등)의 끝값은 0.50f 이상 유지 — 0이면 while 루프 무한 반복 위험. 슬로우모션 커브(`holdCurve`)는 시간 기반이므로 0.10f 등 낮은 값 가능.
- Hold 확장 이벤트(`OnHoldSwipe` / `OnHoldRelease` / `OnHoldDragUpdate`)는 Base가 자동 구독/해제 관리. 자식은 virtual override만 구현.
- `TakeDamage`(IDamageable)는 `ProcessTakeDamage` 한 줄로 위임. 특수 처리가 필요하면 자식 로컬 플래그로 분기하거나 `EnableJustDodge()` / `TriggerJustDodge()` API를 활용한다.

---

## 6. 입력 차단 (공통 규칙)

회피 대시 중 / 고유 스킬 중 / 차지 스킬 중 Tap 입력은 **즉시 무시**된다 (큐잉 없음).
저스트 회피 슬로우 중 Tap은 차단이 아닌 **`OnJustDodgeTap()` 분기**로 처리된다.
회피 쿨다운 중 Swipe 입력은 무시된다.

자세한 차단 규칙과 책임 위치는 `INPUT.md §5` 참조.

캐릭터별 고유 메커니즘이 추가되어도 위 규칙은 일관되게 적용되어야 하며, 자식 클래스에서 우회하면 안 된다.

---

## 7. 능력치 적용

캐릭터의 최종 능력치는 **MetaStat (영구) + RunStat (일회성)** 의 합산으로 결정된다.
계산식과 분리 원칙은 `STATS.md` 참조.
