using System;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 모든 적(일반 적, 보스)의 공통 베이스.
    ///
    /// [공격 흐름]
    ///   Chase → Windup → Hit → PostAttack → Chase ...
    ///
    /// [확장 포인트]
    ///   OnEnterChase / OnEnterWindup / OnEnterHit / OnEnterPostAttack
    ///   OnDeath, GetMoveSpeed, GetAttackPower, GetAttackRange
    /// </summary>
    [RequireComponent(typeof(EnemyView))]
    public abstract class EnemyPresenterBase : MonoBehaviour, IDamageable
    {
        // ── 외부 구독용 사망 이벤트 ───────────────────────────────
        public event Action OnDeath;

        // ── 내부 상태 ────────────────────────────────────────────
        protected EnemyModel       _model;
        protected EnemyView        _view;
        protected SpriteRenderer   _sr;
        protected Transform        _playerTransform;
        protected IDamageable      _playerDamageable;
        protected EnemyHpBar       _hpBar;
        protected Vector2          _approachOffset;
        protected EnemyStatData    _statData;

        // ── 공격 단계 ─────────────────────────────────────────────
        protected enum AttackPhase { Chase, Windup, Hit, PostAttack }
        protected AttackPhase _attackPhase;
        protected float       _phaseTimer;

        // ── 외부 접근 ─────────────────────────────────────────────
        public bool IsAlive => _model != null && _model.IsAlive;

        /// <summary>HUD 등 외부에서 EnemyModel에 접근할 때 사용.</summary>
        public EnemyModel GetModel() => _model;

        // ── 초기화 ───────────────────────────────────────────────
        protected virtual void Awake()
        {
            _view  = GetComponent<EnemyView>();
            _sr    = GetComponent<SpriteRenderer>();
            _hpBar = GetComponentInChildren<EnemyHpBar>(true);
        }

        protected virtual void Start()
        {
            RefreshPlayerReference();
        }

        protected void RefreshPlayerReference()
        {
            var player = ServiceLocator.Get<IPlayerCharacter>();
            if (player != null)
            {
                _playerTransform  = player.transform;
                _playerDamageable = player as IDamageable;
            }
            else
            {
                Debug.LogWarning("[EnemyPresenterBase] IPlayerCharacter가 ServiceLocator에 없음.");
            }
        }

        // ── Spawn ─────────────────────────────────────────────────
        public virtual void Spawn(EnemyStatData statData, Vector2 position)
        {
            _statData = statData;

            if (_model != null)
                _model.OnDeath -= HandleModelDeath;

            _model = new EnemyModel(statData);
            _model.OnDeath += HandleModelDeath;

            transform.position = position;
            _attackPhase       = AttackPhase.Chase;

            if (_sr != null && statData.sprite != null)
                _sr.sprite = statData.sprite;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer == -1)
                Debug.LogError("[EnemyPresenterBase] \"Enemy\" 레이어가 존재하지 않음!");
            else
                gameObject.layer = enemyLayer;

            var col = GetComponent<Collider2D>();
            if (col == null)
                Debug.LogError($"[EnemyPresenterBase] {name} 에 Collider2D가 없음!");
            else if (!col.enabled)
            {
                col.enabled = true;
                Debug.LogWarning($"[EnemyPresenterBase] {name} Collider2D 강제 활성화.");
            }

            float angle     = UnityEngine.Random.Range(-statData.approachAngleVariance, statData.approachAngleVariance);
            _approachOffset = Quaternion.Euler(0f, 0f, angle) * Vector2.up * 0.3f;

            _view.ResetVisual(new Color(0.9f, 0.3f, 0.3f));
            _hpBar?.Init(_model);

            if (_playerTransform == null) RefreshPlayerReference();

            gameObject.SetActive(true);
        }

        // ── IDamageable ──────────────────────────────────────────
        public virtual void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;
            _model.TakeDamage(amount);
            _view.PlayHit();
        }

        // ── 루프 ─────────────────────────────────────────────────
        protected virtual void Update()
        {
            if (!IsAlive || _playerTransform == null) return;

            switch (_attackPhase)
            {
                case AttackPhase.Chase:
                    UpdateChase();
                    break;
                case AttackPhase.Windup:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f) EnterHitPhase();
                    break;
                case AttackPhase.Hit:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f) EnterPostAttackPhase();
                    break;
                case AttackPhase.PostAttack:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f) EnterChasePhase();
                    break;
            }
        }

        // ── Chase 업데이트 ────────────────────────────────────────
        protected virtual void UpdateChase()
        {
            float dist = Vector2.Distance(transform.position, _playerTransform.position);
            if (dist <= GetAttackRange())
            {
                EnterWindupPhase();
                return;
            }

            var dir     = GetDirectionToPlayer() + _approachOffset;
            var nextPos = (Vector2)transform.position
                          + dir.normalized * (GetMoveSpeed() * Time.deltaTime);
            transform.position = nextPos;
        }

        // ── 페이즈 전환 ───────────────────────────────────────────
        protected void EnterChasePhase()
        {
            _attackPhase = AttackPhase.Chase;
            OnEnterChase();
        }

        protected void EnterWindupPhase()
        {
            _attackPhase = AttackPhase.Windup;
            _phaseTimer  = _statData.attackWindupDuration;
            _view.PlayWindup(_statData.attackWindupDuration, GetAttackRange());
            OnEnterWindup();
        }

        protected void EnterHitPhase()
        {
            _attackPhase = AttackPhase.Hit;
            _phaseTimer  = _statData.attackHitDuration;
            _view.StopWindup();

            if (_playerTransform != null &&
                Vector2.Distance(transform.position, _playerTransform.position) <= GetAttackRange())
            {
                _playerDamageable?.TakeDamage(GetAttackPower(), GetDirectionToPlayer());
            }
            OnEnterHit();
        }

        protected void EnterPostAttackPhase()
        {
            _attackPhase = AttackPhase.PostAttack;
            _phaseTimer  = _statData.postAttackDelay;
            OnEnterPostAttack();
        }

        // ── 자식 override 포인트 ──────────────────────────────────
        protected virtual void OnEnterChase()      { }
        protected virtual void OnEnterWindup()     { }
        protected virtual void OnEnterHit()        { }
        protected virtual void OnEnterPostAttack() { }

        protected virtual void HandleModelDeath()
        {
            _attackPhase = AttackPhase.Chase;
            _view.PlayDeath();
            OnDeath?.Invoke();
        }

        // ── 스탯 override 포인트 ──────────────────────────────────
        protected virtual float GetMoveSpeed()   => _statData != null ? _statData.moveSpeed   : 0f;
        protected virtual float GetAttackPower() => _statData != null ? _statData.attackPower : 0f;
        protected virtual float GetAttackRange() => _statData != null ? _statData.attackRange : 1f;

        // ── 유틸 ──────────────────────────────────────────────────
        protected Vector2 GetDirectionToPlayer()
        {
            return ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        }
    }
}
