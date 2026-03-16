using UnityEngine;
using Game.Core;

namespace Game.Enemies
{
    /// <summary>
    /// 일반 적 Presenter. EnemyPresenterBase를 그대로 사용하며,
    /// WaveManager의 오브젝트 풀에 의해 Spawn/재사용된다.
    /// 기존 EnemyPresenter와 동일한 동작.
    /// </summary>
    public class NormalEnemyPresenter : EnemyPresenterBase
    {
        // 현재는 베이스 동작 그대로 사용.
        // 일반 적 고유 로직이 필요할 경우 여기에 override 추가.
    }
}
