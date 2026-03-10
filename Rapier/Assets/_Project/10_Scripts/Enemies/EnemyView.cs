using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 시각 표현.
    /// 피격 플래시, 사망 페이드, Windup 예고 연출, 공격 범위 가시화를 담당한다.
    ///
    /// [Windup 연출]
    ///   본체 색상은 변화 없음.
    ///   범위 인디케이터: baseColor * 0.6f (어둡게) 로 고정.
    ///   인디케이터 알파: 0.5 → 1.0 으로 보간. 완전 불투명 시 공격 발동.
    /// </summary>
    public class EnemyView : MonoBehaviour
    {
        // ── 내부 참조 ─────────────────────────────────────────────
        private SpriteRenderer _sr;
        private Color          _baseColor;
        private float          _flashTimer;
        private const float    FLASH_DURATION = 0.1f;

        // ── Windup 상태 ───────────────────────────────────────────
        private bool  _isWindingUp;
        private float _windupTimer;
        private float _windupDuration;

        // ── 공격 범위 오브젝트 ────────────────────────────────────
        private GameObject     _rangeIndicator;
        private SpriteRenderer _rangeSr;

        private void Awake()
        {
            _sr        = GetComponent<SpriteRenderer>();
            _baseColor = _sr != null ? _sr.color : Color.white;
        }

        private void Update()
        {
            // 피격 플래시 복구
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _sr != null)
                    _sr.color = _baseColor;
            }

            // Windup: 인디케이터 알파 0.5 → 1.0 보간
            if (_isWindingUp && _rangeSr != null)
            {
                _windupTimer += Time.deltaTime;
                float t     = Mathf.Clamp01(_windupTimer / _windupDuration);
                float alpha = Mathf.Lerp(0.5f, 1.0f, t);

                var c   = _rangeSr.color;
                c.a     = alpha;
                _rangeSr.color = c;
            }
        }

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>Windup 예고 시작. 범위 인디케이터를 표시하고 알파 보간 시작.</summary>
        public void PlayWindup(float duration, float attackRange)
        {
            _isWindingUp    = true;
            _windupTimer    = 0f;
            _windupDuration = duration;

            ShowRangeIndicator(attackRange);

            // 인디케이터 색상: baseColor * 0.6 (어둡게), 알파 0.5 에서 시작
            if (_rangeSr != null)
            {
                Color dark = new Color(
                    _baseColor.r * 0.6f,
                    _baseColor.g * 0.6f,
                    _baseColor.b * 0.6f,
                    0.5f
                );
                _rangeSr.color = dark;
            }
        }

        /// <summary>Windup 종료. 범위 인디케이터 숨김.</summary>
        public void StopWindup()
        {
            _isWindingUp = false;
            HideRangeIndicator();
        }

        /// <summary>피격 시 흰색 플래시.</summary>
        public void PlayHit()
        {
            if (_sr == null) return;
            _sr.color   = Color.white;
            _flashTimer = FLASH_DURATION;
        }

        /// <summary>사망 시 즉시 비활성화 (WaveManager가 풀로 회수).</summary>
        public void PlayDeath()
        {
            StopWindup();
            gameObject.SetActive(false);
        }

        /// <summary>풀에서 재사용될 때 색상 초기화.</summary>
        public void ResetVisual(Color baseColor)
        {
            _baseColor   = baseColor;
            _isWindingUp = false;
            _windupTimer = 0f;
            if (_sr != null) _sr.color = baseColor;
            HideRangeIndicator();
        }

        // ── 공격 범위 스프라이트 ──────────────────────────────────

        private void ShowRangeIndicator(float attackRange)
        {
            if (_rangeIndicator == null)
                CreateRangeIndicator();

            float diameter = attackRange * 2f;
            _rangeIndicator.transform.localScale = new Vector3(diameter, diameter, 1f);
            _rangeIndicator.SetActive(true);
        }

        private void HideRangeIndicator()
        {
            if (_rangeIndicator != null)
                _rangeIndicator.SetActive(false);
        }

        private void CreateRangeIndicator()
        {
            _rangeIndicator = new GameObject("AttackRangeIndicator");
            _rangeIndicator.transform.SetParent(transform, false);
            _rangeIndicator.transform.localPosition = Vector3.zero;

            _rangeSr              = _rangeIndicator.AddComponent<SpriteRenderer>();
            _rangeSr.sprite       = CreateCircleSprite();
            _rangeSr.color        = new Color(1f, 1f, 1f, 0.5f); // 초기값, PlayWindup에서 덮어씀
            _rangeSr.sortingOrder = -1;

            _rangeIndicator.SetActive(false);
        }

        private Sprite CreateCircleSprite()
        {
            const int size = 128;
            var tex        = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels     = new Color32[size * size];
            float center   = size / 2f;
            float radius   = size / 2f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx   = x - center;
                float dy   = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = dist <= radius
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
