using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 스펙터 보스.
    ///
    /// [1페이즈] 빠른 이동속도로 플레이어를 압박.
    /// [2페이즈] Windup 진입 시 플레이어 바로 옆으로 순간이동 후 공격.
    ///           순간이동은 _teleportCooldown 쿨타임으로 제한.
    /// </summary>
    public class SpecterBossPresenter : BossPresenterBase
    {
        [Header("스펙터 — 2페이즈 순간이동 설정")]
        [SerializeField] private float _teleportCooldown    = 3f;   // 순간이동 쿨타임 (초)
        [SerializeField] private float _teleportOffset      = 1.2f; // 플레이어 옆 거리
        [SerializeField] private float _teleportFadeTime    = 0.15f; // 페이드 연출 시간

        private float _teleportTimer;
        private bool  _canTeleport;

        // ── 2페이즈 진입 ──────────────────────────────────────────
        protected override void OnEnterPhase2()
        {
            _canTeleport  = true;
            _teleportTimer = 0f;
            Debug.Log("[Specter] 2페이즈: 순간이동 활성화");
        }

        // ── Update override: 텔레포트 쿨타임 갱신 ────────────────
        protected override void Update()
        {
            if (!IsAlive) return;

            if (CurrentPhase == BossPhase.Phase2 && !_canTeleport)
            {
                _teleportTimer -= Time.deltaTime;
                if (_teleportTimer <= 0f)
                    _canTeleport = true;
            }

            base.Update();
        }

        // ── Windup 진입 override: 순간이동 삽입 ──────────────────
        protected override void OnEnterWindup()
        {
            if (CurrentPhase == BossPhase.Phase2 && _canTeleport && _playerTransform != null)
            {
                _canTeleport   = false;
                _teleportTimer = _teleportCooldown;
                StartCoroutine(TeleportRoutine());
            }
        }

        // ── 순간이동 루틴 ─────────────────────────────────────────
        private IEnumerator TeleportRoutine()
        {
            if (_sr == null) yield break;

            // 페이드 아웃
            float elapsed = 0f;
            Color originalColor = _sr.color;
            while (elapsed < _teleportFadeTime)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, elapsed / _teleportFadeTime);
                _sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, a);
                yield return null;
            }

            // 순간이동: 플레이어 옆 랜덤 위치
            if (_playerTransform != null)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                var offset  = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _teleportOffset;
                transform.position = (Vector2)_playerTransform.position + offset;
                Debug.Log("[Specter] 순간이동!");
            }

            // 페이드 인
            elapsed = 0f;
            while (elapsed < _teleportFadeTime)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(0f, 1f, elapsed / _teleportFadeTime);
                _sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, a);
                yield return null;
            }

            _sr.color = originalColor;
        }
    }
}
