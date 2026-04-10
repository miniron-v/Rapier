using System;
using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 모든 적(일반 적, 보스)의 공통 베이스.
    ///
    /// [공격 흐름]
    ///   Chase → Windup → Hit(AttackAction.Execute) → PostAttack → Chase ...
    ///
    /// [시퀀서]
    ///   EnterWindupPhase() 진입 시 시퀀서에서 다음 AttackAction을 꺼낸다.
    ///   각 AttackAction이 자신의 windupDuration, indicators, Execute() 를 소유한다.
    ///
    /// [확장 포인트]
    ///   OnEnterChase / OnEnterWindup / OnEnterHit / OnEnterPostAttack / OnDeath
    ///   GetMoveSpeed / GetAttackPower / GetAttackRange
    ///   SetSequence() : BossPresenterBase가 페이즈 전환 시 호출
    /// </summary>
    [RequireComponent(typeof(EnemyView))]
    public abstract class EnemyPresenterBase : MonoBehaviour, IDamageable
    {
        public event Action OnDeath;

        // ── 내부 참조 ────────────────────────────────────────────
        protected EnemyModel      _model;
        protected EnemyView       _view;
        protected SpriteRenderer  _sr;
        protected Transform       _playerTransform;
        protected IDamageable     _playerDamageable;
        protected EnemyHpBar      _hpBar;
        protected Vector2         _approachOffset;
        protected EnemyStatData   _statData;

        // ── 공격 상태머신 ─────────────────────────────────────────
        protected enum AttackPhase { Chase, Windup, Hit, PostAttack }
        protected AttackPhase _attackPhase;
        protected float       _phaseTimer;

        // ── 시퀀서 + 현재 액션 ────────────────────────────────────
        private   EnemyAttackSequencer _sequencer = new EnemyAttackSequencer();
        protected EnemyAttackAction    _currentAction;
        private   Coroutine            _actionCoroutine;

        // ── 컨텍스트 ─────────────────────────────────────────────
        private EnemyAttackContext _ctx;

        public bool IsAlive => _model != null && _model.IsAlive;
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

            if (_model != null) _model.OnDeath -= HandleModelDeath;
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
            if (col != null && !col.enabled) col.enabled = true;

            float angle     = UnityEngine.Random.Range(-statData.approachAngleVariance, statData.approachAngleVariance);
            _approachOffset = Quaternion.Euler(0f, 0f, angle) * Vector2.up * 0.3f;

            _view.ResetVisual(new Color(0.9f, 0.3f, 0.3f));
            _hpBar?.Init(_model);

            if (_playerTransform == null) RefreshPlayerReference();

            _sequencer.SetSequence(statData.attackSequence);
            _ctx = BuildContext();

            gameObject.SetActive(true);
        }

        // ── IDamageable ──────────────────────────────────────────
        public virtual void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;
            _model.TakeDamage(amount);
            _view.PlayHit();
        }

        // ── Update ───────────────────────────────────────────────
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
                    break;
                case AttackPhase.PostAttack:
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f) EnterChasePhase();
                    break;
            }
        }

        // ── Chase ─────────────────────────────────────────────────
        protected virtual void UpdateChase()
        {
            float dist = Vector2.Distance(transform.position, _playerTransform.position);
            if (dist <= GetAttackRange())
            {
                EnterWindupPhase();
                return;
            }
            var dir = GetDirectionToPlayer() + _approachOffset;
            transform.position = (Vector2)transform.position
                                 + dir.normalized * (GetMoveSpeed() * Time.deltaTime);
        }

        // ── 페이즈 전환 ───────────────────────────────────────────
        protected void EnterChasePhase()
        {
            _attackPhase = AttackPhase.Chase;
            OnEnterChase();
        }

        protected void EnterWindupPhase()
        {
            if (!_sequencer.HasSequence)
            {
                Debug.LogWarning($"[EnemyPresenterBase] {name}: attackSequence가 비어있음. Chase 유지.");
                return;
            }

            _currentAction     = _sequencer.Next();
            _attackPhase       = AttackPhase.Windup;
            _phaseTimer        = _currentAction.windupDuration;
            _ctx.LockedForward = GetDirectionToPlayer();

            var finalIndicators = _currentAction.PrepareWindup(_ctx);

            _view.PlayWindup(
                _currentAction.windupDuration,
                finalIndicators,
                _currentAction.lockIndicatorDirection,
                GetDirectionToPlayer);

                        OnEnterWindup();
        }

        protected void EnterHitPhase()
        {
            _attackPhase = AttackPhase.Hit;
            _view.StopWindup();

            if (_currentAction == null)
            {
                EnterPostAttackPhase();
                return;
            }

            if (_actionCoroutine != null) StopCoroutine(_actionCoroutine);
            _actionCoroutine = StartCoroutine(
                _currentAction.Execute(_ctx, OnActionComplete));

            OnEnterHit();
        }

        protected void EnterPostAttackPhase()
        {
            _attackPhase = AttackPhase.PostAttack;
            _phaseTimer  = _statData.postAttackDelay;
            OnEnterPostAttack();
        }

        private void OnActionComplete()
        {
            _actionCoroutine = null;
            EnterPostAttackPhase();
        }

        // ── 시퀀서 교체 (BossPresenterBase용) ────────────────────
        protected void SetSequence(System.Collections.Generic.List<EnemyAttackAction> sequence)
        {
            _sequencer.SetSequence(sequence);
        }

        // ── 자식 override 포인트 ──────────────────────────────────
        protected virtual void OnEnterChase()      { }
        protected virtual void OnEnterWindup()     { }
        protected virtual void OnEnterHit()        { }
        protected virtual void OnEnterPostAttack() { }

        protected virtual void HandleModelDeath()
        {
            _attackPhase = AttackPhase.Chase;
            if (_actionCoroutine != null)
            {
                StopCoroutine(_actionCoroutine);
                _actionCoroutine = null;
            }
            _view.PlayDeath();
            OnDeath?.Invoke();
        }

        // ── 스탯 override 포인트 ──────────────────────────────────
        protected virtual float GetMoveSpeed()   => _statData != null ? _statData.moveSpeed   : 0f;
        protected virtual float GetAttackPower() => _statData != null ? _statData.attackPower : 0f;
        protected virtual float GetAttackRange() => _statData != null ? _statData.attackRange : 1f;

        // ── 유틸 ─────────────────────────────────────────────────
        protected Vector2 GetDirectionToPlayer()
        {
            if (_playerTransform == null) return Vector2.up;
            return ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        }

        private EnemyAttackContext BuildContext()
        {
            return new EnemyAttackContext
            {
                SelfTransform    = transform,
                PlayerTransform  = _playerTransform,
                PlayerDamageable = _playerDamageable,
                SpriteRenderer   = _sr,
                Stage            = ServiceLocator.Get<StageBuilder>(),
                GetAttackPower   = GetAttackPower,
                GetForward       = GetDirectionToPlayer,
            };
        }
    }
}
