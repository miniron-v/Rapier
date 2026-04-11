# 미션 시스템 (Missions)

일일/주간 미션 트래킹과 보상. 스테이지 진행과 독립된 메타 진행 축.

## 1. 일일 미션 (5종, 매일 04:00 리셋)

| # | 미션 | 이벤트 | 보상 |
|---|---|---|---|
| 1 | 스테이지 1회 클리어 | OnStageCleared | 골드 500 + 가챠 티켓 1 |
| 2 | 보스 5마리 처치 | OnBossKilled | 골드 300 |
| 3 | 저스트 회피 3회 | OnJustDodgeTriggered | 골드 200 + 강화 재료 5 |
| 4 | 차지 스킬 10회 | OnChargeSkillUsed | 골드 200 |
| 5 | 일일 4개 완료 (메타) | OnDailyMissionCompleted | 가챠 티켓 1 |

## 2. 주간 미션 (3종, 매주 월요일 04:00 리셋)

| # | 미션 | 이벤트 | 보상 |
|---|---|---|---|
| 1 | 보스 50마리 누적 | OnBossKilled | 가챠 티켓 5 + 골드 3000 |
| 2 | 신규 도달 또는 최고 기록 | OnStageRecordUpdated | 룬 가챠 티켓 3 |
| 3 | 일일 7일 완료 | OnDailyAllCompleted | 에픽 장비 확정 1 |

## 3. 책임 분리

| 객체 | 책임 |
|---|---|
| `MissionData` (SO) | 목표 수치, 보상, 추적 이벤트 |
| `MissionProgressModel` | 진행 상태 (현재 수치, 완료 여부, 수령 여부) |
| `MissionTracker` | 게임 이벤트 구독 → 미션 진행 갱신 |
| `MissionResetScheduler` | 일일/주간 리셋 시각 체크 및 초기화 |

## 4. 저장

미션 진행 상태는 `PROGRESSION.md §5` 의 저장 시스템에서 "미션" 카테고리로 직렬화 (일일/주간 진행 + 마지막 리셋 시각).
