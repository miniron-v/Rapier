using UnityEngine;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// 방 단위로 생성되는 보스/미니언 등 transient 적 오브젝트의 공통 부모 컨테이너.
    ///
    /// [목적]
    ///   - 보스/미니언이 root GameObject 로 스폰되어 하이어라키 루트에 누적되는 것을 방지
    ///   - 방 전환 시 한 번에 Destroy 하기 위한 단일 엔트리 포인트
    ///
    /// [포함 대상]
    ///   - 보스 (ProgressionManager / BossRushManager 가 Instantiate)
    ///   - 보스가 소환한 미니언 (SummonAttackAction)
    ///
    /// [포함하지 않는 대상]
    ///   - WaveManager 풀의 일반 적: 풀 재사용 구조(재활성화)라 자기 transform 하위 유지
    ///
    /// [수명]
    ///   - 씬에 배치되어 있거나, 없으면 ProgressionManager/BossRushManager 가 런타임 생성
    ///   - 씬 언로드 시 Unity 가 자동 정리
    /// </summary>
    public class EnemyRoot : MonoBehaviour
    {
        private static EnemyRoot _instance;

        /// <summary>
        /// 싱글톤 접근. 씬에 없으면 런타임 생성한다.
        /// ServiceLocator 등록을 피하는 이유: 미니언 스폰은 [SerializeReference] 액션에서 수행되어
        /// Awake 타이밍 의존이 까다로움. 정적 인스턴스가 가장 단순.
        /// </summary>
        public static Transform Container
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<EnemyRoot>();
                    if (_instance == null)
                    {
                        var go = new GameObject("EnemyRoot");
                        _instance = go.AddComponent<EnemyRoot>();
                    }
                }
                return _instance.transform;
            }
        }

        /// <summary>컨테이너 하위의 모든 자식을 Destroy. 방 전환 시 호출.</summary>
        public static void ClearAll()
        {
            if (_instance == null) return;
            var t = _instance.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
