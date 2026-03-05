#if UNITY_EDITOR
// =============================================================
// GuidelinesSync.cs
// 역할: 스크립트 재컴파일 시 PROJECT_GUIDELINES.md 내용을
//       GuidelinesCreator.cs 의 캐시와 비교하여 불일치 시 자동 갱신합니다.
//
// 마커 구조 (GuidelinesCreator.cs 기준):
//   public static string GetCachedContent() => // <GUIDELINES_CACHE_START>
//   @"...내용..."; // <GUIDELINES_CACHE_END>
//
// 흐름:
//   md (Single Source of Truth)
//   → 재컴파일 시 md 읽기
//   → GuidelinesCreator.cs 캐시와 비교
//   → 불일치 시 GuidelinesCreator.cs 자동 갱신
// =============================================================
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Game.Core.Editor
{
    [InitializeOnLoad]
    public static class GuidelinesSync
    {
        private const string MD_PATH      = "Assets/_Project/00_Docs/PROJECT_GUIDELINES.md";
        private const string CREATOR_PATH = "Assets/_Project/00_Docs/Editor/GuidelinesCreator.cs";

        private const string CACHE_START_MARKER = "// <GUIDELINES_CACHE_START>";
        private const string CACHE_END_MARKER   = "// <GUIDELINES_CACHE_END>";

        static GuidelinesSync()
        {
            Sync();
        }

        private static void Sync()
        {
            string mdFullPath      = ToFullPath(MD_PATH);
            string creatorFullPath = ToFullPath(CREATOR_PATH);

            if (!File.Exists(mdFullPath))
            {
                Debug.LogWarning($"[GuidelinesSync] PROJECT_GUIDELINES.md 를 찾을 수 없습니다: {mdFullPath}");
                return;
            }

            if (!File.Exists(creatorFullPath))
            {
                Debug.LogWarning($"[GuidelinesSync] GuidelinesCreator.cs 를 찾을 수 없습니다: {creatorFullPath}");
                return;
            }

            string mdContent      = File.ReadAllText(mdFullPath, Encoding.UTF8);
            string creatorContent = File.ReadAllText(creatorFullPath, Encoding.UTF8);

            string cachedContent = ExtractCache(creatorContent);
            if (cachedContent == null)
            {
                Debug.LogWarning("[GuidelinesSync] GuidelinesCreator.cs 에서 캐시 마커를 찾을 수 없습니다.");
                return;
            }

            if (Normalize(cachedContent) == Normalize(mdContent))
            {
                Debug.Log("[GuidelinesSync] ✅ 지침서 동기화 상태 정상.");
                return;
            }

            string updatedCreator = ReplaceCache(creatorContent, mdContent);
            File.WriteAllText(creatorFullPath, updatedCreator, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log("[GuidelinesSync] 🔄 md 변경 감지 → GuidelinesCreator.cs 캐시 자동 갱신 완료.");
        }

        private static string ExtractCache(string creatorContent)
        {
            int startIdx = creatorContent.IndexOf(CACHE_START_MARKER);
            int endIdx   = creatorContent.IndexOf(CACHE_END_MARKER);

            if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
                return null;

            int atQuoteIdx = creatorContent.IndexOf("@\"", startIdx);
            if (atQuoteIdx < 0 || atQuoteIdx > endIdx)
                return null;

            int contentStart = atQuoteIdx + 2;
            int closingQuote = creatorContent.LastIndexOf("\";", endIdx);
            if (closingQuote < 0)
                return null;

            return creatorContent.Substring(contentStart, closingQuote - contentStart);
        }

        private static string ReplaceCache(string creatorContent, string newMdContent)
        {
            int startIdx = creatorContent.IndexOf(CACHE_START_MARKER);
            int endIdx   = creatorContent.IndexOf(CACHE_END_MARKER);

            if (startIdx < 0 || endIdx < 0)
                return creatorContent;

            int atQuoteIdx   = creatorContent.IndexOf("@\"", startIdx);
            int closingQuote = creatorContent.LastIndexOf("\";", endIdx);

            if (atQuoteIdx < 0 || closingQuote < 0)
                return creatorContent;

            string before = creatorContent.Substring(0, atQuoteIdx);
            string after  = creatorContent.Substring(closingQuote + 2);

            return before + "@\"" + newMdContent + "\";" + after;
        }

        private static string Normalize(string text)
            => text.Replace("\r\n", "\n").Trim();

        private static string ToFullPath(string assetPath)
            => Path.Combine(Application.dataPath.Replace("Assets", ""), assetPath);
    }
}
#endif
