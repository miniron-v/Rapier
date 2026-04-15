using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Characters;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Input;

namespace Game.Characters.Ranger
{
    /// <summary>
    /// 레인저 캐릭터 Presenter.
    ///
    /// [고유 메커니즘]
    ///   Tap    : 전방으로 RangerArrow 발사 (ATK×100%, 사거리 8, 속도 25, 기본 너비).
    ///   Swipe  : 회피 대시 시작 순간 RangerMine 투척 (OnSwipe override) → 회피 반대 방향 3갈래.
    ///   Hold   : 차지 경직 진입 (_isChargeLocked=true). 이동 잠금 + Tap 차단.
    ///   Hold Drag Update : _aimDirection 갱신 + AimIndicatorView 업데이트.
    ///   Hold Release : 차지량(t) 비례 화살 발사 → 경직 해제.
    ///   저스트 회피 후 Hold→Release : 강화 관통 화살 (ATK×300%, 사거리 10, 너비×3).
    ///
    /// [잠금-해제 매핑]
    ///   _isChargeLocked = true   진입: OnHold (Hold 성립 시)
    ///   _isChargeLocked = false  해제: HandleHoldRelease(정상) / OnDisable / OnBeforeDeath
    ///   LockMovement             진입: OnHold (Hold 성립 시)
    ///   FreeMovement             해제: HandleHoldRelease / OnDisable / OnBeforeDeath
    ///   _activeMines 큐          정리: OnDisable / OnBeforeDeath / OnRoomTransition
    ///   AimIndicatorView 활성    진입: OnHold (Hold 성립 시)
    ///   AimIndicatorView 비활성  해제: HandleHoldRelease / OnDisable / OnBeforeDeath
    ///
    /// [CanDodge 제한 — Phase 26-D 에서 해결됨]
    ///   CharacterPresenterBase.HandleSwipe 초입에 `if (!CanDodge) return;` 체크가 추가되어
    ///   차지 경직 중(_isChargeLocked=true) Swipe 입력이 Base 레벨에서 차단된다.
    ///   override: `protected override bool CanDodge => !_isChargeLocked;`
    /// </summary>
    [RequireComponent(typeof(CharacterView))]
    public class RangerPresenter : CharacterPresenterBase, IDamageable, IPlayerCharacter
    {
        // ── 직렬화 필드 ───────────────────────────────────────────────────
        [Header("데이터")]
        [SerializeField] private RangerStatData _statData;

        [Header("투사체 프리팹 (null 이면 빈 GameObject 폴백)")]
        [Tooltip("RangerArrow 컴포넌트가 부착된 프리팹. null 허용.")]
        [SerializeField] private GameObject _arrowPrefab;

        [Header("지뢰 프리팹 (null 이면 빈 GameObject 폴백)")]
        [Tooltip("RangerMine 컴포넌트가 부착된 프리팹. null 허용.")]
        [SerializeField] private GameObject _minePrefab;

        // ── 런타임 비직렬화 필드 ──────────────────────────────────────────
        private CharacterView _view;

        /// <summary>현재 조준 방향. OnHoldDragUpdate 에서 갱신. 기본값은 전방(위).</summary>
        [System.NonSerialized] private Vector2 _aimDirection = Vector2.up;

        /// <summary>차지 경직 플래그. true 동안 CanAttack=false + LockMovement 유지.</summary>
        [System.NonSerialized] private bool _isChargeLocked;

        /// <summary>강화 화살 1회 제한 플래그. OnJustDodge 에서 false 리셋, OnJustDodgeTap 에서 true 세팅.</summary>
        [System.NonSerialized] private bool _justDodgeArrowUsed;

        /// <summary>강화 화살 타겟. OnJustDodge 에서 캐싱, OnJustDodgeTap/OnSlowMotionEnd/OnBeforeDeath/OnDisable 에서 정리.</summary>
        [System.NonSerialized] private EnemyPresenterBase _justDodgeTarget;

        /// <summary>현재 차지 진행률 (0~1). OnHold 에서 갱신.</summary>
        [System.NonSerialized] private float _chargeProgress;

