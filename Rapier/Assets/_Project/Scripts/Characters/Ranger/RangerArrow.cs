using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Enemies;

namespace Game.Characters.Ranger
{
    /// <summary>
    /// 레인저 투사체 (화살).
    ///
    /// [이동]
    ///   Init() 으로 주입된 direction 방향으로 speed unit/s 직선 이동.
    ///   매 프레임 Physics2D.OverlapBoxAll 으로 Enemy 레이어 히트 체크.
    ///
    /// [관통]
    ///   HashSet{Collider2D} 로 이미 타격한 콜라이더를 추적하여 동일 적 중복 타격 방지.
    ///   관통 무제한 (제거 감쇠 없음).
    ///
    /// [소멸 조건]
    ///   1) 누적 이동 거리 >= range
    ///   2) 스테이지 경계 이탈 (StageBuilder.stageWidth/stageHeight 참조)
    /// </summary>
    public class RangerArrow : MonoBehaviour
    {
        // ── 파라미터 (Init 주입) ───────────────────────────────────────────
        [NonSerialized] private float   _damage;
        [NonSerialized] private float   _speed;
        [NonSerialized] private float   _range;
        [NonSerialized] private float   _width;
        [NonSerialized] private Vector2 _direction;
        [NonSerialized] private bool    _piercing;

        // ── 내부 상태 ─────────────────────────────────────────────────────
        [NonSerialized] private float                    _traveled;
        [NonSerialized] private readonly HashSet<Collider2D> _hitColliders = new HashSet<Collider2D>();

        // 스테이지 경계 캐시 (Init 시점에 가져옴)
        [NonSerialized] private float _halfStageW = float.MaxValue;
        [NonSerialized] private float _halfStageH = float.MaxValue;

        // 히트박스 높이 — 화살의 진행 방향 길이 방향. 시각적으로는 얇게, 판정은 작게.
        private const float ARROW_HIT_HEIGHT = 0.3f;
        private const float ARRIVE_EPSILON   = 0.001f;

        // ── 초기화 ────────────────────────────────────────────────────────

        /// <summary>
        /// 화살 파라미터를 주입하고 이동을 시작한다.
        /// </summary>
        /// <param name="damage">타격 시 입힐 데미지 (최종값, 배율 적용 후)</param>
        /// <param name="speed">이동 속도 (unit/s)</param>
        /// <param name="range">최대 사거리 (unit)</param>
        /// <param name="width">히트박스 폭 및 시각적 x/y 스케일 (unit). 공격 범위와 크기를 동일하게 유지.</param>
        /// <param name="direction">발사 방향 (정규화)</param>
        /// <param name="piercing">true = 관통 무제한, false = 첫 번째 적 타격 후 소멸.</param>
        public void Init(float damage, float speed, float range, float width, Vector2 direction, bool piercing = true)
        {
            _damage    = damage;
            _speed     = speed;
            _range     = range;
            _width     = width;
            _direction = direction.sqrMagnitude > ARRIVE_EPSILON ? direction.normalized : Vector2.up;
            _piercing  = piercing;
            _traveled  = 0f;
            _hitColliders.Clear();

            // 스테이지 경계 캐시
            var stage = ServiceLocator.Get<StageBuilder>();
            if (stage != null)
            {
                _halfStageW = stage.stageWidth  * 0.5f;
                _halfStageH = stage.stageHeight * 0.5f;
            }

            // 시각적 스케일: 프리팹 기본 scale 에 width 배수를 곱해 크기 조절
            var baseScale = transform.localScale;
            transform.localScale = new Vector3(baseScale.x * width, baseScale.y * width, baseScale.z);

            // 화살 방향으로 회전 표시 (시각적)
            float angle = Vector2.SignedAngle(Vector2.up, _direction);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ── Unity 라이프사이클 ────────────────────────────────────────────

        private void Update()
        {
            float step = _speed * Time.deltaTime;
            step = Mathf.Min(step, _range - _traveled);

            Vector2 newPos = (Vector2)transform.position + _direction * step;
            transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
            _traveled += step;

            // 히트 판정 (이동 후)
            CheckHits();

            // 소멸 조건 1: 사거리 도달
            if (_traveled >= _range)
            {
                Destroy(gameObject);
                return;
            }

            // 소멸 조건 2: 스테이지 경계 이탈
            if (Mathf.Abs(newPos.x) >= _halfStageW || Mathf.Abs(newPos.y) >= _halfStageH)
            {
                Destroy(gameObject);
            }
        }

        // ── 히트 판정 ────────────────────────────────────────────────────

        private void CheckHits()
        {
            float angle      = Vector2.SignedAngle(Vector2.up, _direction);
            var   boxSize    = new Vector2(_width, ARROW_HIT_HEIGHT);
            int   enemyLayer = LayerMask.GetMask("Enemy");

            var hits = Physics2D.OverlapBoxAll(transform.position, boxSize, angle, enemyLayer);
            foreach (var hit in hits)
            {
                if (_hitColliders.Contains(hit)) continue;

                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                _hitColliders.Add(hit);
                var dir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.TakeDamage(_damage, dir);
                Debug.Log($"[RangerArrow] 적 타격: {enemy.name}, 데미지: {_damage:F0}");

                if (!_piercing)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            float  angle   = Vector2.SignedAngle(Vector2.up, _direction);
            var    rot     = Quaternion.Euler(0f, 0f, angle);
            var    old     = Gizmos.matrix;
            Gizmos.matrix  = Matrix4x4.TRS(transform.position, rot, Vector3.one);
            Gizmos.color   = new Color(0f, 1f, 0.5f, 0.4f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(_width, ARROW_HIT_HEIGHT, 0.1f));
            Gizmos.matrix  = old;
        }
#endif
    }
}
