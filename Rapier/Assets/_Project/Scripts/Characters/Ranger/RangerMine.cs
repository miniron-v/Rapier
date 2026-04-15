using System;
using UnityEngine;
using Game.Enemies;

namespace Game.Characters.Ranger
{
    /// <summary>
    /// 레인저 지뢰.
    ///
    /// [배치 / 수명]
    ///   RangerPresenter.OnDodgeComplete 에서 Instantiate 후 Init() 으로 목적지 주입.
    ///   throwSpeed unit/s 로 목적지까지 직선 이동 후 착지.
    ///   착지 후 lifetime 경과 시 자폭 (LifetimeExplode).
    ///
    /// [폭발 트리거]
    ///   착지 후 OnTriggerEnter2D(enemy Collider2D) → 즉시 폭발.
    ///   폭발: 반경 radius 내 Physics2D.OverlapCircleAll → 각 적에게 데미지 → Destroy(self).
    ///
    /// [큐 관리]
    ///   RangerPresenter 가 Queue{RangerMine} 으로 관리.
    ///   소멸 이벤트 OnMineDestroyed 를 발행해 Presenter 가 큐에서 제거할 수 있도록 한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
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
        [NonSerialized] private bool  _landed;

        private const float LAND_EPSILON = 0.05f;

        // ── 초기화 ────────────────────────────────────────────────────────

        /// <summary>
        /// 지뢰 파라미터를 주입하고 목적지로 이동을 시작한다.
        /// </summary>
        /// <param name="damage">폭발 데미지 (최종값)</param>
        /// <param name="radius">폭발 반경 (unit)</param>
        /// <param name="lifetime">착지 후 수명 (초)</param>
        /// <param name="destination">착지 목적지 (월드 좌표)</param>
        /// <param name="throwSpeed">이동 속도 (unit/s)</param>
        public void Init(float damage, float radius, float lifetime,
                         Vector2 destination, float throwSpeed)
        {
            _damage      = damage;
            _radius      = radius;
            _lifetime    = lifetime;
            _destination = destination;
            _throwSpeed  = throwSpeed;
            _timer       = 0f;
            _exploded    = false;
            _landed      = false;

            // 착지 전 Collider 비활성 — 이동 중 적과 충돌하지 않도록
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.enabled   = false;
            }
        }

        // ── Unity 라이프사이클 ────────────────────────────────────────────

        private void Update()
        {
            if (_exploded) return;

            if (!_landed)
            {
                // 목적지로 이동
                Vector2 current = transform.position;
                Vector2 next    = Vector2.MoveTowards(current, _destination, _throwSpeed * Time.deltaTime);
                transform.position = new Vector3(next.x, next.y, transform.position.z);

                if (Vector2.Distance(next, _destination) <= LAND_EPSILON)
                {
                    // 착지
                    transform.position = new Vector3(_destination.x, _destination.y, transform.position.z);
                    _landed = true;

                    // 착지 후 Collider 활성
                    var col = GetComponent<Collider2D>();
                    if (col != null) col.enabled = true;
                }
                return;
            }

            // 착지 후 수명 카운트
            _timer += Time.deltaTime;
            if (_timer >= _lifetime)
                LifetimeExplode();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_exploded || !_landed) return;

            // Enemy 레이어만 반응
            if (other.gameObject.layer != LayerMask.NameToLayer("Enemy")) return;

            Explode();
        }

        // ── 폭발 로직 ────────────────────────────────────────────────────

        /// <summary>수명 만료로 인한 자폭. Enemy 없이도 소멸.</summary>
        private void LifetimeExplode()
        {
            if (_exploded) return;
            _exploded = true;

            // 수명 만료 시에도 범위 안에 적이 있으면 데미지
            DealAreaDamage();
            DestroyMine();
        }

        /// <summary>적 접촉으로 인한 즉발 폭발.</summary>
        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            DealAreaDamage();
            DestroyMine();
        }

        private void DealAreaDamage()
        {
            int enemyLayer = LayerMask.GetMask("Enemy");
            var hits = Physics2D.OverlapCircleAll(transform.position, _radius, enemyLayer);
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

        private void DestroyMine()
        {
            OnMineDestroyed?.Invoke(this);
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, _radius > 0f ? _radius : 1.5f);
        }
#endif
    }
}
