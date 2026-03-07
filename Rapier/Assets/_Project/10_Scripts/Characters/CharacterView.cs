using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 캐릭터 View 구현체.
    /// 스프라이트 이동 및 플레이스홀더 시각 표현만 담당한다.
    /// 로직 금지.
    /// </summary>
    public class CharacterView : MonoBehaviour, ICharacterView
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("이동")]
        [SerializeField] private float _moveSmoothing = 0.05f; // 위치 보간 강도

        // ── 내부 참조 ─────────────────────────────────────────────
        private SpriteRenderer _renderer;
        private Vector2        _targetPosition;
        private Color          _originalColor;

        // ── 라이프사이클 ─────────────────────────────────────────
        private void Awake()
        {
            _renderer      = GetComponent<SpriteRenderer>();
            _targetPosition = transform.position;

            if (_renderer != null)
                _originalColor = _renderer.color;
        }

        private void Update()
        {
            // 부드러운 이동 보간
            transform.position = Vector2.Lerp(
                transform.position,
                _targetPosition,
                1f - Mathf.Pow(_moveSmoothing, Time.deltaTime));
        }

        // ── ICharacterView 구현 ───────────────────────────────────
        public void MoveTo(Vector2 position)
        {
            _targetPosition = position;
        }

        public void PlayAttack()
        {
            // 플레이스홀더: 색상 플래시
            StopAllCoroutines();
            StartCoroutine(FlashColor(Color.red, 0.1f));
        }

        public void PlayDodge(Vector2 direction)
        {
            // 플레이스홀더: 색상 플래시
            StopAllCoroutines();
            StartCoroutine(FlashColor(Color.cyan, 0.15f));
        }

        public void UpdateChargeGauge(float ratio)
        {
            // 플레이스홀더: 차지량에 따라 색상 변화 (흰색 → 노란색)
            if (_renderer != null)
                _renderer.color = Color.Lerp(_originalColor, Color.yellow, ratio);
        }

        public void UpdateHpGauge(float ratio)
        {
            // Phase 5 HUD 연결 전까지 로그로만 확인
            // Debug.Log($"[CharacterView] HP: {ratio:P0}");
        }

        public void PlayDeath()
        {
            // 플레이스홀더: 투명하게
            if (_renderer != null)
            {
                var c = _renderer.color;
                c.a = 0.3f;
                _renderer.color = c;
            }
            gameObject.SetActive(false);
        }

        // ── 내부 유틸 ─────────────────────────────────────────────
        private System.Collections.IEnumerator FlashColor(Color flashColor, float duration)
        {
            if (_renderer == null) yield break;
            _renderer.color = flashColor;
            yield return new WaitForSeconds(duration);
            _renderer.color = _originalColor;
        }
    }
}
