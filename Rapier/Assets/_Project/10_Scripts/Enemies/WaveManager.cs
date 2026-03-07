using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// 웨이브 자동 진행 + 오브젝트 풀 관리.
    ///
    /// [규칙]
    ///   - 10초마다 적 ENEMIES_PER_WAVE마리 소환 (전투 종료 무관)
    ///   - 적은 스테이지 외곽 랜덤 위치에 스폰
    ///   - 비활성화된 Enemy를 재사용 (오브젝트 풀)
    ///   - ServiceLocator에 등록 → 외부에서 GetNearestEnemy() 접근 가능
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        // ── 인스펙터 ─────────────────────────────────────────────
        [Header("웨이브 설정")]
        [SerializeField] private EnemyStatData _enemyStatData;
        [SerializeField] private GameObject    _enemyPrefab;
        [SerializeField] private int   _enemiesPerWave  = 5;
        [SerializeField] private float _waveInterval    = 10f;

        [Header("스폰 영역 (스테이지 크기에 맞게 조정)")]
        [SerializeField] private float _spawnMargin = 1.5f; // 벽에서 얼마나 안쪽에 스폰

        // ── 내부 ─────────────────────────────────────────────────
        private readonly List<EnemyPresenter> _pool = new List<EnemyPresenter>();
        private StageBuilder _stage;
        private int _waveCount;

        // ── 라이프사이클 ─────────────────────────────────────────
        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void Start()
        {
            _stage = ServiceLocator.Get<StageBuilder>();
            PrewarmPool(_enemiesPerWave * 3); // 풀 사전 생성
            StartCoroutine(WaveRoutine());
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<WaveManager>();
        }

        // ── 웨이브 루틴 ──────────────────────────────────────────
        private IEnumerator WaveRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_waveInterval);
                SpawnWave();
            }
        }

        private void SpawnWave()
        {
            _waveCount++;
            for (int i = 0; i < _enemiesPerWave; i++)
                SpawnOne();

            UnityEngine.Debug.Log($"[WaveManager] Wave {_waveCount} 소환 ({_enemiesPerWave}마리)");
        }

        private void SpawnOne()
        {
            var enemy = GetFromPool();
            var pos   = GetRandomSpawnPosition();
            enemy.Spawn(_enemyStatData, pos);
        }

        // ── 스폰 위치 ────────────────────────────────────────────
        private Vector2 GetRandomSpawnPosition()
        {
            float hw = (_stage != null ? _stage.stageWidth  : 20f) * 0.5f - _spawnMargin;
            float hh = (_stage != null ? _stage.stageHeight : 30f) * 0.5f - _spawnMargin;

            // 스테이지 외곽 4방향 중 하나에서 랜덤 스폰
            int side = Random.Range(0, 4);
            return side switch
            {
                0 => new Vector2(Random.Range(-hw, hw),  hh),  // 위
                1 => new Vector2(Random.Range(-hw, hw), -hh),  // 아래
                2 => new Vector2(-hw, Random.Range(-hh, hh)),  // 왼쪽
                _ => new Vector2( hw, Random.Range(-hh, hh)),  // 오른쪽
            };
        }

        // ── 오브젝트 풀 ──────────────────────────────────────────
        private void PrewarmPool(int count)
        {
            for (int i = 0; i < count; i++)
                CreateEnemy();
        }

        private EnemyPresenter GetFromPool()
        {
            foreach (var e in _pool)
                if (!e.gameObject.activeSelf) return e;
            return CreateEnemy(); // 부족하면 확장
        }

        private EnemyPresenter CreateEnemy()
        {
            var go = Instantiate(_enemyPrefab, Vector3.zero, Quaternion.identity, transform);
            go.SetActive(false);
            var ep = go.GetComponent<EnemyPresenter>();
            _pool.Add(ep);
            return ep;
        }

        // ── 외부 API ─────────────────────────────────────────────
        /// <summary>활성 Enemy 중 fromPos에서 가장 가까운 것을 반환. 없으면 null.</summary>
        public EnemyPresenter GetNearestEnemy(Vector2 fromPos)
        {
            EnemyPresenter nearest = null;
            float minDist = float.MaxValue;
            foreach (var e in _pool)
            {
                if (!e.gameObject.activeSelf || !e.IsAlive) continue;
                float d = Vector2.Distance(fromPos, e.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = e;
                }
            }
            return nearest;
        }

        /// <summary>현재 활성(살아있는) 적 목록.</summary>
        public IReadOnlyList<EnemyPresenter> ActiveEnemies => _pool;
    }
}
