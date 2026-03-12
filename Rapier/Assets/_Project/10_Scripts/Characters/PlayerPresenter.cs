using UnityEngine;
using Game.Core;
using Game.Input;
using Game.Combat;

namespace Game.Characters
{
    /// <summary>
    /// 플레이어 공통 Presenter (기본 캐릭터).
    /// CharacterPresenterBase의 첫 번째 구체 구현체.
    ///
    /// [ServiceLocator 등록]
    ///   IPlayerCharacter 인터페이스로 등록하여
    ///   HudView, EnemyPresenter 등 외부 시스템이 구체 타입 없이 참조 가능.
    ///
    /// [TakeDamage 흐름]
    ///   무적 중(회피 중) → 피해 무시 + JustDodge 트리거
    ///   그 외            → 피해 적용
    /// </summary>
    public class PlayerPresenter : CharacterPresenterBase, IDamageable, IPlayerCharacter
    {
        [Header("데이터")]
        [SerializeField] private CharacterStatData _statData;

        private CharacterView _view;

        private void Awake()
        {
            _view = GetComponent<CharacterView>();

            if (_statData == null)
            {
                Debug.LogError("[PlayerPresenter] CharacterStatData가 할당되지 않음.");
                return;
            }

            Init(_statData, _view);

            if (_statData.sprite != null)
                _view.SetSprite(_statData.sprite);

            ServiceLocator.Register<IPlayerCharacter>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IPlayerCharacter>();
        }

        // ── IDamageable / IPlayerCharacter 구현 ──────────────────
        public bool IsAlive => Model != null && Model.IsAlive;

        public void TakeDamage(float amount, Vector2 knockbackDir)
        {
            if (!IsAlive) return;

            if (Model.IsInvincible)
            {
                Debug.Log("[PlayerPresenter] 무적 중 피격 → JustDodge 트리거!");
                Gesture?.ForceJustDodge(knockbackDir * -1f);
                return;
            }

            Model.TakeDamage(amount);
            View.PlayHit();
        }

        /// <summary>HudView 등 외부에서 Model 이벤트를 구독할 수 있도록 노출.</summary>
        public CharacterModel PublicModel => Model;
    }
}
