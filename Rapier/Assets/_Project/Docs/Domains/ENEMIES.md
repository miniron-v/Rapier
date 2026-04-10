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
- 저장 위치: `Assets/_Project/Prefabs/Boss/{BossName}_Boss.prefab`.

### Gravekeeper 전용 미니언 (Phase 12-C 확정)

- **프리팹**: `Assets/_Project/Prefabs/Enemies/GravekeeperMinion.prefab`
- **SO**: `Assets/_Project/ScriptableObjects/Enemies/GravekeeperMinionData.asset`
- **기반 구조**: `Enemy_Template.prefab`(기존 NormalEnemy) 미러링. 컴포넌트: `SpriteRenderer` + `Rigidbody2D` + `Collider2D` + `NormalEnemyPresenter`.
- **스프라이트**: 기존 NormalEnemy 스프라이트 재사용 (아트 교체는 추후).
- **AttackAction**: `MeleeAttackAction` 1종만. `attackSequence`에 `[SerializeReference]`로 직렬화.
- **임시 밸런스**:
  - HP: 30
  - ATK: 5
  - Speed: 3
  - 공격 주기/Windup: NormalEnemy 기본값(1.5s / 0.5s) 따름
- **연결**: `GravekeeperBossPresenter._minionPrefab`, `_minionData`에 이 프리팹/SO를 Inspector 또는 에디터 유틸리티로 주입.
- 밸런스는 이후 플레이 테스트로 조정.

### 다중 스폰 보스 메커니즘 (Phase 12-C 확정)

일부 보스는 동시에 여러 개체로 등장해야 한다(현재 대상: TwinPhantoms). OCP를 만족하면서 이를 지원하기 위해 다음 구조를 도입한다.

**1. `BossStatData` 확장 필드 (범용 다중 스폰 데이터)**

- `int spawnCount` — 이 보스를 몇 개체 스폰할지. 기본 1.
- `Vector2[] spawnOffsets` — 스폰 시 기준 좌표(0,0)에서의 상대 오프셋 배열. 길이는 `spawnCount`와 동일해야 함.
- 단체 보스(Titan 등)는 `spawnCount=1`, `spawnOffsets=[(0,0)]`.

**2. `BossRushManager` 스폰 흐름**

- `Spawn(bossIndex)` 호출 시 해당 SO의 `spawnCount`만큼 프리팹을 반복 인스턴스화.
- 각 인스턴스는 `spawnOffsets[i]` 위치에 배치.
- 스폰된 모든 인스턴스가 `IMultiBossSibling`을 구현하면, 스폰 직후 전체 리스트를 각 인스턴스에 `SetSiblings()`로 주입.
- 해당 boss stage의 클리어 판정: **모든 인스턴스의 HP가 0**일 때 (하나라도 살아 있으면 진행 중).

**3. `IMultiBossSibling` 인터페이스**

```
namespace Game.Enemies
{
    public interface IMultiBossSibling
    {
        void SetSiblings(IReadOnlyList<BossPresenterBase> siblings);
    }
}
```

- 구현 대상: 형제 인스턴스를 알아야 하는 Presenter(예: `TwinPhantomsBossPresenter` — partner 사망 시 생존자 강화 로직용).
- TwinPhantomsBossPresenter는 `SetSiblings()`에서 자신을 제외한 나머지를 `_partner`(또는 리스트)로 저장.
- 단체 보스는 이 인터페이스를 구현하지 않는다(ISP).

**4. TwinPhantoms 설정값**

- `spawnCount = 2`
- `spawnOffsets = [(-2, 0), (2, 0)]`
- 클리어 판정: 둘 다 사망 시.
- 기존 Presenter의 "partner 사망 시 생존자 ATK/Speed 강화" 로직은 유지되며, `SetSiblings()`가 그 partner 참조를 주입한다.

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
