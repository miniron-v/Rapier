// =============================================================
// DocSyncTool.cs  —  문서 동기화 도구 (MD -> DOCX)
// =============================================================
// 메뉴: Rapier/Docs/Sync to DOCX
//       Rapier/Docs/Create DesignDoc MD
//       Rapier/Docs/Create Guidelines MD
//
// 운영 방식:
//   1. 각 템플릿 함수가 .md 파일의 원본
//   2. 이 스크립트로 Pandoc 호출 -> .docx 자동 생성
//   3. 팀원은 생성된 .docx를 열람
// =============================================================

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;

public class DocSyncTool : EditorWindow
{
    private const string DocsPath = "Assets/_Project/00_Docs";

    // ── Sync to DOCX ─────────────────────────────────────────
    [MenuItem("Rapier/Docs/Sync to DOCX")]
    public static void SyncAllDocs()
    {
        bool anyConverted = false;

        string designDocMd   = Path.GetFullPath(Path.Combine(DocsPath, "Rapier_Prototype_DesignDoc.md"));
        string designDocDocx = Path.GetFullPath(Path.Combine(DocsPath, "Rapier_Prototype_DesignDoc.docx"));
        if (File.Exists(designDocMd))
        {
            ConvertWithPandoc(designDocMd, designDocDocx);
            anyConverted = true;
        }
        else
        {
            UnityEngine.Debug.LogWarning("[DocSync] DesignDoc MD not found. 'Rapier/Docs/Create DesignDoc MD' 메뉴로 먼저 생성하세요.");
        }

        string guidelinesMd   = Path.GetFullPath(Path.Combine(DocsPath, "PROJECT_GUIDELINES.md"));
        string guidelinesDocx = Path.GetFullPath(Path.Combine(DocsPath, "PROJECT_GUIDELINES.docx"));
        if (File.Exists(guidelinesMd))
        {
            ConvertWithPandoc(guidelinesMd, guidelinesDocx);
            anyConverted = true;
        }
        else
        {
            UnityEngine.Debug.LogWarning("[DocSync] PROJECT_GUIDELINES.md not found. 'Rapier/Docs/Create Guidelines MD' 메뉴로 먼저 생성하세요.");
        }

        if (anyConverted)
        {
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[DocSync] 문서 동기화 완료.");
            EditorUtility.DisplayDialog("DocSync", "DOCX 변환 완료!", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("DocSync", "변환할 MD 파일을 찾을 수 없습니다.", "확인");
        }
    }

    // ── Create DesignDoc MD ───────────────────────────────────
    [MenuItem("Rapier/Docs/Create DesignDoc MD")]
    public static void CreateDesignDocMd()
    {
        CreateMd(
            Path.GetFullPath(Path.Combine(DocsPath, "Rapier_Prototype_DesignDoc.md")),
            GetDesignDocTemplate(),
            "Rapier_Prototype_DesignDoc.md");
    }

    // ── Create Guidelines MD ──────────────────────────────────
    [MenuItem("Rapier/Docs/Create Guidelines MD")]
    public static void CreateGuidelinesMd()
    {
        CreateMd(
            Path.GetFullPath(Path.Combine(DocsPath, "PROJECT_GUIDELINES.md")),
            GetGuidelinesTemplate(),
            "PROJECT_GUIDELINES.md");
    }

    // ── 공통 MD 생성 유틸 ─────────────────────────────────────
    private static void CreateMd(string path, string content, string fileName)
    {
        if (File.Exists(path))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "DocSync", $"이미 파일이 존재합니다.\n{fileName}\n덮어쓰시겠습니까?", "덮어쓰기", "취소");
            if (!overwrite) return;
        }

        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log($"[DocSync] MD 생성 완료: {path}");
        EditorUtility.DisplayDialog("DocSync", $"{fileName} 생성 완료!", "확인");
    }

    // ── Pandoc 변환 ───────────────────────────────────────────
    private static void ConvertWithPandoc(string inputMd, string outputDocx)
    {
        string fileName = Path.GetFileName(inputMd);

        string localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        string[] pandocCandidates =
        {
            "pandoc",
            @"C:\Program Files\Pandoc\pandoc.exe",
            Path.Combine(localAppData, "Pandoc", "pandoc.exe")
        };

        string pandocPath = null;
        foreach (var candidate in pandocCandidates)
        {
            try
            {
                var test = new ProcessStartInfo
                {
                    FileName               = candidate,
                    Arguments              = "--version",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };
                using var testProc = Process.Start(test);
                testProc.WaitForExit();
                if (testProc.ExitCode == 0) { pandocPath = candidate; break; }
            }
            catch { }
        }

        if (pandocPath == null)
        {
            UnityEngine.Debug.LogError("[DocSync] Pandoc을 찾을 수 없습니다. 설치 여부를 확인하세요.");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = pandocPath,
                Arguments              = $"\"{inputMd}\" -o \"{outputDocx}\" --from markdown --to docx",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };

            using var process = Process.Start(psi);
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
                UnityEngine.Debug.Log($"[DocSync] 변환 성공: {fileName} -> {Path.GetFileName(outputDocx)}");
            else
                UnityEngine.Debug.LogError($"[DocSync] Pandoc 오류 ({fileName}): {stderr}");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[DocSync] Pandoc 실행 실패.\n{e.Message}");
        }
    }

    // ── 기획서 템플릿 ─────────────────────────────────────────
    private static string GetDesignDocTemplate()
    {
        return
@"# RAPIER 프로토타입 기획서
**Prototype Design Document v1.4.0**
2026-03-16

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 장르 | 싱글 플레이 실시간 액션 RPG |
| 플랫폼 | 모바일 (Android / iOS), PC 테스트 지원 |
| 화면 방향 | 세로 모드 (Portrait) |
| 렌더 파이프라인 | URP 2D |
| 조작 | 전체 화면, 단일 손가락(엄지) / PC 마우스 지원 |
| 클리어 조건 | 보스 처치 |
| 프로토타입 목표 | 핵심 전투 메커니즘 검증 및 재미 확인 |

---

## 2. 게임 구조

### 2-1. 웨이브 구성

| 항목 | 내용 |
|------|------|
| 웨이브당 적 수 | 5마리 |
| 웨이브 전환 방식 | 전투 종료 무관. 10초마다 자동으로 다음 웨이브 추가 등장 |
| 웨이브 누적 방식 | 이전 웨이브 적 미처치 시 다음 웨이브와 동시에 존재 가능 |
| 최종 웨이브 | 보스 1마리 등장 (별도 트리거 또는 N웨이브 후) |
| 클리어 조건 | 보스 처치 |

### 2-2. 보스 패턴

| 패턴 | 횟수/주기 | 설명 |
|------|-----------|------|
| 일반 공격 | 3회 연속 (주기 2초) | 전방 근거리 단일 타격 |
| 광역 스킬 | 3회 일반 공격 후 1회 | 주변 360도 범위 광역 피해 |
| 패턴 루프 | 무한 반복 | 일반 3회 → 광역 1회 → 반복 |

---

## 3. 조작 체계

입력 유효 영역: 전체 화면. 단일 손가락(엄지) 기준. PC에서는 마우스로 에뮬레이션.

### 3-1. 기본 입력

| 입력 | 상태 | 조건 | 설명 |
|------|------|------|------|
| Drag | Move | 이동 거리 ≥ 20px, 지속 ≥ 0.3초 | 가상 조이스틱과 함께 이동. 공격 판정 없음 |
| Tap | Attack | 이동 거리 < 20px, 지속 < 0.2초 | 전방 사각형 범위 광역 공격. 즉시 히트 판정. 인디케이터 0.4초 표시. 딜레이 중 연타 차단 |
| Swipe | Dodge | 이동 거리 ≥ 20px, 지속 < 0.3초 | 방향 회피. 회피 대시 전 구간 무적. 쿨다운 2초. 쿨다운 중 시도 시 차단 |
| Hold → Release | Charge → Skill | 정지 상태, 지속 ≥ 0.3초 | 홀드 중 게이지 충전, 완충 후 손을 떼면 차지 스킬 발동 |

### 3-2. 저스트 회피 시스템

저스트 회피: 회피 대시 중 적의 공격을 받으면 발동. 한 회피당 딱 한 번만 발동 가능.

| 단계 | 설명 |
|------|------|
| 발동 조건 | 회피 대시 중(Swipe 후 목적지 도달 전) 적의 공격을 피격 시 발동. 한 회피당 1회 한정 |
| 슬로우 모션 | 저스트 회피 성공 시 일정 시간 게임 속도 감소 (AnimationCurve 기반, 기본 3초) |
| 카메라 줌 | 저스트 회피 성공 시 카메라가 플레이어를 향해 줌인 후 복귀 (AnimationCurve 기반) |
| 무적 구간 | 저스트 회피 슬로우모션 구간 전체 무적 유지 |
| 슬로우 중 Hold | 캐릭터 고유 스킬 즉시 발동 |

---

## 4. 캐릭터 설계

### 4-1. 공통 스탯 (플레이어)

| 항목 | 수치 | 비고 |
|------|------|------|
| 최대 HP | 500 | 일반 적 10회, 보스 일반 공격 약 6회에 사망 |
| 기본 공격력 | 50 (사각형 범위 광역) | 일반 적 5타, 보스 40타 처치 기준 |
| 이동속도 | 5 | 화면 절반을 약 1초에 이동 |
| 무적 구간 | 회피 대시 전 구간 | unscaledTime 기반. 저스트 회피는 슬로우모션 구간 추가 |
| 회피 쿨다운 | 2초 | 쿨다운 중 Swipe 입력 차단. HUD 세로 게이지로 표시 |

### 4-2. 캐릭터별 고유 메커니즘

기본 입력(Tap/Drag/Swipe/Hold)의 동작은 3장 조작 체계를 따른다.
아래는 각 캐릭터가 기본 시스템과 다르게 동작하는 부분만 기술한다.

#### 전사 (Warrior)

- Hold 중: 방패 방어 추가 (피해 감소 또는 무효화) — 기본 홀드의 게이지 충전에 더해 방어 효과 발동
- Hold 중 Swipe: 방패 밀쳐내기 (적 넉백 + 경직) — Release 대신 Swipe로 차지 스킬 변형 가능
- 패링 성공 시: 즉시 반격 가능 상태 전환
- 저스트 회피 후 슬로우 중 Hold (고유 스킬): 즉시 광역 공격

#### 암살자 (Assassin)

- 저스트 회피 시: 회피 전 위치에 잔상 생성 (피해 없음, 어그로 없음)
- 잔상 활성 중: 본체의 모든 공격(Tap, 차지 스킬)에 잔상이 동시에 동참
- 저스트 회피 후 슬로우 중 Hold (고유 스킬): 즉시 360도 광역 공격
- 차지 스킬 (Hold → Release): 360도 광역 공격

#### 레이피어 (Rapier)

- 저스트 회피 후 슬로우 중 Hold (고유 스킬):
  가장 가까운 적을 향해 대시 → 도착 시 사각형 범위 내 전체 적에게 데미지 + 표식 1중첩 부여
  → 회피 목적지(DodgeDest)로 복귀. 표식 최대 5중첩.
- 무적 구간: 회피 대시 전 구간 + 저스트 회피 슬로우 구간 + 스킬 대시~복귀 구간 전체.
- 스킬 및 회피 진행 중 일반 공격 차단.
- 차지 스킬 (Hold → Release): 표식이 있는 모든 적을 각 보유 표식 중첩 수 × (공격력 × 배율) 데미지로 공격. 이후 모든 표식 소비.

#### 사냥꾼 (Ranger)

- Tap: 기본 공격이 원거리 사격으로 대체
- 일반 회피 시: 회피 지점에 지뢰 설치
- 저스트 회피 후 슬로우 중 Hold (고유 스킬): 즉시 강화 화살 발사
- 차지 스킬 (Hold → Release): 강화 화살 발사

---

## 5. 적 설계

### 5-1. 일반 적

| 항목 | 수치 | 비고 |
|------|------|------|
| 최대 HP | 250 | 광역 공격 고려 상향. 단일 기준 플레이어 5타 |
| 공격력 | 50 | 플레이어 10회 피격 사망 기준 |
| 이동속도 | 2.5 | 플레이어의 절반 속도 |
| 공격 주기 | 1.5초 | 타이밍 학습 가능한 주기 |
| 공격 예고 | 0.5초 Windup | 색상 변화 + 원형 범위 스프라이트로 시각 예고 |
| 접근 방식 | 분산 접근 | 각자 다른 각도에서 접근. 뭉치기 방지 |

### 5-2. 보스

| 항목 | 수치 | 비고 |
|------|------|------|
| 최대 HP | 2000 | 플레이어 40타 처치. 약 2분 보스전 상정 |
| 일반 공격력 | 80 | 플레이어 약 6회 피격 사망 |
| 광역 스킬 데미지 | 120 | 피격 시 HP 약 24% 감소. 회피 필수 |
| 일반 공격 주기 | 2초 (3회) | 패턴 학습 가능한 주기 |
| 광역 스킬 주기 | 3회 일반 후 1회 | 예측 가능한 고정 패턴 |
| 이동속도 | 1.5 | 느리고 무거운 움직임 |

---

## 6. 밸런스 수치 요약

| 구분 | HP | 공격력 | 이동속도 | 비고 |
|------|----|--------|----------|------|
| 플레이어 | 500 | 50 (사각형 범위 광역) | 5.0 | 회피 대시 전 구간 무적, 쿨다운 2초 |
| 일반 적 | 250 | 50 / 주기 1.5초 | 2.5 | 분산 접근 AI, 0.5초 Windup 예고 |
| 보스 (일반) | 2000 | 80 / 주기 2초 | 1.5 | 3회 후 광역 |
| 보스 (광역 스킬) | - | 120 | - | 3회 일반 후 1회 |
| 레이피어 차지 스킬 | - | 중첩 수 × (공격력 × 배율) | - | 각 적마다 보유 표식 중첩 수만큼 피해 |

---

## 7. 구현 우선순위 (프로토타입 단계)

| Phase | 내용 | 완료 기준 |
|-------|------|-----------|
| Phase 1 | ServiceLocator / InputState / GestureRecognizer | 입력 4종 판별 및 저스트 회피 타이밍 감지 동작 |
| Phase 2 | 캐릭터 기반 클래스 (Interface, Model, PresenterBase, View) | 플레이스홀더 스프라이트로 MVP 구조 동작 |
| Phase 3 | 플레이어 이동 + 카메라 추적 | Drag → 이동, 화면 추적 확인 |
| Phase 4 | 전투 기반 (CombatSystem, 일반 적, 웨이브 매니저) | 피격 판정, HP 감소, 10초 자동 웨이브 확인 |
| Phase 5 | UI 연결 (HP바, 차지 게이지, Debug 패널) | 전체 루프 플레이 가능 |
| Phase 6 | 전투 고도화 + 저스트 회피 연출 | 슬로우, 무적, 쿨다운, 카메라 줌, HUD 피드백 확인 |
| Phase 7 | 이동 시스템 리팩토링 + 레이피어 고유 메커니즘 | 표식 시스템 + 대시 스킬 + 차지 스킬 동작 확인 |

---

## 8. 변경 이력

| 버전 | 날짜 | 내용 |
|------|------|------|
| v1.0.0 | 2026-03-07 | 최초 작성. 웨이브/보스/캐릭터/밸런스 수치 확정 |
| v1.1.0 | 2026-03-07 | 저스트 회피 시스템 명세 추가. 강화 스킬 발동 조건 명확화. 웨이브 전환 방식 수정 (10초 자동 추가 등장) |
| v1.2.0 | 2026-03-10 | 저스트 회피 슬로우 중 Hold를 캐릭터 고유 스킬로 분리. 암살자 잔상 발동 조건 변경. 레이피어/차지 스킬 전면 재기술. |
| v1.3.0 | 2026-03-10 | Swipe(Dodge) 쿨다운 2초 추가. 공격 딜레이 0.5초 및 연타 차단 명세 추가. 저스트 회피 카메라 줌 연출 추가. 적 공격 예고(Windup 0.5초) 추가. Phase 6/7 구현 항목 갱신. |
| v1.4.0 | 2026-03-16 | 입력 유효 영역 하단 40% → 전체 화면으로 변경. 공격 즉시 히트 판정 + 인디케이터 0.4초 표시로 변경. 저스트 회피 발동 조건 명확화(회피 대시 중 피격, 한 회피당 1회). 레이피어 고유 스킬 상세 기술 갱신(사각형 범위 전체 적, DodgeDest 복귀, 무적 구간 명세). Phase 7 완료 처리. |
";
    }

    // ── 가이드라인 템플릿 ─────────────────────────────────────
    private static string GetGuidelinesTemplate()
    {
        return
@"# 프로젝트 개발 지침서 (Project Guidelines)

> **버전**: v0.5.0
> **최초 작성일**: 2026-03-05
> **목적**: 본 문서는 프로젝트 전반의 아키텍처, 코딩 컨벤션, 폴더 구조, 협업 규칙을 정의합니다.
> 모든 개발자(인간 및 AI)는 코드 작성 전 반드시 이 문서를 숙지하고, 작업 시 지침으로 삼아야 합니다.

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 장르 | 싱글 플레이 실시간 액션 RPG |
| 플랫폼 | 모바일 (Android / iOS), PC 테스트 지원 |
| 화면 방향 | 세로 모드 (Portrait) |
| 렌더 파이프라인 | URP 2D |
| 조작 | 전체 화면, 단일 손가락(엄지) 조작 / PC 마우스 지원 |
| 클리어 조건 | 보스 처치 |
| 목표 | 프로토타입 → 안정적 서비스 → 수익화 |

### 핵심 조작 상태 (Input States)

| 입력 | 상태 | 조건 | 설명 |
|------|------|------|------|
| Drag | Move | 이동 거리 ≥ 20px, 지속 ≥ 0.3초 | 순수 이동. 공격 판정 없음 |
| Tap | Attack | 이동 거리 < 20px, 지속 < 0.2초 | 전방 사각형 범위 광역 공격. 즉시 히트 판정. 인디케이터 0.4초 표시 |
| Swipe | Dodge | 이동 거리 ≥ 20px, 지속 < 0.3초 | 방향 회피. 회피 대시 전 구간 무적. 쿨다운 2초 |
| Hold → Release | Charge → Skill | 정지 상태, 지속 ≥ 0.3초 | 스킬 차지 후 발동 |

---

## 2. 기술 스택 및 환경

- Unity 버전: 프로젝트 생성 버전 고정 (변경 시 팀 전체 합의 필요)
- 렌더 파이프라인: Universal Render Pipeline (URP) 2D
- Input System: Unity New Input System (com.unity.inputsystem)
- 언어: C# (.NET Standard 2.1)
- 최소 타겟: Android API 28 / iOS 13
- 개발 테스트 환경: PC (마우스 입력으로 모바일 터치 에뮬레이션)

---

## 3. 아키텍처 설계 원칙

### 기본 패턴: MVP (Model - View - Presenter)

```
[Model] <--Interface--> [Presenter] <--직접참조--> [View]
```

- **Model**: 순수 데이터와 상태. MonoBehaviour 금지. SO 또는 순수 C# 클래스.
- **View**: 화면 표시와 시각 연출만. 로직 금지. MonoBehaviour.
  - 위치 설정은 View가 직접 결정하지 않음. Presenter가 계산한 위치를 View.SetPosition()으로 전달받아 반영.
- **Presenter**: Model과 View 중재. 게임 로직의 핵심. MonoBehaviour.
  - 이동 위치 계산 책임: Walk/Dash/Skill 이동 모두 Presenter가 매 프레임 계산 후 View.SetPosition() 호출.

### DI (Dependency Injection) 전략

- 기본 원칙: 수동 DI 사용. Presenter 생성 시 Init() 메서드로 의존성을 명시적으로 주입.
- 전역 시스템(InputManager 등) 단일 접근점: ServiceLocator 패턴 허용 (남용 금지).
- DI 프레임워크(VContainer 등): 기획 안정화 및 씬 구조 확정 후 도입 검토.

### 테스트 전략

- 단위 테스트 대상: MonoBehaviour 의존성 없는 순수 로직.
  예) GestureRecognizer 판별 로직, 데미지 계산, SO 데이터 유효성 검사.
