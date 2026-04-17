# 인터미션 (Intermission)

메인 스테이지와 독립된 별도 컨텐츠. 기록 도전 등 확장 방향은 후속 기획에서 재정의한다. 본 문서는 현 시점의 설계 정의를 담는다.

## 1. 역할

두 효과 동시: (1) **HP 100% 회복** (자동, 강제), (2) **스탯 선택** (능동, 2개 후보 중 1개).

## 2. 규칙

풀에서 매번 2개 랜덤 추출 (서로 다른 2개 보장, 같은 종류 동시 금지). 누적 가능 (같은 스탯 재등장 시 누적 적용). 풀 7종 + 강도는 `Rapier_Prototype_DesignDoc.md §8-2`.

## 3. 객체 책임

| 객체 | 책임 |
|---|---|
| `IntermissionManager` (MonoBehaviour) | 회복 트리거, 후보 추출, 선택 UI 처리 |
| `StatPickPool` (static class) | 후보 풀 (스탯 종류 + 강도) — 하드코딩, SO 아님 |
| `RunStatContainer.Apply()` | 선택 누적/적용 (`STATS.md` 참조) |
