using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 시각 표현.
    /// 피격 플래시, 사망 페이드, Windup 예고 연출, 공격 범위 가시화를 담당한다.
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

        // ── Windup 색상 ───────────────────────────────────────────
        private static readonly Color WindupColor = new Color(1f, 0.2f, 0.2f);

        private void Awake()
        {
            _sr        = GetComponent<SpriteRenderer>();
            _baseColor = _sr != null ? _sr.color : Color.white;
        }

        private void Update()
        {
            // 피격 플래시
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _sr != null && !_isWindingUp)
                    _sr.color = _baseColor;
            }

            // Windup 색상 보간 (baseColor → WindupColor)
            if (_isWindingUp)
            {
                _windupTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_windupTimer / _windupDuration);
                if (_sr != null)
                    _sr.color = Color.Lerp(_baseColor, WindupColor, t);
            }
        }

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>Windup 예고 시작. 색상 변화 + 범위 오브젝트 표시.</summary>
        public void PlayWindup(float duration, float attackRange)
        {
            _isWindingUp    = true;
            _windupTimer    = 0f;
            _windupDuration = duration;

            ShowRangeIndicator(attackRange);
        }

        /// <summary>Windup 종료. 색상 복구 + 범위 오브젝트 숨김.</summary>
        public void StopWindup()
        {
            _isWindingUp = false;
            if (_sr != null) _sr.color = _baseColor;
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

            // attackRange는 반지름 → localScale 지름으로 변환
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
            _rangeSr.color        = new Color(1f, 0.3f, 0.3f, 0.25f); // 반투명 빨강
            _rangeSr.sortingOrder = -1; // 적 스프라이트 아래

            _rangeIndicator.SetActive(false);
        }

        /// <summary>런타임에서 단색 원형 텍스처를 동적 생성한다.</summary>
        private Sprite CreateCircleSprite()
        {
            const int size   = 128;
            var tex          = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels       = new Color32[size * size];
            float center     = size / 2f;
            float radius     = size / 2f;

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

            return Sprite.Create(tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size);
        }
    }
}