- 수동 테스트 대상: Presenter ↔ View 통합 동작, 플레이어 조작감.
- 테스트 ROI가 낮은 곳에 테스트 코드 강제 금지.

### 데이터 설계 원칙

- 캐릭터 스탯, 스킬 수치 등 순수 데이터는 ScriptableObject로 분리.
- 전투 상태(HP, 쿨타임 등) 런타임 데이터는 순수 C# 클래스 또는 구조체로 관리.
- Model은 MonoBehaviour를 상속하지 않으며 Unity 라이프사이클에 의존하지 않음.

### SOLID 원칙

| 원칙 | 적용 방법 |
|------|-----------|
| SRP | 클래스 하나는 하나의 책임만 |
| OCP | 캐릭터 추가 시 기존 코드 수정 없이 확장 |
| LSP | 자식 클래스는 부모를 완전히 대체 가능 |
| ISP | IAttackable, IDodgeable 등 작은 단위로 분리 |
| DIP | Presenter는 구체 View가 아닌 IView Interface에 의존 |

자식 고유 상태에 의존하는 로직은 반드시 자식 안에서만 처리.
Base와의 결합은 virtual/override 계약으로만 수행할 것.

### 금지 패턴

- Singleton 남용 금지
- View에서 로직 처리 금지 (이동 계산 포함)
- GameObject.Find(), SendMessage() 사용 금지