        // ── 지뢰 큐 ──────────────────────────────────────────────────────
        [System.NonSerialized] private readonly Queue<RangerMine> _activeMines = new Queue<RangerMine>();

        // ── AimIndicator ─────────────────────────────────────────────────
        [System.NonSerialized] private AimIndicatorView _aimIndicator;

        // ── 초기화 ────────────────────────────────────────────────────────

        private void Awake()
        {
            _view = GetComponent<CharacterView>();

            if (_statData == null)
            {
                Debug.LogError("[RangerPresenter] RangerStatData 가 할당되지 않음.");
                return;
            }

            Init(_statData, _view, "Ranger");

            if (_statData.sprite != null)
                _view.SetSprite(_statData.sprite);

            // AimIndicatorView 전용 GameObject 생성
            var aimGo = new GameObject("RangerAimIndicator");
            aimGo.transform.SetParent(null); // 씬 루트 (캐릭터 이동에 따라 이동 안 함, UpdateAim 에서 직접 세팅)
            _aimIndicator = aimGo.AddComponent<AimIndicatorView>();
            _aimIndicator.Hide();

            ServiceLocator.Register<IPlayerCharacter>(this);
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ServiceLocator.Unregister<IPlayerCharacter>();
            ServiceLocator.Unregister<RangerPresenter>();

            // AimIndicator 정리
            if (_aimIndicator != null && _aimIndicator.gameObject != null)
                Destroy(_aimIndicator.gameObject);
        }

        // ── 이벤트 구독 / 해제 ────────────────────────────────────────────

        protected override void OnDisable()
        {
            // 차지 경직 해제
            if (_isChargeLocked)
            {
                _isChargeLocked = false;
                FreeMovement();
            }

            // AimIndicator 비활성 (OnDestroy 이후 gameObject 가 파괴된 상태일 수 있으므로 명시적 체크)
            if (_aimIndicator != null && _aimIndicator.gameObject != null)
                _aimIndicator.Hide();

            _justDodgeTarget   = null;
            _justDodgeArrowUsed = false;

            base.OnDisable();

            CleanupAllMines();
        }

        // ── IDamageable / IPlayerCharacter ────────────────────────────────

        /// <inheritdoc/>
        public bool IsAlive => CharacterIsAlive;

        /// <inheritdoc/>
        public void TakeDamage(float amount, Vector2 knockbackDir) => ProcessTakeDamage(amount, knockbackDir);

        /// <inheritdoc/>
        public CharacterModel PublicModel => Model;

        // ── CanAttack / CanDodge override ────────────────────────────────────────────

        /// <summary>
        /// 차지 경직 중에는 공격 불가.
        /// Base 의 Tap 차단과 AND 결합됨.
        /// </summary>
        protected override bool CanAttack => !_isChargeLocked;

        /// <summary>
        /// 차지 경직 중에는 회피 불가.
        /// Base 의 HandleSwipe 초입 CanDodge 체크와 AND 결합됨.
        /// </summary>
        protected override bool CanDodge => !_isChargeLocked;


        // ── 사망 전처리 훅 ────────────────────────────────────────────────

        protected override void OnBeforeDeath()
        {
            // 차지 경직 해제
            if (_isChargeLocked)
            {
                _isChargeLocked = false;
                FreeMovement();
            }

            // AimIndicator 비활성 (OnDestroy 이후 호출 시 gameObject 가 파괴된 상태일 수 있으므로 명시적 체크)
            if (_aimIndicator != null && _aimIndicator.gameObject != null)
                _aimIndicator.Hide();

            _justDodgeTarget   = null;
            _justDodgeArrowUsed = false;

            // 지뢰 전부 정리
            CleanupAllMines();
        }

        // ── 방 전환 훅 ────────────────────────────────────────────────────

        protected override void OnRoomTransition()
        {
            CleanupAllMines();
        }

        // ── DodgeDash 완료 훅 ─────────────────────────────────────────────

        /// <summary>
        /// 회피 완료 시 저스트 회피 소비 처리.
        /// 지뢰 투척은 OnSwipe(회피 시작)에서 수행한다.
        /// </summary>
        protected override void OnDodgeDashComplete()
        {
            base.OnDodgeDashComplete();
            ConsumeJustDodge();
        }

