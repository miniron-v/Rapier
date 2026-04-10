using UnityEngine;

namespace Game.Characters
{
    /// <summary>
    /// 캐릭터 View 구현체.
    /// 시각 연출(색상 플래시, 스프라이트 설정 등)만 담당한다.
    ///
    /// [이동 책임]
    ///   SetPosition()으로 transform.position을 즉시 설정한다.
    ///   Lerp/스무딩 없음 — 부드러운 이동은 Presenter가 매 프레임 작은 delta로 SetPosition()을 호출해 구현한다.
    ///   Update()에 이동 로직 없음.
    /// </summary>
    public class CharacterView : MonoBehaviour, ICharacterView
    {
        // ── 내부 참조 ─────────────────────────────────────────────
        private SpriteRenderer _renderer;
        private Color          _originalColor;

        // ── 라이프사이클 ─────────────────────────────────────────
        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null)
                _originalColor = _renderer.color;
        }

        // ── ICharacterView 구현 ───────────────────────────────────

        /// <summary>
        /// Presenter가 계산한 위치를 즉시 반영한다.
        /// Lerp 없음 — 이동 보간은 Presenter 책임.
        /// </summary>
        public void SetPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        public void SetSprite(Sprite sprite)
        {
            if (_renderer != null && sprite != null)
                _renderer.sprite = sprite;
        }

        public void PlayAttack()
        {
            StopAllCoroutines();
            StartCoroutine(FlashColor(Color.red, 0.1f));
        }

        public void PlayHit()
        {
            StopAllCoroutines();
            StartCoroutine(FlashColor(Color.red, 0.1f));
        }

        public void PlayDodge(Vector2 direction)
        {
            StopAllCoroutines();
            StartCoroutine(FlashColor(Color.cyan, 0.15f));
        }

        public void UpdateChargeGauge(float ratio)
        {
            if (_renderer != null)
                _renderer.color = Color.Lerp(_originalColor, Color.yellow, ratio);
        }

        public void UpdateHpGauge(float ratio)
        {
            // Phase 5 HUD에서 처리. View 레벨 연출 필요 시 여기서 추가.
        }

        public void PlayDeath()
        {
            if (_renderer != null)
            {
                var c = _renderer.color;
                c.a = 0.3f;
                _renderer.color = c;
            }
            gameObject.SetActive(false);
        }

        public void PlayRevive()
        {
            gameObject.SetActive(true);
            if (_renderer != null)
            {
                var c = _renderer.color;
                c.a             = 1f;
                _renderer.color = c;
            }
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
