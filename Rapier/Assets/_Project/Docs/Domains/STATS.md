# 능력치 시스템 (Stats)

---

## 1. 핵심 원칙: Meta vs Run 분리

캐릭터 능력치는 **반드시 두 종류로 명확히 분리**한다. 코드 상으로도 다른 네임스페이스/클래스로 관리하여 혼동을 방지한다.

| 종류 | 정의 | 출처 | 저장 | 적용 시점 | 네임스페이스 |
|------|------|------|------|----------|--------------|
| **MetaStat** | 영구 능력치 | 캐릭터 레벨, 장비, 룬 | JSON 영구 저장 | 게임 시작 시 캐릭터 모델에 주입 | `Game.Data.MetaStats` |
| **RunStat** | 일회성 능력치 | 인터미션 방의 스탯 선택 | 메모리 only, 스테이지 종료/이탈 시 소멸 | 스테이지 진행 중 누적 적용 | `Game.Combat.RunStatModifier` |

> **혼동 금지**: MetaStat과 RunStat은 절대 동일 자료구조로 관리하지 않는다. 변환 함수도 두지 않는다. 두 종류가 합산되는 지점은 오직 `최종 능력치 계산` 한 곳.

---

## 2. 사용 능력치 (프로토타입)

| 능력치 | 약어 | 단위 | MetaStat | RunStat |
|--------|------|------|----------|---------|
| 최대 HP | HP | 정수 | ✓ (깡/%) | ✓ (%) |
| 공격력 | ATK | 정수 | ✓ (깡/%) | ✓ (%) |
| 이동속도 | MS | 실수 | ✓ (깡/%) | ✓ (%) |
| 회피 쿨다운 감소 | DodgeCDR | % | ✓ | ✓ |
| 차지 시간 단축 | ChargeTimeReduction | % | ✓ | ✓ |
| 무적 시간 증가 | InvincibilityBonus | % | ✓ | ✓ |
| 크리티컬 확률 | CritChance | % | ✓ | ✓ |
| 크리티컬 데미지 | CritDamage | % | ✓ | - |
| 스킬 데미지 증가 | SkillDamage | % | ✓ | - |

> **감소율 스탯(DodgeCDR / ChargeTimeReduction / InvincibilityBonus)** 은 §3 에서 별도 곱셈 공식으로 처리한다. 다른 스탯과 동일한 합산 공식을 쓰지 않는다.

> 추후 능력치 추가 가능. enum 또는 string key 기반 확장 구조를 권장.

---

## 3. 계산식

스탯은 **가산형(HP/ATK/MS/SkillDamage/Crit~)** 과 **감소율형(DodgeCDR/ChargeTimeReduction/InvincibilityBonus)** 로 나뉜다. 공식이 다르다.

### 3-1. 가산형 — HP / ATK / MS 등

```
최종 = (기본값 + MetaStat 깡합) × (1 + MetaStat % 합) × (1 + RunStat % 합) + RunStat 깡합
```

- 기본값: `CharacterStatData` (HP 500, ATK 50, MS 5)
- 깡 부스트와 % 부스트를 분리
- 동일 Tier(Meta / Run) 내 % 는 **합연산**, Tier 간은 **곱연산**

예시:
```
기본 HP 500 + 장비 깡 200 + 장비 % +20% + 런 % +25% + 런 % +25%
= (500 + 200) × 1.20 × 1.50 = 1260
```

### 3-2. 감소율형 — DodgeCDR / ChargeTimeReduction 등

```
최종 = 기본값 × Π_i(1 − metaP_i) × Π_j(1 − runP_j)
```

- **소스별 독립 곱연산**. 같은 Tier 내에서도 합산하지 않는다.
- 20% 감소를 3회 획득하면 `(1−0.2)³ = 0.512` — 합산(40%) 이 아니다.
- Meta 와 Run 은 별도 누적되지만 최종은 둘 다 곱한다.

예시 (차지 시간):
```
기본 차지 1.0s
+ 장비 ChargeTimeReduction 15%
+ 인터미션 ChargeTimeReduction 20% × 3회
= 1.0 × (1 − 0.15) × (1 − 0.20) × (1 − 0.20) × (1 − 0.20)
= 1.0 × 0.85 × 0.512
= 0.4352s
```

