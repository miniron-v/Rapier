using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// 웨이브 자동 진행 + 오브젝트 풀 관리.
    /// NormalEnemyPresenter(EnemyPresenterBase 자식)를 사용.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("웨이브 설정")]
        [SerializeField] private EnemyStatData _enemyStatData;
        [SerializeField] private GameObject    _enemyPrefab;
        [SerializeField] private int   _enemiesPerWave  = 5;
        [SerializeField] private float _waveInterval    = 10f;

        [Header("스폰 영역")]
        [SerializeField] private float _spawnMargin = 1.5f;

        private readonly List<NormalEnemyPresenter> _pool = new List<NormalEnemyPresenter>();
        private StageBuilder _stage;
        private int _waveCount;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void Start()
        {
            _stage = ServiceLocator.Get<StageBuilder>();
            PrewarmPool(_enemiesPerWave * 3);
            StartCoroutine(WaveRoutine());
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<WaveManager>();
        }

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
            Debug.Log($"[WaveManager] Wave {_waveCount} 소환 ({_enemiesPerWave}마리)");
        }

        private void SpawnOne()
        {
            var enemy = GetFromPool();
            var pos   = GetRandomSpawnPosition();
            enemy.Spawn(_enemyStatData, pos);
        }

        private Vector2 GetRandomSpawnPosition()
        {
            float hw = (_stage != null ? _stage.stageWidth  : 20f) * 0.5f - _spawnMargin;
            float hh = (_stage != null ? _stage.stageHeight : 30f) * 0.5f - _spawnMargin;

            int side = Random.Range(0, 4);
            return side switch
            {
                0 => new Vector2(Random.Range(-hw, hw),  hh),
                1 => new Vector2(Random.Range(-hw, hw), -hh),
                2 => new Vector2(-hw, Random.Range(-hh, hh)),
                _ => new Vector2( hw, Random.Range(-hh, hh)),
            };
        }

        private void PrewarmPool(int count)
        {
            for (int i = 0; i < count; i++)
                CreateEnemy();
        }

        private NormalEnemyPresenter GetFromPool()
        {
            foreach (var e in _pool)
                if (!e.gameObject.activeSelf) return e;
            return CreateEnemy();
        }

        private NormalEnemyPresenter CreateEnemy()
        {
            var go = Instantiate(_enemyPrefab, Vector3.zero, Quaternion.identity, transform);
            go.SetActive(false);
            var ep = go.GetComponent<NormalEnemyPresenter>();
            _pool.Add(ep);
            return ep;
        }

        /// <summary>활성 Enemy 중 fromPos에서 가장 가까운 것을 반환. 없으면 null.</summary>
        public EnemyPresenterBase GetNearestEnemy(Vector2 fromPos)
        {
            EnemyPresenterBase nearest = null;
            float minDist = float.MaxValue;
            foreach (var e in _pool)
            {
                if (!e.gameObject.activeSelf || !e.IsAlive) continue;
                float d = Vector2.Distance(fromPos, e.transform.position);
                if (d < minDist) { minDist = d; nearest = e; }
            }
            return nearest;
        }

        public IReadOnlyList<NormalEnemyPresenter> ActiveEnemies => _pool;
    }
}
