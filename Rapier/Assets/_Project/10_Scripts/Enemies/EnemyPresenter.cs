using UnityEngine;
using Game.Core;
using Game.Input;
using Game.Combat;
using Game.Characters;

namespace Game.Enemies
{
    /// <summary>
    /// 적 AI 및 전투 로직.
    ///
    /// [AI 흐름]
    ///   Idle   → 플레이어 탐색
    ///   Chase  → 플레이어 방향으로 분산 접근 (랜덤 오프셋 각도 적용)
    ///   Attack → 공격 범위 내 진입 시 쿨다운마다 공격
    ///            공격 직전 플레이어가 히트박스 내에 있을 때만
    ///            GestureRecognizer.OpenAttackWindow() + TakeDamage() 동시 처리
    ///   Dead   → EnemyView.PlayDeath() 호출, WaveManager가 회수
    /// </summary>
    [RequireComponent(typeof(EnemyView))]
    public class EnemyPresenter : MonoBehaviour, IDamageable
    {
        // ── 인스펙터 ─────────────────────────────────────────────
        [SerializeField] private EnemyStatData _statData;

        // ── 내부 상태 ────────────────────────────────────────────
        private EnemyModel        _model;
        private EnemyView         _view;
        private SpriteRenderer    _sr;
        private Transform         _playerTransform;
        private GestureRecognizer _gesture;
        private IDamageable       _playerDamageable;

        private float   _attackCooldownTimer;
        private bool    _isAttacking;        // 현재 히트박스 활성 중
        private float   _attackHitTimer;
        private Vector2 _approachOffset;     // 분산 접근용 고정 오프셋

        // IDamageable
        public bool IsAlive => _model != null && _model.IsAlive;

        // ── 초기화 ───────────────────────────────────────────────
private void Awake()
        {
            _view = GetComponent<EnemyView>();
            _sr   = GetComponent<SpriteRenderer>();
        }

private void Start()
        {
            _gesture = ServiceLocator.Get<GestureRecognizer>();
            var player = ServiceLocator.Get<PlayerPresenter>();
            if (player != null)
            {
                _playerTransform  = player.transform;
                _playerDamageable = player as IDamageable;
            }
        }

        /// <summary>WaveManager가 풀에서 꺼낼 때 호출.</summary>
public void Spawn(EnemyStatData statData, Vector2 position)
        {
            _statData = statData;
            _model    = new EnemyModel(statData);
            _model.OnDeath += HandleDeath;

            transform.position   = position;
            _attackCooldownTimer = statData.attackCooldown;
            _isAttacking         = false;

            // SO에서 스프라이트 할당
            if (_sr != null && statData.sprite != null)
                _sr.sprite = statData.sprite;

            // 분산 접근 오프셋
            float angle   = Random.Range(-statData.approachAngleVariance,
                                          statData.approachAngleVariance);
            _approachOffset = Quaternion.Euler(0f, 0f, angle) * Vector2.up * 0.3f;

            var baseColor = new Color(0.9f, 0.3f, 0.3f);
            _view.ResetVisual(baseColor);
            gameObject.SetActive(true);
        }

        // ── IDamageable ──────────────────────────────────────────
        public void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;
            _model.TakeDamage(amount);
            _view.PlayHit();
        }

        // ── 루프 ─────────────────────────────────────────────────
        private void Update()
        {
            if (!IsAlive || _playerTransform == null) return;

            float dist = Vector2.Distance(transform.position, _playerTransform.position);

            // 히트박스 활성 처리
            if (_isAttacking)
            {
                _attackHitTimer -= Time.deltaTime;

                // 플레이어가 아직 범위 내에 있는 동안 매 프레임 체크
                if (dist <= _statData.attackRange)
                {
                    _gesture?.OpenAttackWindow();
                    _playerDamageable?.TakeDamage(_statData.attackPower, GetDirectionToPlayer());
                }

                if (_attackHitTimer <= 0f)
                {
                    _gesture?.CloseAttackWindow();
                    _isAttacking = false;
                }
                return;
            }

            // 공격 범위 내: 쿨다운 감소 및 공격 시작
            if (dist <= _statData.attackRange)
            {
                _attackCooldownTimer -= Time.deltaTime;
                if (_attackCooldownTimer <= 0f)
                {
                    StartAttack();
                }
                return;
            }

            // 추적: 분산 오프셋을 더한 방향으로 이동
            var dir      = GetDirectionToPlayer() + _approachOffset;
            var nextPos  = (Vector2)transform.position
                           + dir.normalized * (_statData.moveSpeed * Time.deltaTime);
            transform.position = nextPos;
        }

        private void StartAttack()
        {
            _isAttacking         = true;
            _attackHitTimer      = _statData.attackHitDuration;
            _attackCooldownTimer = _statData.attackCooldown;
        }

        private void HandleDeath()
        {
            if (_isAttacking)
                _gesture?.CloseAttackWindow();
            _isAttacking = false;
            _view.PlayDeath();
        }

        private Vector2 GetDirectionToPlayer()
        {
            return ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        }
    }
}
