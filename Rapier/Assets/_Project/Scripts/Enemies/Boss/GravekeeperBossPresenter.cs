using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 그레이브키퍼 보스.
    ///
    /// [1페이즈] Summon → Melee → 반복
    /// [2페이즈] Summon → Summon → Melee → Melee → 반복 (HP 50% 이하)
    ///
    /// [미니언 관리]
    ///   SummonAttackAction 은 [SerializeReference] 제약으로 UnityEngine.Object 를 직접
    ///   보유하지 못하므로, 이 Presenter 가 [SerializeField] 로 minionPrefab/minionData 를
    ///   보유하고 Spawn() 시점에 SummonAttackAction.MinionPrefab/MinionData 에 주입한다.
    ///
    /// [종료 경로 — 미니언]
    ///   1. 플레이어에게 처치 → NormalEnemyPresenter.HandleModelDeath() → _activeMinions 제거
    ///   2. 보스 사망 → HandleModelDeath() → CleanupMinions()
    ///
    /// [코루틴/구독 짝]
    ///   Spawn 시: SummonAttackAction.OnMinionSpawned += HandleMinionSpawned
    ///   HandleModelDeath 시: SummonAttackAction.OnMinionSpawned -= HandleMinionSpawned
    /// </summary>
    public class GravekeeperBossPresenter : BossPresenterBase
    {
        [Header("미니언 설정")]
        [Tooltip("소환할 미니언 프리팹 (NormalEnemyPresenter 컴포넌트 포함)")]
        [SerializeField] private NormalEnemyPresenter _minionPrefab;
        [Tooltip("소환할 미니언의 스탯 데이터 SO")]
        [SerializeField] private EnemyStatData        _minionData;

        // 활성 미니언 목록
        private readonly List<NormalEnemyPresenter> _activeMinions = new List<NormalEnemyPresenter>();

        // 구독 중인 SummonAttackAction 목록 (해제 시 사용)
        private readonly List<SummonAttackAction> _summonActions = new List<SummonAttackAction>();

        // ── Spawn: 의존성 주입 + 구독 등록 ──────────────────────
        public override void Spawn(EnemyStatData statData, Vector2 position)
        {
            _activeMinions.Clear();
            UnsubscribeSummonEvents();
            _summonActions.Clear();

            base.Spawn(statData, position);

            // attackSequence + phase2Sequence 의 SummonAttackAction 에 refs 주입 + 구독
            InjectAndSubscribe(statData.attackSequence);
            if (BossData != null)
                InjectAndSubscribe(BossData.phase2Sequence);
        }

        // ── 사망 override ─────────────────────────────────────────
        protected override void HandleModelDeath()
        {
            CleanupMinions();
            UnsubscribeSummonEvents();
            base.HandleModelDeath();
        }

        protected override void OnEnterPhase2()
        {
            Debug.Log("[GravekeeperBoss] Phase2 진입 — 소환 강화.");
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────
        private void HandleMinionSpawned(NormalEnemyPresenter minion)
        {
            if (minion == null) return;
            _activeMinions.Add(minion);
            minion.OnDeath += () => _activeMinions.Remove(minion);
        }

        // ── 주입 + 구독 ───────────────────────────────────────────
        private void InjectAndSubscribe(List<EnemyAttackAction> seq)
        {
            if (seq == null) return;
            foreach (var action in seq)
            {
                if (action is SummonAttackAction summon)
                {
                    summon.MinionPrefab       = _minionPrefab;
                    summon.MinionData         = _minionData;
                    summon.OnMinionSpawned   += HandleMinionSpawned;
                    _summonActions.Add(summon);
                }
            }
        }

        private void UnsubscribeSummonEvents()
        {
            foreach (var summon in _summonActions)
            {
                if (summon != null)
                    summon.OnMinionSpawned -= HandleMinionSpawned;
            }
            _summonActions.Clear();
        }

        // ── 미니언 일괄 제거 ──────────────────────────────────────
        private void CleanupMinions()
        {
            foreach (var minion in _activeMinions)
            {
                if (minion != null)
                    Destroy(minion.gameObject);
            }
            _activeMinions.Clear();
        }
    }
}