---

## 4. 폴더 구조

```
Assets/
├── Rapier-Private/               # 비공개 (별도 Git repo, .gitignore 제외)
│   ├── Art/
│   ├── Audio/
│   └── ThirdParty/
│
└── _Project/                     # 공개 저장소 대상
    ├── 00_Docs/                  # 개발 문서, 지침서
    │   └── Editor/
    ├── 10_Scripts/
    │   ├── Core/                 # Interfaces, Base, Utils, ServiceLocator
    │   ├── Input/                # GestureRecognizer, InputSystemInitializer
    │   ├── Combat/               # IDamageable
    │   ├── Characters/           # Base, Warrior, Assassin, Rapier, Ranger
    │   ├── Enemies/              # EnemyModel, EnemyView, EnemyPresenter, WaveManager, EnemyHpBar
    │   ├── UI/                   # HUD, Common
    │   └── Data/                 # EnemyStatData
    ├── 20_Prefabs/               # Characters, Enemies, Skills, UI
    ├── 30_ScriptableObjects/     # Characters, Skills
    └── 40_Scenes/                # SampleScene, _Test/
```

규칙: 모든 프로젝트 에셋은 반드시 _Project/ 하위에 위치.
.gitignore 제외 대상: Assets/Rapier-Private/

---

## 5. 네임스페이스 규칙

