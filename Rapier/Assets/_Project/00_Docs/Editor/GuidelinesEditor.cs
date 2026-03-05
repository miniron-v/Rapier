#if UNITY_EDITOR
// =============================================================
// GuidelinesEditor.cs
// 역할: PROJECT_GUIDELINES.md 의 특정 섹션을 직접 수정하는 유틸입니다.
//
// 사용법 (MCP / 코드에서 호출):
//   GuidelinesEditor.UpdateSection("## 4. 폴더 구조", newContent);
//   GuidelinesEditor.AppendChangeLog("v0.3.0", "2026-03-05", "변경 내용");
//
// 흐름:
//   이 유틸이 md를 직접 수정
//   → 재컴파일 시 GuidelinesSync가 변경 감지
//   → GuidelinesCreator.cs 캐시 자동 갱신
// =============================================================
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Game.Core.Editor
{
    public static class GuidelinesEditor
    {
        private const string MD_PATH = "Assets/_Project/00_Docs/PROJECT_GUIDELINES.md";

        // -------------------------------------------------------
        // Public API
        // -------------------------------------------------------

        /// <summary>
        /// md 파일에서 지정한 섹션 헤더(## 로 시작)를 찾아
        /// 다음 ## 헤더 직전까지의 내용을 newContent 로 교체합니다.
        /// </summary>
        /// <param name="sectionHeader">교체할 섹션 헤더 (예: "## 4. 폴더 구조")</param>
        /// <param name="newContent">헤더 다음에 들어갈 새 내용 (헤더 줄 제외)</param>
        public static bool UpdateSection(string sectionHeader, string newContent)
        {
            string md = ReadMd();
            if (md == null) return false;

            // 섹션 헤더 위치 탐색
            int headerIdx = md.IndexOf("\n" + sectionHeader);
            if (headerIdx < 0)
            {
                Debug.LogWarning($"[GuidelinesEditor] 섹션을 찾을 수 없습니다: '{sectionHeader}'");
                return false;
            }

            // 헤더 줄 끝 위치
            int headerEnd = md.IndexOf("\n", headerIdx + 1);
            if (headerEnd < 0) headerEnd = md.Length;

            // 다음 ## 헤더 위치 (없으면 파일 끝)
            int nextHeaderIdx = FindNextSectionHeader(md, headerEnd + 1);
            int sectionEnd    = nextHeaderIdx >= 0 ? nextHeaderIdx : md.Length;

            // 교체
            string before  = md.Substring(0, headerEnd + 1);
            string after   = md.Substring(sectionEnd);
            string updated = before + "\n" + newContent.TrimEnd() + "\n\n" + after.TrimStart();

            return WriteMd(updated);
        }

        /// <summary>
        /// 변경 이력 테이블에 새 행을 추가합니다.
        /// </summary>
        /// <param name="version">버전 문자열 (예: "v0.3.0")</param>
        /// <param name="date">날짜 문자열 (예: "2026-03-05")</param>
        /// <param name="description">변경 내용 설명</param>
        public static bool AppendChangeLog(string version, string date, string description)
        {
            string md = ReadMd();
            if (md == null) return false;

            string newRow    = $"| {version} | {date} | {description} |";
            string marker    = "## 12. 변경 이력";
            int    markerIdx = md.IndexOf(marker);

            if (markerIdx < 0)
            {
                Debug.LogWarning("[GuidelinesEditor] '## 12. 변경 이력' 섹션을 찾을 수 없습니다.");
                return false;
            }

            // 마지막 | 로 시작하는 행 뒤에 삽입
            int lastRowEnd = md.LastIndexOf("\n|", md.IndexOf("\n---", markerIdx));
            if (lastRowEnd < 0)
            {
                Debug.LogWarning("[GuidelinesEditor] 변경 이력 테이블 마지막 행을 찾을 수 없습니다.");
                return false;
            }

            int insertAt = md.IndexOf("\n", lastRowEnd + 1);
            if (insertAt < 0) insertAt = md.Length;

            string updated = md.Substring(0, insertAt) + "\n" + newRow + md.Substring(insertAt);
            return WriteMd(updated);
        }

        /// <summary>
        /// md 파일 전체 내용을 직접 교체합니다. (전면 재작성 시 사용)
        /// </summary>
        public static bool RewriteAll(string newContent)
        {
            return WriteMd(newContent);
        }

        // -------------------------------------------------------
        // Private Helpers
        // -------------------------------------------------------

        private static string ReadMd()
        {
            string fullPath = ToFullPath(MD_PATH);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[GuidelinesEditor] md 파일을 찾을 수 없습니다: {fullPath}");
                return null;
            }
            return File.ReadAllText(fullPath, Encoding.UTF8);
        }

        private static bool WriteMd(string content)
        {
            string fullPath = ToFullPath(MD_PATH);
            try
            {
                File.WriteAllText(fullPath, content, Encoding.UTF8);
                AssetDatabase.Refresh();
                Debug.Log("[GuidelinesEditor] ✅ md 파일 수정 완료. 재컴파일 후 GuidelinesSync가 cs를 자동 갱신합니다.");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GuidelinesEditor] md 파일 쓰기 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// startIdx 이후에 등장하는 다음 ## 헤더의 시작 위치를 반환합니다.
        /// </summary>
        private static int FindNextSectionHeader(string text, int startIdx)
        {
            int idx = startIdx;
            while (idx < text.Length)
            {
                int newline = text.IndexOf("\n##", idx);
                if (newline < 0) return -1;
                return newline; // "\n##" 의 \n 위치 반환
            }
            return -1;
        }

        private static string ToFullPath(string assetPath)
            => Path.Combine(Application.dataPath.Replace("Assets", ""), assetPath);
    }
}
#endif