        /// <summary>
        /// 회피 시작 순간 지뢰를 투척한다.
        /// 회피 반대 방향 기준 0°/+45°/-45° 세 방향으로.
        /// 출발점: 회피 시작 시점 플레이어 위치.
        /// 도착점: 각 방향으로 dashDistance 거리.
        /// </summary>
        private void ThrowMines()
        {
            Vector2 origin   = (Vector2)transform.position;
            Vector2 throwDir = -DodgeDir; // 회피 반대 방향
            float   dist     = _statData.dashDistance;

            float[] angles = { 0f, 45f, -45f };
            foreach (float angleDeg in angles)
            {
                Vector2 dir  = RotateVector(throwDir, angleDeg);
                PlaceMine(origin + dir * dist);
            }
        }

        private static Vector2 RotateVector(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        // ── 저스트 회피 훅 ────────────────────────────────────────────────

        /// <summary>
        /// Swipe 발생 시: 저스트 회피 활성 + 지뢰 투척(회피 시작 순간).
        /// DodgeDir 은 Base.HandleSwipe 에서 이미 설정됨.
        /// </summary>
        protected override void OnSwipe(Vector2 direction)
        {
            EnableJustDodge();

            if (_statData != null && _statData.MinePlaceOnDodge)
                ThrowMines();
        }

        /// <summary>
        /// 저스트 회피 발동 시 강화 화살 준비: 타겟 캐싱 + 1회 제한 플래그 리셋.
        /// </summary>
        protected override void OnJustDodge(Vector2 direction)
        {
            _justDodgeArrowUsed = false;
            _justDodgeTarget    = FindNearestEnemy(30f);
            Debug.Log($"[RangerPresenter] 저스트 회피 발동 → 강화 화살 준비, 타겟: {(_justDodgeTarget != null ? _justDodgeTarget.name : "없음")}");
        }

        // ── 슬로우모션 종료 훅 ────────────────────────────────────────────

        protected override void OnSlowMotionEnd()
        {
            base.OnSlowMotionEnd();
            _justDodgeTarget    = null;
            _justDodgeArrowUsed = false;
        }

        // ── Tap override — 원거리 사격 ────────────────────────────────────

        /// <summary>
        /// 일반 Tap → 최인접 적 방향으로 화살 발사.
        /// </summary>
        protected override void OnNormalAttack(Vector2 screenPos)
        {
            var nearest = FindNearestEnemy(30f);
            var dir = nearest != null
                ? ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            float damage = Model.AttackPower * (_statData.TapDamagePercent / 100f);
            SpawnArrow((Vector2)transform.position, dir, damage,
                       _statData.TapProjectileSpeed, _statData.TapProjectileRange, _statData.TapProjectileWidth, piercing: false);

            Debug.Log($"[RangerPresenter] 일반 사격 → 방향: {dir}, 데미지: {damage:F0}");
        }

        // ── OnHold override — 차지 진행률 추적 + 경직 진입 ──────────────

        /// <summary>
        /// Hold 매 프레임: 차지 진행률 갱신 + 처음 Hold 성립 시 경직 진입.
        /// </summary>
        protected override void OnHold(float duration)
        {
            _chargeProgress = Mathf.Clamp01(duration / Model.ChargeRequiredTime);

            // 차지 경직 진입 (최초 1회)
            if (!_isChargeLocked)
            {
                _isChargeLocked = true;
                LockMovement();
                _aimDirection = Vector2.up; // 기본값으로 초기화
                _aimIndicator?.Show();
                Debug.Log("[RangerPresenter] 차지 시작 → 경직 진입");
            }

            // AimIndicator 업데이트
            float aimLength = Mathf.Lerp(_statData.ChargeArrowMinRange, _statData.ChargeArrowMaxRange, _chargeProgress);
            _aimIndicator?.UpdateAim(transform.position, _aimDirection, aimLength);

        }

        // ── Hold 확장 이벤트 핸들러 ───────────────────────────────────────

        /// <summary>
        /// OnHoldDragUpdate: 조준 방향 갱신.
        /// fromStart 가 zero 이면 마지막 유효 _aimDirection 유지.
        /// </summary>
        protected override void OnHoldDragUpdate(Vector2 fromStart)
        {
            if (fromStart.sqrMagnitude > 0.01f)
                _aimDirection = fromStart.normalized;

            float aimLength = Mathf.Lerp(_statData.ChargeArrowMinRange, _statData.ChargeArrowMaxRange, _chargeProgress);
            _aimIndicator?.UpdateAim(transform.position, _aimDirection, aimLength);
        }

        /// <summary>
        /// 차지 중 손을 뗄 때(Swipe/Release 무관) 화살 발사.
        /// _isChargeLocked 가 true인 동안은 반드시 발사 후 경직 해제.
        /// </summary>
        protected override void OnRelease(InputState lastState)
        {
            if (!_isChargeLocked) return;
            if (Model == null || !Model.IsAlive) return;

            FireChargeArrow(_chargeProgress);
            ReleaseLock();
        }

        /// <summary>
        /// 저스트 회피 슬로우 중 Tap → 강화 화살 1회 발사.
        /// _justDodgeArrowUsed 로 슬로우 구간 내 재진입 차단.
        /// </summary>
        protected override void OnJustDodgeTap()
        {
            if (_justDodgeArrowUsed) return;
            if (Model == null || !Model.IsAlive) return;

            _justDodgeArrowUsed = true;

            Vector2 dir = (_justDodgeTarget != null && _justDodgeTarget.IsAlive)
                ? ((Vector2)_justDodgeTarget.transform.position - (Vector2)transform.position).normalized
                : _aimDirection;

            FireJustDodgeArrow(dir);
            _justDodgeTarget = null;
        }

        // ── 화살 발사 로직 ────────────────────────────────────────────────

        private void FireChargeArrow(float t)
        {
            float damagePercent = Mathf.Lerp(_statData.ChargeArrowMinDamagePercent, _statData.ChargeArrowMaxDamagePercent, t);
            float range         = Mathf.Lerp(_statData.ChargeArrowMinRange,         _statData.ChargeArrowMaxRange,         t);
            float widthMult     = Mathf.Lerp(1f,                                    _statData.ChargeArrowMaxWidthMult,      t);
            float width         = _statData.TapProjectileWidth * widthMult;
            float damage        = Model.AttackPower * (damagePercent / 100f) * Model.SkillDamageMultiplier;

            SpawnArrow((Vector2)transform.position, _aimDirection, damage,
                       _statData.TapProjectileSpeed, range, width);

            Debug.Log($"[RangerPresenter] 차지 화살 발사 — t:{t:F2}, 데미지:{damage:F0}, 사거리:{range:F1}, 너비:{width:F2}");
        }

        private void FireJustDodgeArrow(Vector2 dir)
        {
            float damage = Model.AttackPower * (_statData.JustDodgeArrowDamagePercent / 100f) * Model.SkillDamageMultiplier;
            float width  = _statData.TapProjectileWidth * _statData.JustDodgeArrowWidthMult;

            SpawnArrow((Vector2)transform.position, dir, damage,
                       _statData.TapProjectileSpeed, _statData.JustDodgeArrowRange, width);

            Debug.Log($"[RangerPresenter] 저스트 회피 강화 화살 발사 — 방향:{dir}, 데미지:{damage:F0}, 사거리:{_statData.JustDodgeArrowRange}, 너비:{width:F2}");
        }

        private void SpawnArrow(Vector2 origin, Vector2 direction, float damage,
                                 float speed, float range, float width, bool piercing = true)
        {
            GameObject go;
            if (_arrowPrefab != null)
            {
                go = Instantiate(_arrowPrefab, new Vector3(origin.x, origin.y, 0f), Quaternion.identity);
            }
            else
            {
                go = new GameObject("RangerArrow");
                go.transform.position = new Vector3(origin.x, origin.y, 0f);

                // 폴백 시각 (작은 사각형)
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = CreateSquareSprite();
                sr.color        = new Color(0.2f, 0.8f, 1f, 0.9f);
                sr.sortingOrder = 12;
                go.transform.localScale = new Vector3(width, 0.15f, 1f);
            }

            var arrow = go.GetComponent<RangerArrow>();
            if (arrow == null)
                arrow = go.AddComponent<RangerArrow>();

            // Collider 추가 (없을 경우)
            if (go.GetComponent<Collider2D>() == null)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size      = new Vector2(width, 0.3f);
                box.isTrigger = true;
            }

            arrow.Init(damage, speed, range, width, direction, piercing);
        }