기본 형식: `Game.[시스템명]` 또는 `Game.[시스템명].[서브시스템명]`

| 네임스페이스 | 폴더 |
|-------------|------|
| Game.Core | Scripts/Core/ |
| Game.Input | Scripts/Input/ |
| Game.Combat | Scripts/Combat/ |
| Game.Characters | Scripts/Characters/ |
| Game.Characters.Warrior | Scripts/Characters/Warrior/ |
| Game.Characters.Assassin | Scripts/Characters/Assassin/ |
| Game.Characters.Rapier | Scripts/Characters/Rapier/ |
| Game.Characters.Ranger | Scripts/Characters/Ranger/ |
| Game.UI | Scripts/UI/ |
| Game.Data | Scripts/Data/ |

---

## 6. 코딩 컨벤션

### 명명 규칙

| 대상 | 규칙 | 예시 |
|------|------|------|
| 클래스 | PascalCase | PlayerPresenter |
| 인터페이스 | I + PascalCase | ICharacterView |
| Public 메서드 | PascalCase | TakeDamage() |
| Private 필드 | _ + camelCase | _currentHP |
| SerializeField | _ + camelCase | _moveSpeed |
| 상수 | UPPER_SNAKE_CASE | MAX_CHARGE_TIME |
| 이벤트 | On + PascalCase | OnTapPerformed |
| SO 클래스 | PascalCase + Data/Config | WarriorData |
| Enum 값 | PascalCase | InputState.Move |

