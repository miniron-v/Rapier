using UnityEngine;
using Game.Core;
using Game.Input;

namespace Game.Characters
{
    /// <summary>
    /// 플레이어 공통 Presenter.
    /// CharacterPresenterBase의 첫 번째 구체 구현체.
    /// 캐릭터별 고유 메커니즘은 Phase 6에서 자식 클래스로 분리 예정.
    /// </summary>
    public class PlayerPresenter : CharacterPresenterBase
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

            // ServiceLocator에 등록
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<PlayerPresenter>();
        }

        // ── 입력 override (필요 시 확장) ─────────────────────────
        // 기본 이동/공격/회피는 CharacterPresenterBase가 처리.
        // Phase 6에서 캐릭터별 고유 메커니즘 추가 예정.
    }
}