        // ── 지뢰 설치 로직 ────────────────────────────────────────────────

        /// <summary>
        /// 현재 위치에서 destination 으로 지뢰를 투척한다.
        /// </summary>
        private void PlaceMine(Vector2 destination)
        {
            // 최대 수 초과 시 최고참 제거
            if (_activeMines.Count >= _statData.MaxActiveMines)
            {
                var oldest = _activeMines.Dequeue();
                if (oldest != null)
                {
                    oldest.OnMineDestroyed -= HandleMineDestroyed;
                    Destroy(oldest.gameObject);
                    Debug.Log("[RangerPresenter] 최고참 지뢰 제거 (최대 수 초과)");
                }
            }

            // 투척 시작 위치 = 현재 플레이어 위치
            Vector2 origin = (Vector2)transform.position;

            GameObject go;
            if (_minePrefab != null)
            {
                go = Instantiate(_minePrefab, new Vector3(origin.x, origin.y, 0f), Quaternion.identity);
            }
            else
            {
                go = new GameObject("RangerMine");
                go.transform.position = new Vector3(origin.x, origin.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = CreateSquareSprite();
                sr.color        = new Color(1f, 0.4f, 0f, 0.8f);
                sr.sortingOrder = 11;
                go.transform.localScale = Vector3.one * 0.5f;
            }

            var mine = go.GetComponent<RangerMine>();
            if (mine == null)
                mine = go.AddComponent<RangerMine>();

            // 충돌 감지는 RangerMine.Update 에서 Physics2D.OverlapCircleAll 폴링으로 처리.
            // (적 프리팹에 Rigidbody2D 없음 → OnTriggerEnter2D 미사용)

            float damage = Model.AttackPower * (_statData.MineDamagePercent / 100f);
            mine.Init(damage, _statData.MineExplosionRadius, _statData.MineLifetime,
                      destination, _statData.MineThrowSpeed);
            mine.OnMineDestroyed += HandleMineDestroyed;
            _activeMines.Enqueue(mine);

            Debug.Log($"[RangerPresenter] 지뢰 투척 → {origin} → {destination}, 활성 지뢰: {_activeMines.Count}");
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────────────

        private void HandleMineDestroyed(RangerMine mine)
        {
            mine.OnMineDestroyed -= HandleMineDestroyed;
            // Queue 에서 안전하게 제거 (Queue 는 중간 제거 미지원 → 재구성)
            RemoveMineFromQueue(mine);
        }

        // ── 내부 유틸 ─────────────────────────────────────────────────────

        /// <summary>차지 경직 해제 + AimIndicator 숨김.</summary>
        private void ReleaseLock()
        {
            _isChargeLocked = false;
            _chargeProgress = 0f;
            FreeMovement();
            _aimIndicator?.Hide();
            Debug.Log("[RangerPresenter] 차지 경직 해제");
        }

        private void CleanupAllMines()
        {
            while (_activeMines.Count > 0)
            {
                var mine = _activeMines.Dequeue();
                if (mine != null)
                {
                    mine.OnMineDestroyed -= HandleMineDestroyed;
                    Destroy(mine.gameObject);
                }
            }
            Debug.Log("[RangerPresenter] 모든 지뢰 정리 완료.");
        }

        /// <summary>
        /// Queue 에서 특정 Mine 을 제거한다.
        /// Queue{T} 는 중간 삭제를 지원하지 않으므로, 임시 버퍼로 재구성한다.
        /// </summary>
        private void RemoveMineFromQueue(RangerMine target)
        {
            int count = _activeMines.Count;
            for (int i = 0; i < count; i++)
            {
                var m = _activeMines.Dequeue();
                if (m != null && m != target)
                    _activeMines.Enqueue(m);
            }
        }
    }
}
