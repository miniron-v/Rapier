using System;
using UnityEngine;
using Game.Enemies;

namespace Game.Characters.Ranger
{
    /// <summary>
    /// 레인저 지뢰.
    ///
    /// [배치 / 수명]
    ///   RangerPresenter.OnDodgeComplete 에서 현재 위치에 Instantiate.
    ///   Init() 으로 damage / radius / lifetime 을 주입받는다.
    ///   lifetime 경과 시 자폭 (LifetimeExplode).
    ///
    /// [폭발 트리거]
    ///   OnTriggerEnter2D(enemy Collider2D) → 즉시 폭발.
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
        [NonSerialized] private float _damage;
        [NonSerialized] private float _radius;
        [NonSerialized] private float _lifetime;

        // ── 내부 상태 ─────────────────────────────────────────────────────
        [NonSerialized] private float _timer;
        [NonSerialized] private bool  _exploded;

        // ── 초기화 ────────────────────────────────────────────────────────

        /// <summary>
        /// 지뢰 파라미터를 주입한다.
        /// </summary>
        /// <param name="damage">폭발 데미지 (최종값)</param>
        /// <param name="radius">폭발 반경 (unit)</param>
        /// <param name="lifetime">수명 (초)</param>
        public void Init(float damage, float radius, float lifetime)
        {
            _damage   = damage;
            _radius   = radius;
            _lifetime = lifetime;
            _timer    = 0f;
            _exploded = false;

            // Trigger Collider 확인 — 없으면 CircleCollider2D 추가
            var col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        // ── Unity 라이프사이클 ────────────────────────────────────────────

        private void Update()
        {
            if (_exploded) return;

            _timer += Time.deltaTime;
            if (_timer >= _lifetime)
            {
                // 수명 만료 자폭
                LifetimeExplode();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_exploded) return;

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