> 컨테이너는 이 누적을 `float _multiplier = 1f` 로 들고 있으며, 소스가 들어오면 `*= (1 − p)`, 제거되면 `/= (1 − p)`, 초기화는 `= 1f` 로 처리한다. 별도 % 합 필드를 두지 않는다.

---

## 4. 적용 위치

- **MetaStat 구성**: `EquipmentMetaStatProvider` (`IMetaStatProvider` 구현) — `EquipmentManager` 의 장착 상태를 읽어 `MetaStatContainer` 를 빌드. 자세한 파이프라인은 `EQUIPMENT.md §4` 참조.
- **RunStat 소유**: `StageManager._runStat` (메모리 only). `IntermissionManager` 가 참조를 공유해 `RunStatContainer.Apply()` 로 누적. 스테이지 클리어 / 로비 복귀 시 `Reset()`.
- **주입 지점**: `CharacterPresenterBase.Init(statData, view)` — 씬 진입 시 1회. `ServiceLocator` 에서 `EquipmentManager` / `StageManager` 조회 → `MetaStatContainer` + `RunStatContainer` 를 **둘 다** `CharacterModel` 생성에 주입. `StageManager` 미등록(로비 등) 이면 RunStat 없이 진행.
- **최종 스탯 계산**: `CharacterModel` 내부에서 §3 계산식을 적용하여 가산형은 `_finalMaxHp / _finalAttackPower / _finalMoveSpeed`, 감소형은 `_finalDodgeCooldown / _finalChargeRequiredTime`, 배수는 `_skillDamageMultiplier` 를 캐싱. MetaStat / RunStat 두 컨테이너는 이 계산 지점에서만 만난다 — 섞지 않는다.
- **갱신 트리거**: `CharacterPresenterBase.Init` 에서 `RunStatContainer.OnStatChanged` 를 구독 → 픽이 들어올 때마다 `CharacterModel.RecomputeFinalStats()` 호출. 구독은 `OnDisable` / `OnDestroy` 에서 **반드시 해제 쌍** 유지 (§5 참조). MetaStat 은 인게임 중 변경 없음 — 스냅샷으로 충분.
- **HP 처리 정책**: RunStat HP% 픽으로 `MaxHp` 가 증가하면 **증가분만큼 `CurrentHp` 를 Heal** 한다 (인터미션이 보상 겸 회복 의미를 갖도록). 감소 시에는 `CurrentHp` 를 새 `MaxHp` 로 Clamp.
- **스탯 소비 경로**: 모든 런타임 스탯 읽기는 Model 경유 — `Model.MaxHp / Model.AttackPower / Model.MoveSpeed / Model.DodgeCooldown / Model.ChargeRequiredTime / Model.SkillDamageMultiplier`. `CharacterStatData` (SO) 를 직접 읽으면 MetaStat / RunStat 가 누락된다. 스킬·차지 공격·회피 쿨 전 경로 통일.
- **소멸**: 스테이지 클리어/로비 복귀 시 `StageManager` 가 `RunStatContainer.Reset()` 호출 → 다음 런은 0 부터 시작.

---

## 5. 구현 시 주의사항

- **MetaStat 변경 → JSON 저장 필수.** 인메모리만 유지하면 종료 시 소실.
- **RunStat 변경 → 저장 금지.** 메모리에서만 관리.
- 최종 능력치는 `[NonSerialized]` 캐싱 필드에 저장. SO에 직접 쓰지 않는다.
- **감소율 스탯은 소스 독립 곱연산**. 합산 필드(`_percentDodgeCdr` 등)를 두지 말고 `_multiplier = 1f` 단일 값만 유지. 같은 스탯이라도 장비 / 룬 / 인터미션 픽은 각각 독립 소스로 취급 — 세 번의 20% 감소 픽은 `× 0.8³` 이지 `× 0.4` 가 아니다.
- **RunStat 구독 해제 쌍 필수**: `CharacterPresenterBase` 가 `OnStatChanged` 를 구독하면 `OnDisable` / `OnDestroy` / 씬 전환 모든 종료 경로에 해제가 있어야 한다. 누락 시 씬 재진입 후 중복 구독 → 이벤트 중복 발행.
- HUD 등은 `CharacterModel` 의 `OnHpChanged` 또는 `RunStatContainer.OnStatChanged` 를 직접 구독.
