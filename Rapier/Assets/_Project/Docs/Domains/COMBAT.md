# 전투 시스템 (Combat)

---

## 1. 데미지 인터페이스

- `IDamageable`: 피격 대상이 구현. `TakeDamage(float damage)` 등.
- 위치: `Scripts/Combat/`
- 네임스페이스: `Game.Combat`

---

## 2. AttackAction 패턴

모든 적의 공격은 `EnemyAttackAction` 파생 클래스로 정의한다.
`EnemyStatData.attackSequence` (`[SerializeReference] List<EnemyAttackAction>`)에 직렬화.

### 공격 흐름

```
EnterWindupPhase()
  → action.PrepareWindup(ctx)   // 가변 범위 확정
  → AttackIndicator.Play()      // 인디케이터 표시
EnterHitPhase()
  → action.Execute(ctx, cb)     // 실제 공격 판정. 완료 시 cb() 호출
  → EnterPostAttackPhase()
```

### 파생 클래스 (현재)

| 클래스 | 역할 |
|--------|------|
| `MeleeAttackAction` | Sector/Rectangle 모양 히트 판정. Execute 시점 실시간 위치 기준. |
| `AoeAttackAction` | 범위 내 전체 히트. |
| `ChargeAttackAction` | PrepareWindup에서 RaycastToWall로 wallDist 확정. SO의 chargeMaxDistance는 상한값. |
| `TeleportAttackAction` | 히트 판정 없음. 페이드아웃 → 순간이동 → 페이드인. |

### 파생 클래스 (계획)

다음 AttackAction은 **사용하는 보스를 구현할 때 함께 추가**한다. 미리 만들지 않는다.

| 클래스 | 역할 | 사용 보스 |
|--------|------|----------|
| `ProjectileAttackAction` | 투사체 발사 (직선/곡선/유도). 투사체는 별도 풀로 관리. | Pyromancer, Stormcaller |
| `GroundHazardAttackAction` | 지면에 일정 시간 데미지존 생성. 보스 본체와 분리된 수명. | Pyromancer |
| `MultiDirectionalAttackAction` | 여러 방향으로 동시 공격. 회피 방향 강제. 기존 angleOffset 다중 활용 가능. | Pyromancer, Stormcaller |
| `SummonAttackAction` | 미니언 소환. 소환된 적은 일반 적 풀과 분리되거나 통합 가능. | Gravekeeper |

신규 AttackAction 추가 시 `EnemyAttackAction` 추상 클래스의 계약(`PrepareWindup` / `Execute`)을 그대로 따른다.

### 핵심 원칙

- 인디케이터 범위 = 히트 판정 범위 (동일 데이터 사용).
- 방향 계산: **x축 기준** (Atan2 / Cos·Sin 순서) 통일.
- 인디케이터 루트에 `lossyScale` 역수 적용 → 부모 스케일 상속 취소.
- SO 가변 범위는 `PrepareWindup`에서 계산 후 `[NonSerialized]` 필드에 캐싱.

---

## 3. 공격 인디케이터

- `AttackIndicatorEntry`: shape(Sector/Rectangle) + angleOffset + sectorData/rectData
- `angleOffset`: 플레이어 방향 기준 회전 오프셋 (도). 여러 방향 동시 표시 가능.
- `lockIndicatorDirection`: true 시 Windup 시작 방향 고정.
- 스캔라인이 경계에 닿는 순간 = 공격 발동.

---

## 4. 플레이어 공격

- Tap: 전방 사각형 범위 광역 공격. 즉시 히트 판정. 인디케이터 0.4초 표시.
- 차지 스킬: 캐릭터별 고유 동작 (CHARACTERS.md 참조).
