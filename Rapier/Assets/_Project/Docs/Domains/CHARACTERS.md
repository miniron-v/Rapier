# 캐릭터 시스템 (Characters)

---

## 1. 클래스 계층

```
CharacterPresenterBase (abstract)   ← 공통 로직 (이동, 공격, 회피, 차지)
└── RapierPresenter                 ← 표식 시스템 + 대시 스킬 (구현 완료)

[미구현 — 향후 추가]
├── WarriorPresenter                ← 방패 방어 + 패링
├── AssassinPresenter               ← 잔상 스태킹
└── RangerPresenter                 ← 원거리 사격 + 지뢰
```

> **현재 (2026-04-10) 구현된 캐릭터는 Rapier 단일.**
> Warrior/Assassin/Ranger 클래스는 존재하지 않으며, 캐릭터 선택 화면에서 "Coming Soon" 잠금으로 표현된다.
> 신규 캐릭터 추가는 OCP를 만족해야 하며, 기존 코드 수정 없이 확장 가능해야 한다.

- `CharacterModel`: 순수 데이터 (HP, 상태 등). MonoBehaviour 아님.
- `CharacterView`: 시각 표현만. MonoBehaviour.
- `CharacterStatData`: SO. 캐릭터 공통 스탯.
- 각 캐릭터별 SO (예: `RapierStatData`)는 고유 수치 추가.

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

- Hold 중: 방패 방어 (데미지 감소)
- Hold → Release: 대지 분쇄 (전방 광역)
- Hold → Swipe: 방패 밀쳐내기 (데미지 + 넉백). 피격 중이면 패링(저스트 회피) 발동
- 저스트 회피 후 고유 스킬: 즉시 대지 분쇄 (차지 없이)

### Assassin — 잔상 스태킹과 난무

- 저스트 회피 시: 회피 전 위치에 잔상 생성. 잔상 활성 중 본체 공격에 잔상 동참.
- 차지 스킬: 360도 원형 광역 베기

### Rapier — 빌드업과 수확

- 저스트 회피 후 슬로우 중 Hold → Release 시 고유 스킬 발동: 가장 가까운 적에게 대시 → 표식 1중첩 부여 + markDamage 기반 데미지(공격력 영향) → 복귀. 표식 최대 5중첩.
- 차지 스킬: 표식 보유 적에게 중첩 수 × (공격력 × chargeMarkMultiplier) 데미지. 표식 소비.
- 스킬 대시~복귀 구간 전체 무적. 스킬/회피 중 일반 공격 차단.

### Ranger — 거리 조절과 화망

- Tap: 원거리 사격으로 대체
- 회피: 대시 + 회피 지점에 지뢰 설치
- 저스트 회피 후 고유 스킬: 즉시 강화 폭발 화살 (차지 없이)
- 차지 스킬: 직선 관통 화살. 시전 중 경직 부여.

---

## 4. 저스트 회피 (공통)

- 발동: 회피 대시 중 적 공격 피격 시. 한 회피당 1회.
- 효과: 슬로우 모션 + 카메라 줌 + 무적 유지.
- 슬로우 중 Hold → 캐릭터 고유 스킬 즉시 발동.
- `GestureRecognizer.TriggerJustDodge(Vector2 direction)`가 유일한 발동 API. `JustDodgeAvailable` / `ConsumeJustDodge()`는 `CharacterPresenterBase` 소유.

---

## 5. 구현 시 주의사항

- 새 캐릭터 추가 시 `CharacterPresenterBase`를 상속하고, 기존 코드 수정 없이 확장할 것 (OCP).
- 자식 고유 상태(`_isDashSkillActive` 등)는 자식 안에서만 처리. Base에 노출 금지.
- 속도 배율로 사용되는 AnimationCurve(`dodgeDashCurve` 등)의 끝값은 0.50f 이상 유지 — 0이면 while 루프 무한 반복 위험. 슬로우모션 커브(`holdCurve`)는 시간 기반이므로 0.10f 등 낮은 값 가능.

---

## 6. 입력 차단 (공통 규칙)

회피 / 저스트 회피 / 고유 스킬 / 차지 스킬 진행 중 Tap 입력은 **즉시 무시**된다 (큐잉 없음).
회피 쿨다운 중 Swipe 입력도 마찬가지로 무시된다.

자세한 차단 규칙과 책임 위치는 `INPUT.md §5` 참조.

캐릭터별 고유 메커니즘이 추가되어도 위 차단 규칙은 일관되게 유지되어야 하며, 자식 클래스에서 우회하면 안 된다.

---

## 7. 능력치 적용

캐릭터의 최종 능력치는 **MetaStat (영구) + RunStat (일회성)** 의 합산으로 결정된다.
계산식과 분리 원칙은 `STATS.md` 참조.
