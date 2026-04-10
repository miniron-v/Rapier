using System;
using System.Collections;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 미니언 소환 공격 (Gravekeeper 전용).
    ///
    /// Gravekeeper 사망 시 GravekeeperBossPresenter.CleanupMinions()로 일괄 제거.
    ///
    /// [Unity [SerializeReference] 제약]
    ///   [SerializeReference] 로 직렬화된 managed 객체는 UnityEngine.Object 레퍼런스를
    ///   직접 보유할 수 없다. 따라서 minionPrefab / minionData 는 SerializeReference로
    ///   저장되지 않는다.
    ///   대신 GravekeeperBossPresenter 가 [SerializeField] 로 minionPrefab / minionData 를
    ///   보유하고, Execute 호출 전에 SetMinionRefs() 로 주입한다.
    ///
    /// [종료 경로 — 미니언]
    ///   1. 플레이어에게 처치 → 자체 사망 처리
    ///   2. 보스 사망 → GravekeeperBossPresenter.CleanupMinions()
    /// </summary>
    [Serializable]
    public class SummonAttackAction : EnemyAttackAction
    {
        [Tooltip("소환 수")]
        public int minionCount = 2;
        [Tooltip("소환 위치 반경 (보스 주변 원형 배치)")]
        public float spawnRadius = 2f;

        // GravekeeperBossPresenter 에서 주입 (UnityEngine.Object 레퍼런스는 SerializeReference 불가)
        [NonSerialized] public NormalEnemyPresenter MinionPrefab;
        [NonSerialized] public EnemyStatData        MinionData;

        /// <summary>미니언 소환 시 외부(GravekeeperBossPresenter)에서 수신하여 목록 관리.</summary>
        [field: NonSerialized]
        public event Action<NormalEnemyPresenter> OnMinionSpawned;

        public override IEnumerator Execute(EnemyAttackContext ctx, Action onComplete)
        {
            if (MinionPrefab != null && MinionData != null)
            {
                for (int i = 0; i < minionCount; i++)
                {
                    float angle  = (360f / minionCount) * i * Mathf.Deg2Rad;
                    var   offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
                    var   pos    = (Vector2)ctx.SelfTransform.position + offset;

                    var minion = UnityEngine.Object.Instantiate(MinionPrefab);
                    minion.Spawn(MinionData, pos);
                    OnMinionSpawned?.Invoke(minion);
                }
            }
            else
            {
                Debug.LogWarning("[SummonAttackAction] MinionPrefab 또는 MinionData 가 주입되지 않았음. " +
                                 "GravekeeperBossPresenter.InjectMinionRefs() 호출 확인.");
            }

            onComplete?.Invoke();
            yield break;
        }
    }
}
