using System;
using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 순간이동 액션 (스펙터 전용).
    /// 히트 판정 없이 페이드아웃 → 플레이어 옆으로 순간이동 → 페이드인.
    /// 이후 시퀀서의 다음 공격(Melee 등)이 이어진다.
    /// </summary>
    [Serializable]
    public class TeleportAttackAction : EnemyAttackAction
    {
        [Tooltip("플레이어 옆 이동 거리")]
        public float teleportOffset  = 1.2f;
        [Tooltip("페이드 연출 시간 (초)")]
        public float fadeTime        = 0.15f;

        public override IEnumerator Execute(EnemyAttackContext ctx, Action onComplete)
        {
            if (ctx.SpriteRenderer == null || ctx.PlayerTransform == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            var   sr            = ctx.SpriteRenderer;
            Color originalColor = sr.color;

            // ── 페이드 아웃 ──────────────────────────────────────
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float a  = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, a);
                yield return null;
            }

            // ── 순간이동 ─────────────────────────────────────────
            float angle  = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            var   offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * teleportOffset;
            ctx.SelfTransform.position = (Vector2)ctx.PlayerTransform.position + offset;

            // ── 페이드 인 ────────────────────────────────────────
            elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float a  = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
                sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, a);
                yield return null;
            }
            sr.color = originalColor;

            onComplete?.Invoke();
        }
    }
}
