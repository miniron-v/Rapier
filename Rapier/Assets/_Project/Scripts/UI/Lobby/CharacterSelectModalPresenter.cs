using System;
using Game.Characters;
using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 캐릭터 변경 모달 Presenter.
    ///
    /// [Phase 23c]
    ///   - CharacterSelectModalView 관리
    ///   - 캐릭터 목록 순환 (Rapier / Assassin / Warrior(잠금) / Ranger(잠금))
    ///   - 선택하기 → OnCharacterSelected 이벤트 발행 + 모달 닫기
    ///   - CharacterInfoPanelPresenter 에서 구독
    ///
    /// ── 캐릭터 순서 (index) ────────────────────────────────────────
    ///   0: Rapier   (구현됨)
    ///   1: Assassin (구현됨)
    ///   2: Warrior  (잠금)
    ///   3: Ranger   (잠금)
    /// </summary>
    public class CharacterSelectModalPresenter : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private CharacterSelectModalView _view;

        [Header("캐릭터 StatData")]
        [SerializeField] private CharacterStatData _rapierData;
        [SerializeField] private CharacterStatData _assassinData;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>선택하기 버튼으로 캐릭터가 선택됐을 때 발행 (characterId).</summary>
        public event Action<string> OnCharacterSelected;

        // ── 캐릭터 목록 ──────────────────────────────────────────────────────

        private static readonly string[] CHARACTER_IDS =
        {
            "Rapier", "Assassin", "Warrior", "Ranger"
        };

        private static readonly string[] CHARACTER_NAMES =
        {
            "레이피어", "어쌔신", "전사", "레인저"
        };

        // 구현된 캐릭터 인덱스 (false = 잠금)
        private static readonly bool[] CHARACTER_UNLOCKED =
        {
            true, true, false, false
        };

        private int _currentIndex;

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 호출.</summary>
        public void InitReferences(
            CharacterSelectModalView view,
            CharacterStatData        rapierData,
            CharacterStatData        assassinData)
        {
            _view         = view;
            _rapierData   = rapierData;
            _assassinData = assassinData;
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>모달을 열고 지정 캐릭터로 초기화한다.</summary>
        public void OpenModal(string currentCharacterId)
        {
            if (_view == null) return;

            _currentIndex = GetIndexById(currentCharacterId);

            // 이벤트 구독
            _view.OnSelectClicked -= HandleSelectClicked;
            _view.OnSelectClicked += HandleSelectClicked;
            _view.OnPrevClicked   -= HandlePrevClicked;
            _view.OnPrevClicked   += HandlePrevClicked;
            _view.OnNextClicked   -= HandleNextClicked;
            _view.OnNextClicked   += HandleNextClicked;

            RefreshView();
            _view.Show();
        }

        /// <summary>모달을 닫는다 (이벤트 구독 해제 포함).</summary>
        public void CloseModal()
        {
            if (_view == null) return;

            _view.OnSelectClicked -= HandleSelectClicked;
            _view.OnPrevClicked   -= HandlePrevClicked;
            _view.OnNextClicked   -= HandleNextClicked;

            _view.Hide();
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private void RefreshView()
        {
            if (_view == null) return;

            string id          = CHARACTER_IDS[_currentIndex];
            string name        = CHARACTER_NAMES[_currentIndex];
            bool   isLocked    = !CHARACTER_UNLOCKED[_currentIndex];
            var    data        = GetStatData(id);
            string description = isLocked ? "Coming Soon" : (data?.description ?? "");
            var    sprite      = data?.sprite;

            _view.RefreshCharacter(name, description, sprite, isLocked);
        }

        private void HandleSelectClicked()
        {
            if (!CHARACTER_UNLOCKED[_currentIndex]) return;

            string id = CHARACTER_IDS[_currentIndex];
            CloseModal();
            OnCharacterSelected?.Invoke(id);
        }

        private void HandlePrevClicked()
        {
            _currentIndex = (_currentIndex - 1 + CHARACTER_IDS.Length) % CHARACTER_IDS.Length;
            RefreshView();
        }

        private void HandleNextClicked()
        {
            _currentIndex = (_currentIndex + 1) % CHARACTER_IDS.Length;
            RefreshView();
        }

        private CharacterStatData GetStatData(string characterId)
        {
            return characterId switch
            {
                "Rapier"   => _rapierData,
                "Assassin" => _assassinData,
                _          => null
            };
        }

        private static int GetIndexById(string characterId)
        {
            for (int i = 0; i < CHARACTER_IDS.Length; i++)
                if (CHARACTER_IDS[i] == characterId) return i;
            return 0;
        }
    }
}
