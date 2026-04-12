using System;
using System.Collections;
using UnityEngine;
using Game.Enemies;

namespace Game.Characters.Assassin
{
    /// <summary>
    /// 어새신 잔상(Phantom) 개별 오브젝트 컨트롤러.
    ///
    /// [생명주기]
    ///   Init(duration, damagePercent) 호출 → 수명 타이머 시작.
    ///   수명 마지막 0.5초: alpha 선형 감소 페이드 아웃.
    ///   수명 만료 → OnDeath 이벤트 발행 → Destroy(gameObject).
    ///
    /// [공격 동참]
    ///   AttackWithPlayer(targetDir, baseDamage): 잔상 위치에서 같은 방향으로 공격 판정.
    ///   데미지 = baseDamage × (damagePercent / 100).
    ///   단, 수명이 만료되었거나 페이드 아웃 중이면 공격하지 않는다.
    ///
    /// [시각]
    ///   CharacterView의 SpriteRenderer를 복제하여 alpha 0.5 반투명 적용.
    ///   페이드 아웃 구간에서 alpha 선형 감소.
    /// </summary>
    public class PhantomController : MonoBehaviour
    {
        // ── 상수 ──────────────────────────────────────────────────
        private const float FADE_DURATION        = 0.5f;
        private const float PHANTOM_ALPHA        = 0.5f;
        private const float ATTACK_BOX_WIDTH     = 2.0f;
        private const float ATTACK_BOX_HEIGHT    = 1.5f;
        private const float ATTACK_BOX_OFFSET    = 1.0f;

        // ── 직렬화 필드 (없음 — 모두 Init 주입) ──────────────────

        // ── 비직렬화 런타임 필드 ──────────────────────────────────
        [NonSerialized] private float          _remainingTime;
        [NonSerialized] private float          _damagePercent;
        [NonSerialized] private bool           _isExpired;
        [NonSerialized] private SpriteRenderer _sr;
        [NonSerialized] private Color          _baseColor;
        [NonSerialized] private Coroutine      _lifetimeCoroutine;

        // ── 이벤트 ────────────────────────────────────────────────
        /// <summary>수명 만료 직전 발행. AssassinPresenter가 구독해 리스트에서 제거.</summary>
        public event Action<PhantomController> OnPhantomExpired;

        // ── 초기화 ────────────────────────────────────────────────

        /// <summary>
        /// 잔상을 초기화하고 수명 타이머를 시작한다.
        /// </summary>
        /// <param name="sourceSr">복제할 원본 SpriteRenderer (캐릭터 본체)</param>
        /// <param name="duration">잔상 지속 시간 (초)</param>
        /// <param name="damagePercent">공격 동참 데미지 배율 (% 정수, 50 = ATK×0.5)</param>
        public void Init(SpriteRenderer sourceSr, float duration, float damagePercent)
        {
            _damagePercent = damagePercent;
            _isExpired     = false;

            // SpriteRenderer 설정 — 반투명 복제
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null)
                _sr = gameObject.AddComponent<SpriteRenderer>();

            if (sourceSr != null)
            {
                _sr.sprite       = sourceSr.sprite;
                _sr.sortingOrder = sourceSr.sortingOrder - 1; // 본체보다 뒤에 렌더링
                _sr.flipX        = sourceSr.flipX;
                _sr.flipY        = sourceSr.flipY;
            }

            _baseColor   = new Color(0.6f, 0.6f, 1.0f, PHANTOM_ALPHA); // 청보라 반투명
            _sr.color    = _baseColor;

            _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(duration));
        }

        // ── 공격 동참 ─────────────────────────────────────────────

        /// <summary>
        /// 본체 Tap 공격 시 잔상이 동참하는 메서드.
        /// 잔상 위치에서 targetDir 방향으로 박스 히트 판정을 수행한다.
        /// </summary>
        /// <param name="targetDir">공격 방향 (정규화된 Vector2)</param>
        /// <param name="baseDamage">본체 기준 공격력 (ATK × normalAttackPercent/100 이전 값)</param>
        public void AttackWithPlayer(Vector2 targetDir, float baseDamage)
        {
            if (_isExpired) return;

            float damage     = baseDamage * (_damagePercent / 100f);
            var   boxCenter  = (Vector2)transform.position + targetDir * ATTACK_BOX_OFFSET;
            var   boxSize    = new Vector2(ATTACK_BOX_WIDTH, ATTACK_BOX_HEIGHT);
            float angle      = Vector2.SignedAngle(Vector2.up, targetDir);
            int   enemyLayer = LayerMask.GetMask("Enemy");

            var hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, enemyLayer);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.TakeDamage(damage, targetDir);
            }
        }

        // ── 수명 코루틴 ───────────────────────────────────────────
        private IEnumerator LifetimeRoutine(float duration)
        {
            float fadeStartTime = duration - FADE_DURATION;
            float elapsed       = 0f;

            // 페이드 시작 전 대기
            while (elapsed < fadeStartTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 페이드 아웃 구간
            float fadeElapsed = 0f;
            while (fadeElapsed < FADE_DURATION)
            {
                fadeElapsed  += Time.deltaTime;
                float ratio   = Mathf.Clamp01(fadeElapsed / FADE_DURATION);
                var   col     = _baseColor;
                col.a         = Mathf.Lerp(PHANTOM_ALPHA, 0f, ratio);
                if (_sr != null) _sr.color = col;
                yield return null;
            }

            Expire();
        }

        // ── 수명 만료 처리 ────────────────────────────────────────
        private void Expire()
        {
            if (_isExpired) return;
            _isExpired = true;

            OnPhantomExpired?.Invoke(this);
            Destroy(gameObject);
        }

        // ── 외부 강제 제거 ────────────────────────────────────────

        /// <summary>
        /// AssassinPresenter가 잔상을 강제로 제거할 때 호출한다 (사망/씬 전환 등).
        /// 이벤트를 발행하지 않고 오브젝트만 파괴한다 (호출자가 이미 리스트를 직접 정리함).
        /// </summary>
        public void ForceDestroy()
        {
            _isExpired = true;
            if (_lifetimeCoroutine != null)
            {
                StopCoroutine(_lifetimeCoroutine);
                _lifetimeCoroutine = null;
            }
            Destroy(gameObject);
        }
    }
}
