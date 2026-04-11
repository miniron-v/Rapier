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
| 쿨타임 감소 | CDR | % | ✓ | - |
| 스킬 데미지 증가 | SkillDamage | % | ✓ | - |

> 추후 능력치 추가 가능. enum 또는 string key 기반 확장 구조를 권장.

---

## 3. 계산식

```
최종 능력치 = (기본값 + MetaStat 깡합) × (1 + MetaStat % 합) × (1 + RunStat % 합) + RunStat 깡합
```

- 기본값: `CharacterStatData` (HP 500, ATK 50, MS 5)
- 깡 부스트와 % 부스트를 분리
- RunStat 깡합은 마지막에 더한다 (현재 RunStat은 % 위주이므로 일반적으로 0)

### 예시

```
기본 HP 500
+ 장비 깡 HP 200       (MetaStat 깡)
+ 장비 % HP +20%       (MetaStat %)
+ 런 % HP +25%         (RunStat % - 1회 선택)
+ 런 % HP +25%         (RunStat % - 누적)
= (500 + 200) × (1 + 0.20) × (1 + 0.25 + 0.25) + 0
= 700 × 1.20 × 1.50
= 1260
```

---

## 4. 적용 위치

- **MetaStat 구성**: `EquipmentMetaStatProvider` (`IMetaStatProvider` 구현) — `EquipmentManager` 의 장착 상태를 읽어 `MetaStatContainer` 를 빌드. 자세한 파이프라인은 `EQUIPMENT.md §4` 참조.
- **주입 지점**: `CharacterPresenterBase.Init(statData, view)` — 씬 진입 시 1회. `ServiceLocator.Get<EquipmentManager>()` 로 현재 장착 상태 조회 후 `MetaStatContainer` 를 `CharacterModel` 생성에 주입.
- **RunStat 적용 위치**: `RunStatContainer` — 인터미션 방에서 선택 시 누적. `ProgressionManager` 가 컨테이너 보유.
- **최종 스탯 계산**: `RunStatContainer.CalculateFinalHp(base, meta, run)` 와 같이 MetaStatContainer 와 RunStatContainer 가 **합산 지점에서만 만난다**. 두 컨테이너를 섞지 않는다.
- **호출 시점**:
  - 인게임 씬 진입 시: MetaStat 1회 계산 → CharacterModel 주입
  - RunStat 추가 시 (인터미션 방 선택): 매번 재계산
  - 외부에서는 항상 캐싱된 최종값을 읽는다

- **소멸**:
  - 스테이지 클리어/이탈 → `RunStatContainer.Clear()`
  - 다음 게임 시작 시 RunStat은 빈 상태에서 시작

---

## 5. 구현 시 주의사항

- **MetaStat 변경 → JSON 저장 필수.** 인메모리만 유지하면 종료 시 소실.
- **RunStat 변경 → 저장 금지.** 메모리에서만 관리.
- 최종 능력치는 `[NonSerialized]` 캐싱 필드에 저장. SO에 직접 쓰지 않는다.
- 능력치 변경 이벤트(`OnStatChanged`)를 발행하여 HUD 등이 구독하도록 한다.
