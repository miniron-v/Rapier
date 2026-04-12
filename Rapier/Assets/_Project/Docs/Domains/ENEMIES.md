# 적 시스템 (Enemies)

## 1. 클래스 계층

```
EnemyPresenterBase (abstract)       ← Chase/Windup/Hit/PostAttack + 시퀀서
├── NormalEnemyPresenter            ← WaveManager 오브젝트 풀
└── BossPresenterBase (abstract)    ← 가변 페이즈 (1~3+), 페이즈별 시퀀스 교체
    ├── TitanBossPresenter
    ├── SpecterBossPresenter
    ├── PyromancerBossPresenter
    ├── BerserkerBossPresenter
    ├── StormcallerBossPresenter
    ├── GravekeeperBossPresenter
    └── TwinPhantomsBossPresenter
```

- `EnemyModel`: 순수 데이터 (HP/상태). `EnemyView`: 시각/애니.
- `EnemyStatData` (SO): 일반 적 스탯 + attackSequence.
- `BossStatData` (SO): `EnemyStatData` 확장 + `phase2Sequence` (2페이즈 시퀀스, 단일 필드) + HP 50% 임계치 하드코딩 (`BossPresenterBase`) + 다중 스폰 필드 (§4). 3페이즈 이상은 자식 Presenter 에서 별도 필드로 확장 (예: `StormcallerBossPresenter.phase3Sequence`).

## 2. 상태 머신

`Chase → Windup → Hit → PostAttack → Chase` 반복. `EnemyAttackSequencer` 가 attackSequence 순회, `BossPresenterBase` 가 페이즈 임계 HP 도달 시 시퀀스 교체.

## 3. 공격 시스템

상세는 `COMBAT.md`. 적 고유 사항만 여기 기술.

- 공격은 `EnemyAttackAction` 파생, `[SerializeReference]` 리스트로 SO 직렬화.
- 보스 Presenter 에 공격 로직 없음 — AttackAction SO 에 완전 위임.

## 4. 보스 라인업 (7종)

모든 보스 스크립트/SO/프리팹/공격 패턴 구현 완료.

| # | 이름 | 페이즈 | AttackAction |
|---|---|---|---|
| 1 | Titan | 2 | Melee, Charge |
| 2 | Specter | 2 | Teleport, Melee |
| 3 | Pyromancer | 2 | Projectile, GroundHazard, MultiDirectional |
| 4 | Berserker | 2 | Melee×3~4 콤보, Charge |
| 5 | Stormcaller | 3 | MultiDirectional, Projectile(유도) |
| 6 | Gravekeeper | 2 | Summon, Melee |
| 7 | TwinPhantoms | 1 | Melee×2 (본체+분신 협공) |

- **프리팹 경로**: `Assets/_Project/Prefabs/Boss/{BossName}_Boss.prefab`. 구조: `SpriteRenderer` + `Rigidbody2D` + `Collider2D` + `*BossPresenter` + `*StatData` 참조 + 필요한 `*AttackAction`.
- **스테이지**: 실 게임 플로우는 `StageDemo.unity` (나선비경식) 가 담당. `PROGRESSION.md` 참조.
- **개별 테스트**: 단일 보스만 배치한 별도 씬으로 수행. 통합 러시 씬은 더 이상 사용하지 않음.
- **재사용 규칙**: 한 스테이지 내 같은 보스 재출현 금지. 다른 스테이지에서 강화 버전 재활용 가능 (`Rapier_Prototype_DesignDoc.md §5-2`).

### Gravekeeper 미니언

- 프리팹: `Assets/_Project/Prefabs/Enemies/GravekeeperMinion.prefab`
- SO: `Assets/_Project/ScriptableObjects/Enemies/GravekeeperMinionData.asset`
- 구조: `Enemy_Template.prefab` 미러, `NormalEnemyPresenter`. 스프라이트 재사용.
- 공격: `MeleeAttackAction` 1종 (`[SerializeReference]`).
- 임시 스탯: HP 30 / ATK 5 / Speed 3, 주기 1.5s / Windup 0.5s.
- 연결: `GravekeeperBossPresenter._minionPrefab`, `_minionData` 에 주입.

### 다중 스폰 보스

동시 다개체 등장 지원 (현재 TwinPhantoms). OCP 준수 구조:

1. **`BossStatData` 필드**: `int spawnCount` (기본 1), `Vector2[] spawnOffsets` (길이 = spawnCount). 단체 보스는 `spawnCount=1`, `offsets=[(0,0)]`.
2. **스폰 흐름**: 보스 스폰 시 `spawnCount` 만큼 반복 인스턴스화, 각 `spawnOffsets[i]` 배치. `IMultiBossSibling` 구현체면 스폰 직후 전체 리스트를 `SetSiblings()` 로 주입. 클리어 판정: 모든 인스턴스 HP 0.
3. **`IMultiBossSibling`** (`namespace Game.Enemies`): `void SetSiblings(IReadOnlyList<BossPresenterBase>)`. 형제 인식 필요 시만 구현 (ISP). `TwinPhantomsBossPresenter` 는 자신 제외 나머지를 `_partner` 로 보관.
4. **TwinPhantoms**: `spawnCount=2`, `spawnOffsets=[(-2,0),(2,0)]`. 둘 다 사망 시 클리어. "partner 사망 시 생존자 ATK/Speed 강화" 로직은 `SetSiblings()` 의 partner 참조로 구동.

### 패턴 예시

- **Titan**: P1 `Melee×3 → 반복`, P2 `Melee×3 → Charge → 반복`
- **Specter**: P1 `Melee → 반복`, P2 `Teleport → Melee → 반복`
- 나머지 보스의 정확한 시퀀스는 각 `*StatData.asset` 에 직렬화되어 있음.

## 5. 일반 적

분산 접근 AI (뭉치기 방지). 공격 주기 1.5s / Windup 0.5s. WaveManager 오브젝트 풀.

## 6. 에디터 유틸

- `BossDataSetup.cs` (`Scripts/Enemies/Boss/Editor/`): Specter/Pyromancer/Berserker/Stormcaller/Gravekeeper/TwinPhantoms Setup 메뉴 통합.
- `TitanDataSetup.cs` (`Docs/Editor/`, 레거시): `Rapier/Dev/Setup Titan Attack Sequence`
- `EnemyDataSetup.cs` (`Docs/Editor/`, 레거시): `Rapier/Dev/Setup Normal Enemy Sequence`, `Setup Specter Sequence`
- `[SerializeReference]` 갱신 순서: null 초기화 → SetDirty → SaveAssets → ImportAsset → 재할당.
