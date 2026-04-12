using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Data.Characters;

namespace Game.Characters.Assassin
{
    /// <summary>
    /// 어새신 캐릭터 Presenter.
    ///
    /// [잔상(Phantom) 관리]
    ///   저스트 회피 발동 시 회피 전 위치에 PhantomController를 Instantiate.
    ///   동시 최대 maxPhantoms 개. 초과 시 가장 오래된 잔상(인덱스 0) 제거.
    ///   잔상은 수명 만료 시 자동 페이드 아웃 후 Destroy.
    ///
    /// [동참 공격]
    ///   OnHitDamageable override: 본체 Tap 공격이 적을 맞혔을 때 활성 잔상 전부에
    ///   AttackWithPlayer(dir, ATK) 를 호출한다.
    ///   잔상 데미지 = ATK × (phantomDamagePercent / 100).
    ///
    /// [차지 스킬 — 360도 광역 베기]
    ///   OnSkillRelease(fullyCharged=true) 시 비동기 코루틴으로 360도 원형 광역 공격.
    ///   BeginSignatureSkill() + LockMovement() → 공격 → FreeMovement() + EndSignatureSkill().
    ///
    /// [잠금↔해제 매핑]
    ///   BeginSignatureSkill   ↔  EndSignatureSkill
    ///     · 정상: AoeSkillRoutine 완료 시 EndAoeSkillCleanup()
    ///     · 취소: OnBeforeDeath() → 코루틴 Stop → EndSignatureSkill 즉시
    ///     · OnDisable: StopAllCoroutines + EndSignatureSkill
    ///   LockMovement          ↔  FreeMovement
    ///     · 위와 동일 경로
    /// </summary>
    [RequireComponent(typeof(CharacterView))]
    public class AssassinPresenter : CharacterPresenterBase, IDamageable, IPlayerCharacter
    {
        // ── 직렬화 필드 ───────────────────────────────────────────
        [Header("데이터")]
        [SerializeField] private AssassinStatData _statData;

        [Header("잔상 프리팹")]
        [Tooltip("PhantomController가 부착된 프리팹. null이면 빈 GameObject로 대체.")]
        [SerializeField] private GameObject _phantomPrefab;

        // ── 런타임 비직렬화 필드 ──────────────────────────────────
        private CharacterView _view;

        [NonSerialized] private readonly List<PhantomController> _activePhantoms
            = new List<PhantomController>();

        [NonSerialized] private Coroutine _aoeSkillCoroutine;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            _view = GetComponent<CharacterView>();

            if (_statData == null)
            {
                Debug.LogError("[AssassinPresenter] AssassinStatData가 할당되지 않음.");
                return;
            }

            Init(_statData, _view);

            if (_statData.sprite != null)
                _view.SetSprite(_statData.sprite);

            ServiceLocator.Register<IPlayerCharacter>(this);
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ServiceLocator.Unregister<IPlayerCharacter>();
            ServiceLocator.Unregister<AssassinPresenter>();
        }

        // ── OnDisable — 씬 전환 / 오브젝트 비활성화 시 잔상 정리 ─
        protected override void OnDisable()
        {
            base.OnDisable();
            CleanupAllPhantoms();

            // 차지 스킬 코루틴이 진행 중이면 즉시 잠금 해제
            if (_aoeSkillCoroutine != null)
            {
                StopCoroutine(_aoeSkillCoroutine);
                _aoeSkillCoroutine = null;
                EndAoeSkillCleanup();
            }
        }

        // ── IDamageable / IPlayerCharacter ────────────────────────
        /// <inheritdoc/>
        public bool IsAlive => Model != null && Model.IsAlive;

        /// <inheritdoc/>
        public void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;

            if (JustDodgeAvailable)
            {
                ConsumeJustDodge();
                Debug.Log("[AssassinPresenter] 회피 중 피격 → 저스트 회피 발동!");
                Gesture?.TriggerJustDodge(knockbackDir * -1f);
                return;
            }

            if (Model.IsInvincible) return;

