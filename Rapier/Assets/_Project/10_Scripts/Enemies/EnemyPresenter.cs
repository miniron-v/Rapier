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
    ///   Chase      → 플레이어 추적. 범위 진입 즉시 Windup 시작.
    ///   Windup     → 정지. 범위 인디케이터 알파 0.5→1.0 (attackWindupDuration 초)
    ///   Hit        → TakeDamage 호출. 즉시 PostAttack 진입.
    ///   PostAttack → 정지 (postAttackDelay 초). 이후 Chase 복귀.
    ///
    /// [총 공격 소요 시간]
    ///   attackWindupDuration(0.3) + attackHitDuration(0.05) + postAttackDelay(0.3) ≈ 0.65초
    ///   ※ Inspector에서 조정 가능
    ///
    /// [피해 판정]
    ///   PlayerPresenter.TakeDamage() 내부에서 무적 여부 결정.
    ///   무적 중이면 JustDodge 트리거, 피해 없음.
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

        // ── 공격 단계 ─────────────────────────────────────────────
        private enum AttackPhase { Chase, Windup, Hit, PostAttack }
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

            transform.position = position;
            _attackPhase       = AttackPhase.Chase;

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

            switch (_attackPhase)
            {
                case AttackPhase.Chase:
                    UpdateChase();
                    break;

                case AttackPhase.Windup:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        EnterHitPhase();
                    break;

                case AttackPhase.Hit:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        EnterPostAttackPhase();
                    break;

                case AttackPhase.PostAttack:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        EnterChasePhase();
                    break;
            }
        }

        // ── 상태 업데이트 ─────────────────────────────────────────

        private void UpdateChase()
        {
            float dist = Vector2.Distance(transform.position, _playerTransform.position);

            // 범위 진입 즉시 공격
            if (dist <= _statData.attackRange)
            {
                EnterWindupPhase();
                return;
            }

            // 추적 이동
            var dir     = GetDirectionToPlayer() + _approachOffset;
            var nextPos = (Vector2)transform.position
                          + dir.normalized * (_statData.moveSpeed * Time.deltaTime);
            transform.position = nextPos;
        }

        // ── 공격 단계 전환 ────────────────────────────────────────

        private void EnterChasePhase()
        {
            _attackPhase = AttackPhase.Chase;
        }

        private void EnterWindupPhase()
        {
            _attackPhase = AttackPhase.Windup;
            _phaseTimer  = _statData.attackWindupDuration;
            _view.PlayWindup(_statData.attackWindupDuration, _statData.attackRange);
        }

        private void EnterHitPhase()
        {
            _attackPhase = AttackPhase.Hit;
            _phaseTimer  = _statData.attackHitDuration;
            _view.StopWindup();

            if (_playerTransform != null &&
                Vector2.Distance(transform.position, _playerTransform.position) <= _statData.attackRange)
            {
                _playerDamageable?.TakeDamage(_statData.attackPower, GetDirectionToPlayer());
            }
        }

        private void EnterPostAttackPhase()
        {
            _attackPhase = AttackPhase.PostAttack;
            _phaseTimer  = _statData.postAttackDelay;
        }

        private void HandleDeath()
        {
            _attackPhase = AttackPhase.Chase;
            _view.PlayDeath();
        }

        private Vector2 GetDirectionToPlayer()
        {
            return ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        }
    }
}
