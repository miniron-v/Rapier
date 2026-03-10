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
    /// [공격 흐름]
    ///   Windup  → 색상 변화 + 범위 표시 (attackWindupDuration 초)
    ///   Hit     → playerDamageable.TakeDamage() 호출
    ///             피해 적용 여부는 PlayerPresenter.TakeDamage() 내부에서 결정
    ///             (무적 중이면 JustDodge 트리거, 피해 없음)
    ///
    /// [AI 흐름]
    ///   Idle   → 플레이어 탐색
    ///   Chase  → 플레이어 방향으로 분산 접근
    ///   Windup → 공격 예고 연출
    ///   Attack → 피해 적용
    ///   Dead   → EnemyView.PlayDeath() 호출, WaveManager가 회수
    /// </summary>
    [RequireComponent(typeof(EnemyView))]
    public class EnemyPresenter : MonoBehaviour, IDamageable
    {
        // ── 인스펙터 ─────────────────────────────────────────────
        [SerializeField] private EnemyStatData _statData;

        // ── 내부 상태 ────────────────────────────────────────────
        private EnemyModel     _model;
        private EnemyView      _view;
        private SpriteRenderer _sr;
        private Transform      _playerTransform;
        private IDamageable    _playerDamageable;
        private EnemyHpBar     _hpBar;
        private Vector2        _approachOffset;

        private float _attackCooldownTimer;

        // ── 공격 단계 ─────────────────────────────────────────────
        private enum AttackPhase { None, Windup, Hit }
        private AttackPhase _attackPhase;
        private float       _phaseTimer;

        // IDamageable
        public bool IsAlive => _model != null && _model.IsAlive;

        // ── 초기화 ───────────────────────────────────────────────
        private void Awake()
        {
            _view  = GetComponent<EnemyView>();
            _sr    = GetComponent<SpriteRenderer>();
            _hpBar = GetComponentInChildren<EnemyHpBar>(true);
        }

        private void Start()
        {
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
            _attackPhase         = AttackPhase.None;

            if (_sr != null && statData.sprite != null)
                _sr.sprite = statData.sprite;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer == -1)
                Debug.LogError("[EnemyPresenter] \"Enemy\" 레이어가 존재하지 않음!");
            else
                gameObject.layer = enemyLayer;

            var col = GetComponent<Collider2D>();
            if (col == null)
                Debug.LogError($"[EnemyPresenter] {name} 에 Collider2D가 없음!");
            else if (!col.enabled)
            {
                col.enabled = true;
                Debug.LogWarning($"[EnemyPresenter] {name} Collider2D 강제 활성화.");
            }

            float angle     = Random.Range(-statData.approachAngleVariance, statData.approachAngleVariance);
            _approachOffset = Quaternion.Euler(0f, 0f, angle) * Vector2.up * 0.3f;

            _view.ResetVisual(new Color(0.9f, 0.3f, 0.3f));
            _hpBar?.Init(_model);

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

            switch (_attackPhase)
            {
                case AttackPhase.Windup:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        EnterHitPhase();
                    return;

                case AttackPhase.Hit:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        EndAttack();
                    return;
            }

            // 공격 범위 내: 쿨다운 감소
            if (dist <= _statData.attackRange)
            {
                _attackCooldownTimer -= Time.deltaTime;
                if (_attackCooldownTimer <= 0f)
                    EnterWindupPhase();
                return;
            }

            // 추적
            var dir     = GetDirectionToPlayer() + _approachOffset;
            var nextPos = (Vector2)transform.position
                          + dir.normalized * (_statData.moveSpeed * Time.deltaTime);
            transform.position = nextPos;
        }

        // ── 공격 단계 전환 ────────────────────────────────────────

        private void EnterWindupPhase()
        {
            _attackPhase = AttackPhase.Windup;
            _phaseTimer  = _statData.attackWindupDuration;
            _view.PlayWindup(_statData.attackWindupDuration, _statData.attackRange);
            Debug.Log($"[EnemyPresenter] {name} Windup 시작 ({_statData.attackWindupDuration}초)");
        }

        private void EnterHitPhase()
        {
            _attackPhase = AttackPhase.Hit;
            _phaseTimer  = _statData.attackHitDuration;
            _view.StopWindup();

            // 플레이어가 아직 범위 내에 있을 때만 피해 적용
            if (_playerTransform != null &&
                Vector2.Distance(transform.position, _playerTransform.position) <= _statData.attackRange)
            {
                _playerDamageable?.TakeDamage(_statData.attackPower, GetDirectionToPlayer());
            }

            Debug.Log($"[EnemyPresenter] {name} Hit 발동");
        }

        private void EndAttack()
        {
            _attackPhase         = AttackPhase.None;
            _attackCooldownTimer = _statData.attackCooldown;
            Debug.Log($"[EnemyPresenter] {name} 공격 종료, 쿨다운 시작");
        }

        private void HandleDeath()
        {
            _attackPhase = AttackPhase.None;
            _view.PlayDeath();
        }

        private Vector2 GetDirectionToPlayer()
        {
            return ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        }
    }
}