            Model.TakeDamage(amount);
            View.PlayHit();
        }

        /// <inheritdoc/>
        public CharacterModel PublicModel => Model;

        // ── DodgeDash 완료 콜백 ───────────────────────────────────
        protected override void OnDodgeDashComplete()
        {
            base.OnDodgeDashComplete();
            ConsumeJustDodge();
        }

        // ── 저스트 회피 훅 — 잔상 생성 ───────────────────────────
        /// <summary>
        /// 저스트 회피 발동 순간에 현재 위치(회피 전 위치)에 잔상을 생성한다.
        /// 활성 잔상이 maxPhantoms를 초과하면 가장 오래된 잔상(인덱스 0)을 먼저 제거한다.
        /// </summary>
        protected override void OnJustDodge(Vector2 direction)
        {
            Vector2 spawnPos = transform.position;

            // 초과 시 가장 오래된 잔상 제거
            if (_activePhantoms.Count >= _statData.MaxPhantoms)
            {
                var oldest = _activePhantoms[0];
                _activePhantoms.RemoveAt(0);
                if (oldest != null)
                    oldest.ForceDestroy();
            }

            // 잔상 생성
            var phantom = SpawnPhantom(spawnPos);
            if (phantom != null)
            {
                // 잔상 룬 효과 반영: 지속 시간 조정 (추후 룬 로직 연동 확장점)
                float duration = _statData.PhantomDuration;
                // TODO: 룬 효과 적용 시 duration *= (1f + phantomDurationBonus)

                var sourceSr = GetComponent<SpriteRenderer>();
                phantom.Init(sourceSr, duration, _statData.PhantomDamagePercent);
                phantom.OnPhantomExpired += HandlePhantomExpired;
                _activePhantoms.Add(phantom);

                Debug.Log($"[AssassinPresenter] 잔상 생성 @ {spawnPos}. 활성 잔상: {_activePhantoms.Count}");
            }
        }

        // ── 공격 히트 훅 — 잔상 동참 공격 ───────────────────────
        /// <summary>
        /// 본체 Tap 공격이 IDamageable을 맞혔을 때 호출된다.
        /// 활성 잔상 전부에 AttackWithPlayer를 전달한다.
        /// 공격 방향은 피격 타겟(EnemyPresenterBase)의 위치로부터 즉시 계산한다.
        /// </summary>
        protected override void OnHitDamageable(IDamageable target)
        {
            if (_activePhantoms.Count == 0) return;

            // 공격 방향: 타겟 위치 기반으로 즉시 계산.
            // target 이 EnemyPresenterBase 이면 정확한 방향, 아니면 기본 Vector2.up 사용.
            var enemy = target as EnemyPresenterBase;
            Vector2 attackDir = enemy != null
                ? ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized
                : Vector2.up;

            // 본체 ATK만 전달 — 잔상 내부에서 damagePercent 비율 적용
            float baseAtk = Model != null ? Model.AttackPower : 0f;

            for (int i = _activePhantoms.Count - 1; i >= 0; i--)
            {
                var phantom = _activePhantoms[i];
                if (phantom == null)
                {
                    _activePhantoms.RemoveAt(i);
                    continue;
                }
                phantom.AttackWithPlayer(attackDir, baseAtk);
            }
        }

        // ── 슬로우모션 종료 훅 ────────────────────────────────────
        protected override void OnSlowMotionEnd()
        {
            base.OnSlowMotionEnd();
        }

        // ── 사망 전처리 훅 ────────────────────────────────────────
        /// <summary>
        /// 사망 처리 직전 훅. 모든 활성 잔상을 즉시 파괴한다.
        /// 차지 스킬 코루틴이 진행 중이면 중단 후 잠금을 해제한다.
        /// </summary>
        protected override void OnBeforeDeath()
        {
            // 차지 스킬 코루틴 중단 + 잠금 해제
            if (_aoeSkillCoroutine != null)
            {
                StopCoroutine(_aoeSkillCoroutine);
                _aoeSkillCoroutine = null;
                EndAoeSkillCleanup();
            }

            CleanupAllPhantoms();
        }

        // ── 스킬 발동 훅 ──────────────────────────────────────────
        /// <summary>
        /// Hold → Release 시 Base에서 호출된다.
        /// fullyCharged=true : 차지 스킬(360도 광역 베기)를 비동기 코루틴으로 시작.
        /// justDodgeReady=true : Assassin은 저스트 회피 후 별도 고유 스킬 없음.
        ///   (잔상은 OnJustDodge에서 이미 생성됨. 추가 스킬 분기 불필요.)
        /// </summary>
        protected override void OnSkillRelease(bool fullyCharged, bool justDodgeReady)
        {
            if (fullyCharged)
            {
                // 차지 스킬: 360도 광역 베기. Base가 _isChargeSkillActive 플래그를 자동 해제하므로
                // 코루틴에서 추가 Tap 차단을 원하면 BeginSignatureSkill을 사용한다.
                BeginSignatureSkill();
                LockMovement();
                _aoeSkillCoroutine = StartCoroutine(AoeSkillRoutine());
            }
            // justDodgeReady 분기: Assassin은 별도 고유 스킬 없음 — 아무 것도 하지 않음.
        }

        // ── 360도 광역 베기 코루틴 ────────────────────────────────
        private IEnumerator AoeSkillRoutine()
        {
            // 짧은 선모션 (공격 예고)
            yield return new WaitForSecondsRealtime(0.15f);

            ExecuteAoeAttack();

            // 후딜 (공격 완료 후 짧은 경직)
            yield return new WaitForSecondsRealtime(0.25f);

            _aoeSkillCoroutine = null;
            EndAoeSkillCleanup();
        }

        private void ExecuteAoeAttack()
        {
            float radius     = _statData.AoeSkillRadius;
            float damage     = Model.AttackPower * (_statData.AoeSkillDamagePercent / 100f)
                               * Model.SkillDamageMultiplier;
            int   enemyLayer = LayerMask.GetMask("Enemy");

            var hits     = Physics2D.OverlapCircleAll(transform.position, radius, enemyLayer);
            int hitCount = 0;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponent<EnemyPresenterBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                var dir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.TakeDamage(damage, dir);
                hitCount++;
            }

            Debug.Log($"[AssassinPresenter] 360도 광역 베기 히트: {hitCount}명 / 반지름: {radius}");
        }

        /// <summary>
        /// 차지 스킬(AoeSkillRoutine) 종료 시 공통 뒷정리.
        /// EndSignatureSkill과 FreeMovement를 동시에 해제한다.
        /// </summary>
        private void EndAoeSkillCleanup()
        {
            EndSignatureSkill();
            FreeMovement();
        }

        // ── 잔상 스폰 ─────────────────────────────────────────────
        private PhantomController SpawnPhantom(Vector2 position)
        {
            GameObject go;
            if (_phantomPrefab != null)
            {
                go = Instantiate(_phantomPrefab, position, Quaternion.identity);
            }
            else
            {
                // 프리팹 미할당 시 빈 GameObject로 대체
                go = new GameObject("Phantom");
                go.transform.position = position;
            }

            var ctrl = go.GetComponent<PhantomController>();
            if (ctrl == null)
                ctrl = go.AddComponent<PhantomController>();

            return ctrl;
        }

        // ── 잔상 이벤트 핸들러 ────────────────────────────────────
        private void HandlePhantomExpired(PhantomController phantom)
        {
            _activePhantoms.Remove(phantom);
            Debug.Log($"[AssassinPresenter] 잔상 수명 만료. 잔여 활성 잔상: {_activePhantoms.Count}");
        }

        // ── 잔상 전체 정리 ────────────────────────────────────────
        private void CleanupAllPhantoms()
        {
            // 역순 순회로 안전하게 제거
            for (int i = _activePhantoms.Count - 1; i >= 0; i--)
            {
                var phantom = _activePhantoms[i];
                if (phantom != null)
                {
                    phantom.OnPhantomExpired -= HandlePhantomExpired;
                    phantom.ForceDestroy();
                }
            }
            _activePhantoms.Clear();
            Debug.Log("[AssassinPresenter] 모든 잔상 정리 완료.");
        }
    }
}
