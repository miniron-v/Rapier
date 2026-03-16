using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 타이탄 보스.
    ///
    /// [1페이즈] 느리지만 강력한 근접 공격 (베이스 AI 그대로).
    ///
    /// [2페이즈] 일반 공격과 돌진 공격을 교대로 수행.
    ///   돌진 시퀀스:
    ///     1. WindupCharge (예고): 발사 방향으로 선형 인디케이터 표시, _chargeWindupDuration 초 정지
    ///     2. Charge (돌진): 고정 방향으로 직선 이동. 플레이어 범위 진입 또는 스테이지 끝 도달 시 종료.
    ///     3. Groggy (그로기): _grogyDuration 초 정지. 이 구간은 공격 불가, Chase 차단.
    ///
    ///   [일반 공격 / 돌진 분리]
    ///     _isChargeSequence = true 동안 base.Update() 차단 → 일반 AI(Chase/Windup/Hit) 중단.
    ///     돌진 쿨타임(_chargeTimer)이 0 이하 → 즉시 돌진 시퀀스 진입 (일반 공격 대기 없음).
    /// </summary>
    public class TitanBossPresenter : BossPresenterBase
    {
        [Header("타이탄 — 2페이즈 돌진 설정")]
        [SerializeField] private float _chargeCooldown      = 5f;   // 돌진 쿨타임 (초)
        [SerializeField] private float _chargeWindupDuration = 0.7f; // 예고 시간 (인디케이터 표시)
        [SerializeField] private float _chargeSpeed         = 14f;  // 돌진 이동 속도
        [SerializeField] private float _chargeHitRange      = 1.8f; // 돌진 히트 판정 반경
        [SerializeField] private float _chargeDamageMultiplier = 2f; // 돌진 데미지 배율
        [SerializeField] private float _chargeMaxDistance   = 20f;  // 돌진 최대 이동 거리
        [SerializeField] private float _grogyDuration       = 2.5f; // 그로기 시간 (초)

        // ── 내부 상태 ─────────────────────────────────────────────
        private bool  _isChargeSequence; // true 동안 base.Update() 차단
        private float _chargeTimer;

        // ── 돌진 예고 인디케이터 ──────────────────────────────────
        private GameObject     _chargeIndicator;
        private SpriteRenderer _chargeIndicatorSr;

        // ── 2페이즈 진입 ──────────────────────────────────────────
        protected override void OnEnterPhase2()
        {
            // 진입 직후 일반 공격 한 사이클 후 첫 돌진
            _chargeTimer = _chargeCooldown * 0.5f;
            Debug.Log("[Titan] 2페이즈: 직선 돌진 활성화");
        }

        // ── Update: 돌진 쿨타임 관리 + base 교대 ─────────────────
        protected override void Update()
        {
            if (!IsAlive || _playerTransform == null) return;

            // 돌진 시퀀스 중엔 일반 AI 완전 중단
            if (_isChargeSequence) return;

            if (CurrentPhase == BossPhase.Phase2)
            {
                _chargeTimer -= Time.deltaTime;
                if (_chargeTimer <= 0f)
                {
                    _chargeTimer = _chargeCooldown;
                    StartCoroutine(ChargeSequence());
                    return;
                }
            }

            base.Update();
        }

        // ── 돌진 시퀀스 ──────────────────────────────────────────
private IEnumerator ChargeSequence()
        {
            if (_playerTransform == null) yield break;

            _isChargeSequence = true;

            // ① 돌진 방향 결정 + 벽까지 거리 계산
            var chargeDir = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;

            var stage = Game.Core.ServiceLocator.Get<Game.Core.StageBuilder>();
            float wallDist = stage != null
                ? stage.RaycastToWall(transform.position, chargeDir, _chargeMaxDistance)
                : _chargeMaxDistance;
            // 보스 반경만큼 여유 제거 (벽에 파묻히지 않도록)
            wallDist = Mathf.Max(0f, wallDist - 0.5f);

            // ② 예고: 인디케이터를 벽까지 표시
            ShowChargeIndicator(chargeDir, wallDist);
            Debug.Log($"[Titan] 돌진 예고! 방향:{chargeDir} 거리:{wallDist:F1}");
            yield return new WaitForSeconds(_chargeWindupDuration);
            HideChargeIndicator();

            // ③ 직선 돌진: 벽까지 고정 방향 이동
            Debug.Log("[Titan] 돌진!");
            float traveled  = 0f;
            bool  hitLanded = false;

            while (IsAlive && traveled < wallDist)
            {
                float step = _chargeSpeed * Time.deltaTime;
                float remaining = wallDist - traveled;
                step = Mathf.Min(step, remaining); // 벽 너머 이동 방지

                transform.position = (Vector2)transform.position + chargeDir * step;
                traveled += step;

                // 플레이어 히트 판정
                if (_playerTransform != null &&
                    Vector2.Distance(transform.position, _playerTransform.position) <= _chargeHitRange)
                {
                    hitLanded = true;
                    break;
                }

                yield return null;
            }

            // ④ 히트 데미지
            if (hitLanded && IsAlive && _playerTransform != null)
            {
                float damage = GetAttackPower() * _chargeDamageMultiplier;
                _playerDamageable?.TakeDamage(damage, chargeDir);
                Debug.Log($"[Titan] 돌진 히트! 데미지: {damage}");
            }

            // ⑤ 그로기
            Debug.Log("[Titan] 그로기!");
            yield return new WaitForSeconds(_grogyDuration);

            _isChargeSequence = false;
        }

        // ── 돌진 예고 인디케이터 ──────────────────────────────────
private void ShowChargeIndicator(Vector2 dir, float length)
        {
            if (_chargeIndicator == null)
                CreateChargeIndicator();

            float width  = _chargeHitRange * 2f;
            var center = (Vector2)transform.position + dir * (length * 0.5f);
            float angle = Vector2.SignedAngle(Vector2.up, dir);

            _chargeIndicator.transform.position   = new Vector3(center.x, center.y, 0f);
            _chargeIndicator.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
            _chargeIndicator.transform.localScale = new Vector3(width, length, 1f);
            _chargeIndicator.SetActive(true);
        }

        private void HideChargeIndicator()
        {
            if (_chargeIndicator != null)
                _chargeIndicator.SetActive(false);
        }

        private void CreateChargeIndicator()
        {
            _chargeIndicator = new GameObject("ChargeIndicator");
            _chargeIndicator.transform.SetParent(null);

            _chargeIndicatorSr              = _chargeIndicator.AddComponent<SpriteRenderer>();
            _chargeIndicatorSr.sprite       = CreateSquareSprite();
            _chargeIndicatorSr.color        = new Color(1f, 0.4f, 0f, 0.4f); // 주황 반투명
            _chargeIndicatorSr.sortingOrder = 5;
            _chargeIndicator.SetActive(false);
        }

        // ── Square Sprite (Base 메서드 재사용 불가 → 인라인) ─────
        private Sprite CreateSquareSprite()
        {
            const int size = 32;
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void OnDestroy()
        {
            if (_chargeIndicator != null)
                Destroy(_chargeIndicator);
        }
    }
}
