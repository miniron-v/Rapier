using System;
using System.Collections;
using UnityEngine;
using Game.Enemies;

namespace Game.Characters.Ranger
{
    /// <summary>
    /// 레인저 지뢰.
    ///
    /// [배치 / 수명]
    ///   RangerPresenter.OnSwipe 에서 Instantiate 후 Init() 으로 목적지 주입.
    ///   throwSpeed unit/s 로 목적지까지 직선 이동.
    ///   생성 직후부터 lifetime 타이머 시작. 수명 만료 시 자동 폭발.
    ///
    /// [충돌 감지]
    ///   OnTriggerEnter2D 미사용 (적 프리팹에 Rigidbody2D 없음).
    ///   매 프레임 Physics2D.OverlapCircleAll 으로 직접 폴링.
    ///   이동 중/착지 후 구분 없이 항상 감지.
    ///
    /// [폭발]
    ///   반경 radius 내 모든 Enemy 에게 데미지.
    ///   폭발 후 원형 인디케이터 0.3초 표시 → Destroy(self).
    ///
    /// [큐 관리]
    ///   RangerPresenter 가 Queue{RangerMine} 으로 관리.
    ///   소멸 이벤트 OnMineDestroyed 를 발행해 Presenter 가 큐에서 제거할 수 있도록 한다.
    /// </summary>
    public class RangerMine : MonoBehaviour
    {
        /// <summary>지뢰가 소멸(폭발 or 수명 만료)할 때 발행. Presenter 큐 동기화용.</summary>
        public event Action<RangerMine> OnMineDestroyed;

        // ── 파라미터 ──────────────────────────────────────────────────────
        [NonSerialized] private float   _damage;
        [NonSerialized] private float   _radius;
        [NonSerialized] private float   _lifetime;
        [NonSerialized] private Vector2 _destination;
        [NonSerialized] private float   _throwSpeed;

        // ── 내부 상태 ─────────────────────────────────────────────────────
        [NonSerialized] private float _timer;
        [NonSerialized] private bool  _exploded;
        [NonSerialized] private int   _enemyLayerMask;

        // ── 상수 ─────────────────────────────────────────────────────────
        private const float INDICATOR_DURATION  = 0.3f;
        private const float LAND_EPSILON        = 0.05f;
        /// <summary>지뢰 본체 접촉 감지 반경. 폭발 반경(_radius)과 무관한 고정값.</summary>
        private const float CONTACT_RADIUS      = 0.25f;

        // ── 초기화 ────────────────────────────────────────────────────────

        /// <summary>
        /// 지뢰 파라미터를 주입하고 목적지로 이동을 시작한다.
        /// </summary>
        /// <param name="damage">폭발 데미지 (최종값)</param>
        /// <param name="radius">폭발/감지 반경 (unit)</param>
        /// <param name="lifetime">수명 (초). 생성 시점부터 카운트.</param>
        /// <param name="destination">착지 목적지 (월드 좌표)</param>
        /// <param name="throwSpeed">이동 속도 (unit/s)</param>
        public void Init(float damage, float radius, float lifetime,
                         Vector2 destination, float throwSpeed)
        {
            _damage         = damage;
            _radius         = radius;
            _lifetime       = lifetime;
            _destination    = destination;
            _throwSpeed     = throwSpeed;
            _timer          = 0f;
            _exploded       = false;
            _enemyLayerMask = LayerMask.GetMask("Enemy");
        }

        // ── Unity 라이프사이클 ────────────────────────────────────────────

        private void Update()
        {
            if (_exploded) return;

            // 목적지로 이동
            Vector2 current = transform.position;
            if (Vector2.Distance(current, _destination) > LAND_EPSILON)
            {
                Vector2 next = Vector2.MoveTowards(current, _destination, _throwSpeed * Time.deltaTime);
                transform.position = new Vector3(next.x, next.y, transform.position.z);
            }

            // 수명 카운트
            _timer += Time.deltaTime;
            if (_timer >= _lifetime)
            {
                Explode();
                return;
            }

            // 적 접촉 감지 — 지뢰 본체 크기(CONTACT_RADIUS) 기준, 폭발 반경과 무관
            var hits = Physics2D.OverlapCircleAll(transform.position, CONTACT_RADIUS, _enemyLayerMask);
            foreach (var hit in hits)
            {
                if (hit.GetComponent<EnemyPresenterBase>() != null)
                {
                    Explode();
                    return;
                }
            }
        }

        // ── 폭발 로직 ────────────────────────────────────────────────────

        /// <summary>폭발. 적 접촉 및 수명 만료 모두 이 경로로 진입.</summary>
        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            DealAreaDamage();
            OnMineDestroyed?.Invoke(this);
            StartCoroutine(ShowExplosionIndicator());
        }

        private void DealAreaDamage()
        {
            var hits     = Physics2D.OverlapCircleAll(transform.position, _radius, _enemyLayerMask);
            int hitCount = 0;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                var dir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.TakeDamage(_damage, dir);
                hitCount++;
            }
            Debug.Log($"[RangerMine] 폭발 @ {transform.position}, 반경: {_radius}, 히트: {hitCount}명");
        }

        /// <summary>
        /// 폭발 반경을 원형으로 잠깐 표시 후 오브젝트 삭제.
        /// </summary>
        private IEnumerator ShowExplosionIndicator()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;

            var indicatorGo = new GameObject("MineExplosionIndicator");
            indicatorGo.transform.position = transform.position;

            var indicatorSr        = indicatorGo.AddComponent<SpriteRenderer>();
            indicatorSr.sprite     = CreateCircleSprite();
            indicatorSr.color      = new Color(1f, 0.4f, 0f, 0.6f);
            indicatorSr.sortingOrder = 13;

            float diameter = _radius * 2f;
            indicatorGo.transform.localScale = new Vector3(diameter, diameter, 1f);

            float elapsed = 0f;
            while (elapsed < INDICATOR_DURATION)
            {
                elapsed += Time.deltaTime;
                float t     = elapsed / INDICATOR_DURATION;
                float alpha = Mathf.Lerp(0.6f, 0f, t);
                float scale = diameter * Mathf.Lerp(1f, 1.3f, t);
                indicatorSr.color                = new Color(1f, 0.4f, 0f, alpha);
                indicatorGo.transform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            Destroy(indicatorGo);
            Destroy(gameObject);
        }

        private static Sprite CreateCircleSprite()
        {
            const int size   = 64;
            const int center = size / 2;
            const float r    = size / 2f;

            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx   = x - center;
                float dy   = y - center;
                pixels[y * size + x] = (dx * dx + dy * dy) <= r * r
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_exploded) return;
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
            Gizmos.DrawSphere(transform.position, _radius > 0f ? _radius : 1.5f);
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, _radius > 0f ? _radius : 1.5f);
        }
#endif
    }
}
