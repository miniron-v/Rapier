using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 시각 표현. 피격 플래시, 사망 페이드를 담당한다.
    /// </summary>
    public class EnemyView : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Color          _baseColor;
        private float          _flashTimer;
        private const float    FLASH_DURATION = 0.1f;

        private void Awake()
        {
            _sr        = GetComponent<SpriteRenderer>();
            _baseColor = _sr != null ? _sr.color : Color.white;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _sr != null)
                    _sr.color = _baseColor;
            }
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
            gameObject.SetActive(false);
        }

        /// <summary>풀에서 재사용될 때 색상 초기화.</summary>
        public void ResetVisual(Color baseColor)
        {
            _baseColor = baseColor;
            if (_sr != null) _sr.color = baseColor;
        }
    }
}
