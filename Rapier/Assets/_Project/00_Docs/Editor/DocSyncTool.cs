// =============================================================
// DocSyncTool.cs  —  문서 동기화 도구 (MD → DOCX)
// =============================================================
// 메뉴: Rapier/Docs/Sync to DOCX
//       Rapier/Docs/Create DesignDoc MD (최초 1회)
//
// 운영 방식:
//   1. .md 파일이 원본 (Claude가 MCP로 편집)
//   2. 이 스크립트로 Pandoc 호출 → .docx 자동 생성
//   3. 팀원은 생성된 .docx를 열람
// =============================================================

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;

public class DocSyncTool : EditorWindow
{
    private const string DocsPath = "Assets/_Project/00_Docs";

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
            UnityEngine.Debug.LogWarning("[DocSync] PROJECT_GUIDELINES.md not found.");
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

    [MenuItem("Rapier/Docs/Create DesignDoc MD")]
    public static void CreateDesignDocMd()
    {
        string path = Path.GetFullPath(Path.Combine(DocsPath, "Rapier_Prototype_DesignDoc.md"));

        if (File.Exists(path))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "DocSync", "이미 파일이 존재합니다. 덮어쓰시겠습니까?", "덮어쓰기", "취소");
            if (!overwrite) return;
        }

        File.WriteAllText(path, GetDesignDocTemplate(), System.Text.Encoding.UTF8);
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("[DocSync] DesignDoc MD 생성 완료: " + path);
        EditorUtility.DisplayDialog("DocSync", "Rapier_Prototype_DesignDoc.md 생성 완료!", "확인");
    }

    private static void ConvertWithPandoc(string inputMd, string outputDocx)
    {
        string fileName = Path.GetFileName(inputMd);

        // Unity 에디터가 시스템 PATH를 못 읽는 경우를 대비해 주요 경로를 직접 탐색
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
                    FileName = candidate,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var testProc = Process.Start(test);
                testProc.WaitForExit();
                if (testProc.ExitCode == 0)
                {
                    pandocPath = candidate;
                    break;
                }
            }
            catch
            {
                // 해당 경로 없음, 다음 후보 시도
            }
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
                FileName = pandocPath,
                Arguments = "\"" + inputMd + "\" -o \"" + outputDocx + "\" --from markdown --to docx",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
                UnityEngine.Debug.Log("[DocSync] 변환 성공: " + fileName + " -> " + Path.GetFileName(outputDocx));
            else
                UnityEngine.Debug.LogError("[DocSync] Pandoc 오류 (" + fileName + "): " + stderr);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("[DocSync] Pandoc 실행 실패.\n" + e.Message);
        }
    }

    private static string GetDesignDocTemplate()
    {
        return
@"# RAPIER 프로토타입 기획서
**Prototype Design Document v1.2.0**
2026-03-07

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 장르 | 싱글 플레이 실시간 액션 RPG |
| 플랫폼 | 모바일 (Android / iOS), PC 테스트 지원 |
| 화면 방향 | 세로 모드 (Portrait) |
| 렌더 파이프라인 | URP 2D |
| 조작 | 화면 하단 40% 영역, 단일 손가락(엄지) / PC 마우스 지원 |
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

입력 유효 영역: 화면 하단 40%. 단일 손가락(엄지) 기준. PC에서는 마우스로 에뮬레이션.

### 3-1. 기본 입력

| 입력 | 상태 | 조건 | 설명 |
|------|------|------|------|
| Drag | Move | 이동 거리 ≥ 20px, 지속 ≥ 0.3초 | 가상 조이스틱과 함께 이동. 공격 판정 없음 |
| Tap | Attack | 이동 거리 < 20px, 지속 < 0.2초 | 전방 좁은 범위 광역 공격. 0.5초 딜레이 |
| Swipe | Dodge | 이동 거리 ≥ 20px, 지속 < 0.3초 | 방향 회피. 무적 0.2초. 쿨다운 없음 |
| Hold → Release | Charge → Skill | 정지 상태, 지속 ≥ 0.3초 | 홀드 중 게이지 충전, 완충 후 손을 떼면 차지 스킬 발동 |

### 3-2. 저스트 회피 시스템

저스트 회피: 적의 공격 히트박스가 활성화되는 순간에 정확히 Swipe 입력 시 발동.

| 단계 | 설명 |
|------|------|
| 발동 조건 | 적 공격 타이밍에 정확히 Swipe 입력 (일반 회피와 동일한 입력, 타이밍으로 구분) |
| 슬로우 모션 | 저스트 회피 성공 시 일정 시간 게임 속도 감소 (반응 기회 부여) |
| 슬로우 중 Hold | 캐릭터 고유 스킬 즉시 발동 |

---

## 4. 캐릭터 설계

### 4-1. 공통 스탯 (플레이어)

| 항목 | 수치 | 비고 |
|------|------|------|
| 최대 HP | 500 | 일반 적 10회, 보스 일반 공격 약 6회에 사망 |
| 기본 공격력 | 50 (좁은 범위 광역) | 일반 적 5타, 보스 40타 처치 기준 |
| 이동속도 | 5 | 화면 절반을 약 1초에 이동 |
| 회피 무적 시간 | 0.2초 | 모션 종료 즉시 재사용 가능. 저스트 회피는 타이밍으로 판정 |

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

- 저스트 회피 후 슬로우 중 Hold (고유 스킬): 자신을 공격한 적에게 대시 → 표식 부여 + 데미지 → 원위치 복귀. 표식 최대 5중첩
- 차지 스킬 (Hold → Release): 표식이 있는 모든 적을 고속 연속 관통 공격. 각 적은 보유한 표식 중첩 수만큼 피해를 받음

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
| 플레이어 | 500 | 50 (좁은 범위 광역) | 5.0 | 회피 무적 0.2초, 재사용 즉시 |
| 일반 적 | 250 | 50 / 주기 1.5초 | 2.5 | 분산 접근 AI |
| 보스 (일반) | 2000 | 80 / 주기 2초 | 1.5 | 3회 후 광역 |
| 보스 (광역 스킬) | - | 120 | - | 3회 일반 후 1회 |
| 레이피어 차지 스킬 | - | 차지 계수 × 표식 중첩 수 | - | 각 적마다 보유 표식 중첩 수만큼 피해 |

---

## 7. 구현 우선순위 (프로토타입 단계)

| Phase | 내용 | 완료 기준 |
|-------|------|-----------|
| Phase 1 | ServiceLocator / InputState / GestureRecognizer | 입력 4종 판별 및 저스트 회피 타이밍 감지 동작 |
| Phase 2 | 캐릭터 기반 클래스 (Interface, Model, PresenterBase, View) | 플레이스홀더 스프라이트로 MVP 구조 동작 |
| Phase 3 | 플레이어 이동 + 카메라 추적 | Drag → 이동, 화면 추적 확인 |
| Phase 4 | 전투 기반 (CombatSystem, 일반 적, 웨이브 매니저) | 피격 판정, HP 감소, 10초 자동 웨이브 확인 |
| Phase 5 | UI 연결 (HP바, 차지 게이지, Debug 패널) | 전체 루프 플레이 가능 |
| Phase 6 | 캐릭터 고유 메커니즘 1종 + 저스트 회피 슬로우 구현 | 핵심 재미 요소 확인 |

---

## 8. 변경 이력

| 버전 | 날짜 | 내용 |
|------|------|------|
| v1.0.0 | 2026-03-07 | 최초 작성. 웨이브/보스/캐릭터/밸런스 수치 확정 |
| v1.1.0 | 2026-03-07 | 저스트 회피 시스템 명세 추가. 강화 스킬 발동 조건 명확화. 웨이브 전환 방식 수정 (10초 자동 추가 등장) |
| v1.2.0 | 2026-03-10 | 저스트 회피 슬로우 중 Hold를 캐릭터 고유 스킬로 분리. 암살자 잔상 발동 조건을 저스트 회피로 변경 및 잔상 동참 범위 명확화. 레이피어 고유 스킬 및 차지 스킬 전면 재기술. 4-2 캐릭터 메커니즘 기술 방식 개편 (기본 시스템과의 차이점 중심). 파일명에서 버전 번호 제거. |
";
    }
}
