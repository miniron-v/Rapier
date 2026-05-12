<p align="center">
  <img src="https://img.shields.io/badge/Unity-2D%20URP-000000?style=for-the-badge&logo=unity&logoColor=white" alt="Unity">
  <img src="https://img.shields.io/badge/Platform-Android%20%7C%20iOS%20%7C%20PC-3DDC84?style=for-the-badge" alt="Platform">
  <img src="https://img.shields.io/badge/Genre-Action%20RPG-FF6B6B?style=for-the-badge" alt="Genre">
  <img src="https://img.shields.io/badge/Version-0.1.5-blue?style=for-the-badge" alt="Version">
</p>

<h1 align="center">RAPIER</h1>

<p align="center">
  <b>한 손가락으로 완성하는 보스 러시 액션 RPG</b><br>
  <sub>Single-finger Boss Rush Action RPG</sub>
</p>

---

## About

**Rapier**는 출퇴근길 지하철 안에서 엄지 하나로 플레이하는 세로 화면 실시간 액션 RPG입니다.

한 스테이지에 보스 하나. 약 3분이면 한 판이 끝납니다.  
탭으로 공격하고, 스와이프로 회피하고, 홀드로 스킬을 충전하세요.  
적의 공격을 회피 중에 맞으면 **저스트 회피**가 발동 -- 슬로우 모션과 함께 캐릭터 고유 스킬이 열립니다.

---

## Combat System

### One-Finger, Four Actions

전체 화면이 입력 영역입니다. 버튼 없이, 제스처 하나로 전투합니다.

| Gesture | Action | Description |
|:-------:|:------:|:------------|
| **Tap** | Attack | 전방 광역 공격. 즉시 히트 판정 |
| **Swipe** | Dodge | 입력 방향으로 무적 대시. 쿨다운 2초 |
| **Drag** | Move | 가상 조이스틱 이동 |
| **Hold & Release** | Charge Skill | 게이지 충전 후 캐릭터 고유 스킬 발동 |

### Just Dodge

회피 중 적의 공격 범위에 닿으면 **저스트 회피**가 발동합니다.

> 시간이 느려지고, 카메라가 줌인되며, 캐릭터 고유 스킬을 차지 없이 즉시 사용할 수 있습니다.

---

## Characters

4명의 캐릭터가 같은 입력 체계 위에서 완전히 다른 전투 스타일을 만들어냅니다.

### Rapier -- Build-up & Harvest

표식을 쌓고, 한 번에 터뜨리는 수확형 딜러.

- **저스트 회피** -> 적에게 대시하며 표식 부여 (최대 5중첩)
- **차지 스킬** -> 모든 표식을 소비하여 폭발 데미지

### Warrior -- Endurance & Parry

방패로 버티다 반격하는 탱커 파이터.

- **홀드** -> 방패 방어 (데미지 감소)
- **홀드 + 스와이프** -> 방패 밀쳐내기. 피격 중이면 **패링** 판정
- **차지 스킬** -> 대지 분쇄 (전방 광역)

### Assassin -- Phantom Stacking

잔상을 쌓아 분신과 함께 난무하는 어쌔신.

- **저스트 회피** -> 이전 위치에 잔상 생성
- 잔상 활성 중 본체의 모든 공격에 잔상이 **동시 참여**
- **차지 스킬** -> 360도 원형 베기

### Ranger -- Distance Control & Minefield

거리를 벌리며 화망을 깔아두는 원거리 딜러.

- **탭** -> 원거리 사격 (근접 대체)
- **회피** -> 대시 착지 지점에 **지뢰 설치**
- **차지 스킬** -> 직선 관통 화살 + 적 경직

---

## Bosses

104개 스테이지에 걸쳐 7종의 보스가 순환하며, 사이클마다 강해집니다.  
모든 보스는 **공격 예고 인디케이터**(아웃라인 + 스캔라인)로 타이밍을 읽을 수 있습니다.

| # | Boss | Concept | Teaches You |
|:-:|:----:|:-------:|:------------|
| 1 | **Titan** | Heavy Charger | 회피 타이밍, 안전 거리 감각 |
| 2 | **Specter** | Teleport Assassin | 위치 예측, 순간 대응 |
| 3 | **Pyromancer** | Ranged Mage | 장판 회피, 거리 좁히기 |
| 4 | **Berserker** | Fast Combo | 콤보 사이 회피 끼워넣기 |
| 5 | **Stormcaller** | Multi-Directional | 안전 방향 판단 |
| 6 | **Gravekeeper** | Summoner | 우선순위 판단, 미니언 관리 |
| 7 | **Twin Phantoms** | Dual Boss | 다중 위협 동시 관리 |

---

## Progression

```
                 ┌─────────────┐
                 │    Lobby    │
                 └──────┬──────┘
                        │
              ┌─────────▼─────────┐
              │   Stage Select    │
              │  (104 Stages)     │
              └─────────┬─────────┘
                        │
              ┌─────────▼─────────┐
              │   Boss Battle     │
              │   (~3 min/run)    │
              └─────────┬─────────┘
                        │
              ┌─────────▼─────────┐
              │  Clear / Retry    │
              │  Equipment Drop   │
              └───────────────────┘
```

### Equipment System

- **8 Armor Slots** -- Weapon, Hat, Top, Bottom, Shoes, Necklace, Ring x2
- **4 Grades** -- Normal -> Rare -> Epic -> Unique (룬 소켓 1~3개)
- **Rune System** -- 캐릭터별 고유 강화 (표식 +1, 패링 범위 확대 등)
- **Gacha** -- 보스별 테마 장비 드랍 + 가챠 배너

### Stat System

능력치는 **MetaStat** (영구: 장비, 레벨)과 **RunStat** (일회: 인터미션 선택)으로 분리됩니다.

```
Final = (Base + MetaFlat) x (1 + Meta%) x (1 + Run%) + RunFlat
```

> HP, ATK, Move Speed, Crit Chance/Damage, Skill Damage, CDR, Dodge CDR, Invincibility Bonus

---

## Tech Stack

| | |
|---|---|
| **Engine** | Unity (URP 2D, New Input System) |
| **Architecture** | MVP (Model-View-Presenter) |
| **DI** | Manual Init() injection + ServiceLocator |
| **Data** | ScriptableObject-driven (stats, patterns, drops) |
| **Principles** | SOLID -- new characters extend, never modify base |
| **Target** | .NET Standard 2.1, Portrait orientation |

---

## Project Structure

```
Assets/_Project/
├── Scripts/
│   ├── Characters/    # Rapier, Warrior, Assassin, Ranger, Base
│   ├── Combat/        # Damage, indicators, hit detection
│   ├── Core/          # Game loop, ServiceLocator, utilities
│   ├── Data/          # Save system, stat containers
│   ├── Enemies/       # Boss AI, patterns, spawning
│   ├── Input/         # GestureRecognizer (tap/swipe/drag/hold)
│   └── UI/            # HUD, lobby, gacha, inventory
├── ScriptableObjects/ # Characters, enemies, equipment, stages
├── Prefabs/           # Player, bosses, enemies
└── Scenes/            # Lobby, StageDemo, BossRushDemo
```

---

## Design Philosophy

> **Action Feel** -- 단일 손가락 조작으로 완성하는 직관적 전투  
> **Clear Turns** -- 이동과 공격 간 간섭을 제거한 피지컬 전투  
> **Fair Monetization** -- 무과금도 실력으로 극복 가능, 과금 시 쾌적함 제공

---

<p align="center">
  <sub>Made by <b>Reku</b></sub>
</p>
