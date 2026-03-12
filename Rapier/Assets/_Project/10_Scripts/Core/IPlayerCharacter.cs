using UnityEngine;
using Game.Combat;
using Game.Characters;

namespace Game.Core
{
    /// <summary>
    /// 플레이어 캐릭터 공통 인터페이스.
    ///
    /// [도입 배경]
    ///   캐릭터가 4종으로 확장되면서 HudView / EnemyPresenter 등 외부 시스템이
    ///   PlayerPresenter라는 구체 타입에 직접 의존하는 문제를 해결하기 위해 도입.
    ///   각 캐릭터 Presenter는 이 인터페이스를 구현하고
    ///   ServiceLocator.Register<IPlayerCharacter>(this) 로 등록한다.
    ///
    /// [사용 측]
    ///   ServiceLocator.Get<IPlayerCharacter>() 로 참조.
    ///   HudView, EnemyPresenter 등 외부 시스템은 이 인터페이스만 알면 된다.
    /// </summary>
    public interface IPlayerCharacter : IDamageable
    {
        /// <summary>캐릭터 Model — HudView가 HP/Charge/DodgeCooldown 이벤트 구독에 사용.</summary>
        CharacterModel PublicModel { get; }

        /// <summary>캐릭터 Transform — EnemyPresenter 추적에 사용.</summary>
        Transform transform { get; }
    }
}
