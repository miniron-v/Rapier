using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 시각 표현.
    /// 피격 플래시, 사망, Windup 예고 연출을 담당한다.
    ///
    /// [Windup 연출]
    ///   AttackIndicator 컴포넌트에 위임.
    ///   EnemyView는 Play/Stop 호출만 담당한다.
    /// </summary>
    public class EnemyView : MonoBehaviour
    {
        // ── 내부 참조 ─────────────────────────────────────────────
        private SpriteRenderer  _sr;
        private Color           _baseColor;
        private float           _flashTimer;
        private AttackIndicator _indicator;
        private const float     FLASH_DURATION = 0.1f;

        private void Awake()
        {
            _sr        = GetComponent<SpriteRenderer>();
            _baseColor = _sr != null ? _sr.color : Color.white;
            _indicator = GetComponent<AttackIndicator>();
            if (_indicator == null)
                _indicator = gameObject.AddComponent<AttackIndicator>();
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

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>Windup 예고 시작.</summary>
        public void PlayWindup(
            float                      duration,
            List<AttackIndicatorEntry> indicators,
            bool                       lockDirection,
            Func<Vector2>              getForward)
        {
            _indicator.Play(indicators, duration, lockDirection, getForward);
        }

        /// <summary>Windup 종료.</summary>
        public void StopWindup()
        {
            _indicator.Stop();
        }

        /// <summary>피격 시 흰색 플래시.</summary>
        public void PlayHit()
        {
            if (_sr == null) return;
            _sr.color   = Color.white;
            _flashTimer = FLASH_DURATION;
        }

        /// <summary>사망 시 즉시 비활성화.</summary>
        public void PlayDeath()
        {
            StopWindup();
            gameObject.SetActive(false);
        }

        /// <summary>풀에서 재사용될 때 색상 초기화.</summary>
        public void ResetVisual(Color baseColor)
        {
            _baseColor = baseColor;
            if (_sr != null) _sr.color = baseColor;
            _indicator.Stop();
        }
    }
}
