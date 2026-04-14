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
    ///   AttackWithPlayer(targetPos, baseDamage): 잔상 위치에서 타겟 방향으로 박스 히트 판정.
    ///   AttackNearestEnemy(baseDamage): 잔상 반경 내 가장 가까운 적을 탐색해 AttackWithPlayer 호출.
    ///   ExecuteAoe(damage): 잔상 위치에서 360도 원형 광역 공격 (차지 스킬 동참).
    ///   데미지 = baseDamage × (damagePercent / 100).
    ///   수명 만료(_isExpired=true)이면 모든 공격 메서드에서 조기 리턴.
    ///
    /// [시각 피드백]
    ///   공격 시 ShowAttackFlash()로 0.08초간 Color.white 플래시.
    ///
    /// [시각]
    ///   CharacterView의 SpriteRenderer를 복제하여 alpha 0.5 반투명 적용.
    ///   페이드 아웃 구간에서 alpha 선형 감소.
    /// </summary>
    public class PhantomController : MonoBehaviour
    {
        // ── 상수 ──────────────────────────────────────────────────
        private const float FADE_DURATION         = 0.5f;
        private const float PHANTOM_ALPHA         = 0.5f;
        private const float ATTACK_BOX_WIDTH      = 2.0f;
        private const float ATTACK_BOX_HEIGHT     = 1.5f;
        private const float ATTACK_BOX_OFFSET     = 1.0f;
        private const float ATTACK_RANGE          = 3.0f;
        private const float ATTACK_FLASH_DURATION = 0.08f;
        private const float INDICATOR_DURATION    = 0.15f;

        // ── 직렬화 필드 ────────────────────────────────────────────
        [Tooltip("잔상 일반 공격 인디케이터에 쓸 사각형 스프라이트. 미할당 시 런타임 생성.")]
        [SerializeField] private Sprite _attackRangeSprite;

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

            _baseColor   = new Color(0.7f, 0.5f, 1.0f, PHANTOM_ALPHA); // 연보라 반투명 (포탈 진보라와 구분)
            _sr.color    = _baseColor;

            _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(duration));
        }

        // ── 공격 동참 ─────────────────────────────────────────────

        /// <summary>
        /// 본체 Tap 공격 시 잔상이 동참하는 메서드.
        /// 잔상 자신의 위치에서 targetPos 방향으로 방향을 재계산하여 박스 히트 판정을 수행한다.
        /// 본체→적 방향이 아니라 잔상→적 방향을 사용하므로 기하학적으로 정확하다.
        /// </summary>
        /// <param name="targetPos">공격 대상의 월드 좌표</param>
        /// <param name="baseDamage">본체 기준 공격력 (ATK × normalAttackPercent/100 이전 값)</param>
        public void AttackWithPlayer(Vector2 targetPos, float baseDamage)
        {
            if (_isExpired) return;

            // 잔상 위치에서 타겟 방향을 재계산 — 본체→타겟 방향과 다를 수 있음
            Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
            if (dir == Vector2.zero) dir = Vector2.up;

            // 잔상 일반 공격 범위 인디케이터 — 판정 직전 표시
            ShowBoxIndicator(dir);

            float damage     = baseDamage * (_damagePercent / 100f);
            var   boxCenter  = (Vector2)transform.position + dir * ATTACK_BOX_OFFSET;
            var   boxSize    = new Vector2(ATTACK_BOX_WIDTH, ATTACK_BOX_HEIGHT);
            float angle      = Vector2.SignedAngle(Vector2.up, dir);
            int   enemyLayer = LayerMask.GetMask("Enemy");

            var hits     = Physics2D.OverlapBoxAll(boxCenter, boxSize, angle, enemyLayer);
            int hitCount = 0;
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;
                enemy.TakeDamage(damage, dir);
                hitCount++;
            }

            ShowAttackFlash();
            Debug.Log($"[PhantomController] 동참 공격 @ {transform.position} → 타겟 {targetPos}, 히트: {hitCount}");
        }

        /// <summary>
        /// 잔상 위치 기준 반경 내 가장 가까운 적을 탐색해 공격한다.
        /// 적이 없으면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="baseDamage">본체 기준 공격력 (damagePercent는 내부 적용)</param>
        public void AttackNearestEnemy(float baseDamage)
        {
            if (_isExpired) return;

            int   enemyLayer = LayerMask.GetMask("Enemy");
            var   hits       = Physics2D.OverlapCircleAll(transform.position, ATTACK_RANGE, enemyLayer);

            EnemyPresenterBase nearest      = null;
            float              nearestDistSq = float.MaxValue;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;
                float distSq = ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest       = enemy;
                }
            }

            if (nearest == null) return;

            AttackWithPlayer((Vector2)nearest.transform.position, baseDamage);
        }

        /// <summary>
        /// 잔상 위치에서 360도 원형 광역 공격을 수행한다. 차지 스킬 동참에 사용한다.
        /// </summary>
        /// <param name="damage">최종 데미지 (호출자에서 계산 완료된 값)</param>
        public void ExecuteAoe(float damage)
        {
            if (_isExpired) return;

            // 잔상 AoE 범위 인디케이터 — 판정 직전 표시
            ShowCircleIndicator(ATTACK_RANGE);

            int   enemyLayer = LayerMask.GetMask("Enemy");
            var   hits       = Physics2D.OverlapCircleAll(transform.position, ATTACK_RANGE, enemyLayer);
            int   hitCount   = 0;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;
                var dir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.TakeDamage(damage, dir);
                hitCount++;
            }

            ShowAttackFlash();
            Debug.Log($"[PhantomController] AoE 동참 @ {transform.position}, 히트: {hitCount}");
        }

        // ── 인디케이터 ────────────────────────────────────────────

        /// <summary>
        /// 잔상 일반 공격용 사각형 인디케이터를 생성하고 INDICATOR_DURATION 후 자동 소멸한다.
        /// _attackRangeSprite 미할당 시 런타임 생성 사각형 스프라이트로 대체.
        /// </summary>
        private void ShowBoxIndicator(Vector2 dir)
        {
            var   boxCenter = (Vector2)transform.position + dir * ATTACK_BOX_OFFSET;
            float angle     = Vector2.SignedAngle(Vector2.up, dir);

            var go = new GameObject("PhantomBoxIndicator");
            go.transform.position   = new Vector3(boxCenter.x, boxCenter.y, 0f);
            go.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(ATTACK_BOX_WIDTH, ATTACK_BOX_HEIGHT, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _attackRangeSprite != null ? _attackRangeSprite : CreateSquareSprite();
            sr.color        = new Color(0.7f, 0.5f, 1f, 0.25f); // 잔상 색조 (연보라)
            sr.sortingOrder = 10;

            Destroy(go, INDICATOR_DURATION);
        }

        /// <summary>
        /// 잔상 AoE 공격용 원형 인디케이터를 생성하고 INDICATOR_DURATION 후 자동 소멸한다.
        /// </summary>
        private void ShowCircleIndicator(float radius)
        {
            var go = new GameObject("PhantomCircleIndicator");
            go.transform.position   = transform.position;
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateCircleSprite(64);
            sr.color        = new Color(0.7f, 0.5f, 1f, 0.25f); // 잔상 색조 (연보라)
            sr.sortingOrder = 10;

            Destroy(go, INDICATOR_DURATION);
        }

        /// <summary>
        /// 흰색 사각형 스프라이트를 런타임에 생성한다 (_attackRangeSprite 미할당 폴백).
        /// </summary>
        private static Sprite CreateSquareSprite()
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

        /// <summary>
        /// 흰색 원형 스프라이트를 런타임에 생성한다 (_circleIndicatorSprite 미할당 폴백).
        /// </summary>
        private static Sprite CreateCircleSprite(int size)
        {
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float cx   = size * 0.5f - 0.5f;
            float cy   = size * 0.5f - 0.5f;
            float rSq  = (size * 0.5f) * (size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx, dy = y - cy;
                pixels[y * size + x] = (dx * dx + dy * dy) <= rSq
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ── 공격 시각 피드백 ──────────────────────────────────────
        private void ShowAttackFlash()
        {
            if (_sr == null) return;
            StartCoroutine(AttackFlashRoutine());
        }

        private IEnumerator AttackFlashRoutine()
        {
            Color original = _sr.color;
            _sr.color = Color.white;
            yield return new WaitForSecondsRealtime(ATTACK_FLASH_DURATION);
            if (_sr != null)
                _sr.color = original;
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
