# 적 시스템 (Enemies)

---

## 1. 클래스 계층

```
EnemyPresenterBase (abstract)       ← 공통 (Chase/Windup/Hit/PostAttack + 시퀀서)
├── NormalEnemyPresenter            ← 일반 적 (WaveManager 오브젝트 풀)
└── BossPresenterBase (abstract)    ← 가변 페이즈 (1~3+), 페이즈별 시퀀스 교체
    ├── TitanBossPresenter          ← 구현+프리팹 완료
    ├── SpecterBossPresenter        ← 구현+프리팹 완료 (Phase 12-C 밸런싱)
    ├── PyromancerBossPresenter     ← 스크립트+SO 완료, 프리팹 제작 대상
    ├── BerserkerBossPresenter      ← 스크립트+SO 완료, 프리팹 제작 대상
    ├── StormcallerBossPresenter    ← 스크립트+SO 완료, 프리팹 제작 대상 (3페이즈)
    ├── GravekeeperBossPresenter    ← 스크립트+SO 완료, 프리팹 제작 대상
    └── TwinPhantomsBossPresenter   ← 스크립트+SO 완료, 프리팹 제작 대상 (1페이즈, 2체 동시)
```

- `EnemyModel`: HP, 상태 등 순수 데이터.
- `EnemyView`: 시각 표현, 애니메이션.
- `EnemyStatData`: SO. 일반 적 스탯 + attackSequence.
- `BossStatData`: SO. EnemyStatData 확장 + **phaseSequences 리스트** (단일/다중 페이즈 모두 지원).

> `BossStatData`는 기존 `phase2Sequence` 단일 필드 구조에서 `List<List<EnemyAttackAction>> phaseSequences` 형태로 확장되어야 한다 (1~N페이즈 지원). 페이즈 전환 임계 HP는 보스마다 다를 수 있으므로 별도 임계 리스트도 필요.

---

## 2. 상태 머신

```
Chase → Windup → Hit → PostAttack → Chase (반복)
```

- `EnemyAttackSequencer`: attackSequence 리스트를 순회하며 현재 공격 결정.
- `BossPresenterBase`: 페이즈별 임계 HP 도달 시 다음 시퀀스로 시퀀서 자동 교체. 보스마다 페이즈 수가 다름 (1/2/3+).

---

## 3. 공격 시스템

공격 상세는 `COMBAT.md` 참조. 여기서는 적 고유 사항만 기술.

- 모든 공격은 `EnemyAttackAction` 파생 클래스로 정의.
- `[SerializeReference]` 리스트로 SO에 직렬화.
- 보스 Presenter는 공격 로직을 갖지 않음 — AttackAction SO에 완전 위임.

---

## 4. 보스 라인업 (7종)

| # | 이름 | 페이즈 | 사용 AttackAction | 구현 상태 |
|---|------|--------|------------------|----------|
| 1 | Titan | 2 | Melee, Charge | 완료 |
| 2 | Specter | 2 | Teleport, Melee | 완료 |
| 3 | Pyromancer | 2 | Projectile, GroundHazard, MultiDirectional | 스크립트+SO 완료, 프리팹 제작 대상 |
| 4 | Berserker | 2 | Melee×3~4 콤보, Charge | 스크립트+SO 완료, 프리팹 제작 대상 |
| 5 | Stormcaller | 3 | MultiDirectional, Projectile (유도) | 스크립트+SO 완료, 프리팹 제작 대상 |
| 6 | Gravekeeper | 2 | Summon, Melee | 스크립트+SO 완료, 프리팹 제작 대상 |
| 7 | Twin Phantoms | 1 | Melee × 2 (본체+분신 협공) | 스크립트+SO 완료, 프리팹 제작 대상 |

### BossRushDemo 시퀀스 (Phase 12-C 확정)

- 총 7종 순차 플레이: **Titan → Specter → Berserker → Gravekeeper → Pyromancer → Stormcaller → TwinPhantoms**
- 앞 2종(Titan/Specter)은 기존 순서 유지, 이후 5종은 알파벳 순.
- `BossRushManager._bossPrefabs` / `_bossStatDatas`에 위 순서대로 7개 주입.

### 신규 5종 보스 프리팹 제작 사양 (Phase 12-C)

- 기준: `Titan_Boss.prefab`의 컴포넌트/계층 구조를 그대로 미러링.
- 스프라이트: 임시로 `Titan_Boss`와 동일 스프라이트 재사용 (아트 교체는 추후 Phase).
- 각 프리팹 구성: `SpriteRenderer` + `Rigidbody2D` + `Collider2D` + 해당 `*BossPresenter` + 해당 `*StatData` 참조 + 필요한 `*AttackAction` 컴포넌트 (프리팹에 부착해야 하는 경우 각 `*BossPresenter.cs` 요구 사항 따름).
- 저장 위치: `Assets/_Project/20_Prefabs/{BossName}_Boss.prefab`.

### 패턴 예시

#### Titan
| 페이즈 | 시퀀스 |
|--------|--------|
| 1 | Melee → Melee → Melee → 반복 |
| 2 | Melee → Melee → Melee → Charge → 반복 |

#### Specter
| 페이즈 | 시퀀스 |
|--------|--------|
| 1 | Melee → 반복 |
| 2 | Teleport → Melee → 반복 |

나머지 보스의 시퀀스는 구현 시점에 보스별 작업 안에서 확정한다.

### 보스 사용 규칙

- 한 스테이지 내에서 같은 보스는 재출현 금지.
- 같은 보스를 다른 스테이지에서 강화 버전으로 재활용 가능.
- 강화 방식 (HP/공격력 스케일링) 은 `Rapier_Prototype_DesignDoc.md §5-2` 참조.

---

## 5. 일반 적

- 분산 접근 AI (뭉치기 방지).
- 공격 주기 1.5초, Windup 0.5초.
- WaveManager가 오브젝트 풀로 관리.

---

## 6. 에디터 유틸

- `TitanDataSetup.cs`: 메뉴 `Rapier/Dev/Setup Titan Attack Sequence`
- `EnemyDataSetup.cs`: 메뉴 `Rapier/Dev/Setup Normal Enemy Sequence`, `Setup Specter Sequence`
- `[SerializeReference]` 리스트 갱신 순서: null 초기화 → SetDirty → SaveAssets → ImportAsset → 재할당.

---

## 7. 미해결 이슈

- **ISSUE-01**: `[SerializeReference]` 인스펙터 NullReferenceException — 런타임 무관. 인스펙터 닫았다 열면 해소.
- **ISSUE-02**: EnemyStatData CustomEditor 미구현 — 타입 선택 드롭다운 없음. 에디터 스크립트로 초기값 주입 중.