### 파일 내부 순서

1. Serialized Fields
2. Private Fields
3. Properties
4. Unity Lifecycle (Awake → OnEnable → Start → Update → OnDisable → OnDestroy)
5. Public Methods
6. Private Methods
7. Event Handlers (On~ 접두사)
8. #if UNITY_EDITOR 블록

### 주석 규칙

- 공개 API: XML 문서 주석 (///) 필수
- 복잡한 로직: 인라인 주석으로 의도 설명
- 금지: 코드를 그대로 설명하는 주석

---

## 7. 이벤트 통신 규칙

| 상황 | 방식 |
|------|------|
| Presenter ↔ View 계약 | C# Interface |
| 동일 씬 내 시스템 간 통신 | C# event |
| 전역 시스템 단일 접근점 | ServiceLocator (남용 금지, 등록 목록 관리 필요) |
| 씬 경계를 넘는 글로벌 이벤트 | SO 이벤트 채널 (추후 도입) |

- 이벤트 구독은 OnEnable, 해제는 OnDisable에서 반드시 쌍으로 처리
- 핸들러 이름: Handle + 동사 (HandleTapPerformed)

---

## 8. ScriptableObject 활용 규칙

- 캐릭터 스탯, 스킬 설정값은 SO로 분리
- menuName 형식: 'Game/Data/[카테고리]/[이름]'
- 외부에는 읽기 전용 프로퍼티(=>)만 노출. setter 금지.

---

## 9. 씬 구성 전략

- 현재: 단일 씬 (SampleScene) — 프로토타입 단계
- 추후: Bootstrap(영속) + Gameplay(Additive) + UI(Additive) 분리
- 씬 분리는 기획 안정화 후 진행

---

## 10. Input System 규칙

### 입력 아키텍처

```
New Input System → GestureRecognizer → InputState Enum → C# event → CharacterPresenter
```

### 플랫폼 처리

- InputActions 에셋에서 Mobile(Touch)과 PC(Mouse) 바인딩을 모두 등록
- 로직 코드에서 플랫폼 분기(#if MOBILE 등) 금지

### 입력 유효 영역

- 전체 화면 (제한 없음)

### 제스처 구분 기준

| 제스처 | 판별 조건 |
|--------|-----------|
| Tap | 이동 거리 < 20px, 지속 시간 < 0.2초 |
| Swipe | 이동 거리 >= 60px, 지속 시간 < 0.25초 |
| Hold | 이동 없음, 지속 시간 >= 0.3초 |
| Drag | 이동 거리 >= 20px, 지속 시간 >= 0.25초 |

### 저스트 회피 트리거

- 회피 대시 중(JustDodgeAvailable == true) 피격 시 GestureRecognizer.TriggerJustDodge() 호출
- 한 회피당 1회만 발동. ConsumeJustDodge()로 소비.
- 디버그용 ForceJustDodge 제거됨 — TriggerJustDodge()가 유일한 발동 API

---

## 11. 작업 프로세스

### 기능 개발 사이클

1. 기획 확인 및 기술 설계 대화
2. Interface / Base 클래스 정의
3. Model → Presenter → View 순서로 구현
4. 에디터 테스트 씬에서 단독 검증
5. 코드 리뷰 (SOLID, 컨벤션, 확장성 체크)
6. 기획자용 Inspector 세팅 (SO, SerializeField)
7. 메인 씬 편입

### AI 협업 규칙

- 설계 완료 후 채팅으로 보고 → 승인 후 착수. 승인 없이 MCP 작업 시작 금지.
- 작업 완료 보고 전: read_console clear 후 재확인까지 완료.
- SOLID 원칙 준수: 자식 고유 상태는 자식 안에서만 처리. Base는 virtual/override 계약으로만 결합.

### 코드 리뷰 체크리스트

- [ ] 네임스페이스가 올바르게 지정되었는가?
- [ ] View에 로직이 없는가? (이동 계산 포함)
- [ ] Presenter가 IView Interface를 통해 View와 통신하는가?
- [ ] 이벤트 구독/해제가 OnEnable/OnDisable에 쌍으로 있는가?
- [ ] SerializeField에 [Header]로 Inspector 그룹이 지정되었는가?
- [ ] 공개 API에 XML 문서 주석이 있는가?
- [ ] Find(), SendMessage()를 사용하지 않았는가?
- [ ] SO 데이터는 읽기 전용 프로퍼티로만 외부에 노출하는가?
- [ ] 새 캐릭터 추가 시 기존 코드를 수정하지 않아도 되는가? (OCP)
- [ ] 자식 고유 상태가 Base에 노출되지 않는가? (DIP/OCP)

---

## 12. 변경 이력

| 버전 | 날짜 | 내용 |
|------|------|------|
| v0.1.0 | 2026-03-05 | 초안 작성. 기술 스택, 아키텍처, 컨벤션, 폴더 구조 확립 |
| v0.2.1 | 2026-03-05 | GuidelinesEditor.cs 추가. md 직접 수정 유틸 도입으로 cs 재생성 방식 제거 |
| v0.3.0 | 2026-03-05 | DI 전략, 테스트 전략, 데이터 설계 원칙 추가. 이벤트 통신 표에 ServiceLocator 항목 추가 |
| v0.4.0 | 2026-03-07 | 클리어 조건 추가. 조작 조건 수치 명세화 (Tap/Swipe/Drag/Hold 기준값 확정) |
| v0.5.0 | 2026-03-16 | 입력 유효 영역 전체 화면으로 변경. View 이동 로직 금지 명시(Presenter가 SetPosition 호출). 저스트 회피 트리거 API 갱신(TriggerJustDodge). AI 협업 규칙 추가. 코드 리뷰 체크리스트 갱신. 중복 섹션 정리. |

---

> 이 문서는 살아있는 문서입니다.
> 기획 변경, 기술 부채, 팀 합의에 따라 지속적으로 업데이트됩니다.
> 변경 시 반드시 변경 이력 섹션을 갱신하세요.
";
    }
}
