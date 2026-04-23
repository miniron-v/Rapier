using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 캐릭터 탭 — 캐릭터 정보 패널 View.
    ///
    /// [Phase 23c]
    ///   - 중앙 일러스트 (빈 Image, Raycast Target off)
    ///   - 하단 "캐릭터 변경" 버튼
    ///   - 좌측 세로 3칸 슬롯 (Weapon / Necklace / Ring)
    ///   - 우측 세로 5칸 슬롯 (Hat / Top / Bottom / Gloves / Shoes)
    ///
    /// 로직 없음 — 표시 전용 (MVP View 규칙).
    /// </summary>
    public class CharacterInfoPanelView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("일러스트")]
        [SerializeField] private Image _illustrationImage;

        [Header("버튼")]
        [SerializeField] private Button _changeCharacterButton;
        [SerializeField] private Button _statsButton;

        [Header("좌측 슬롯 (Weapon/Necklace/Ring)")]
        [SerializeField] private Equipment.EquipmentSlotView _leftSlot0; // Weapon
        [SerializeField] private Equipment.EquipmentSlotView _leftSlot1; // Necklace
        [SerializeField] private Equipment.EquipmentSlotView _leftSlot2; // Ring

        [Header("우측 슬롯 (Hat/Top/Bottom/Gloves/Shoes)")]
        [SerializeField] private Equipment.EquipmentSlotView _rightSlot0; // Hat
        [SerializeField] private Equipment.EquipmentSlotView _rightSlot1; // Top
        [SerializeField] private Equipment.EquipmentSlotView _rightSlot2; // Bottom
        [SerializeField] private Equipment.EquipmentSlotView _rightSlot3; // Gloves
        [SerializeField] private Equipment.EquipmentSlotView _rightSlot4; // Shoes

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>캐릭터 변경 버튼 클릭 이벤트.</summary>
        public event Action OnChangeCharacterClicked;

        /// <summary>스탯 보기 버튼 클릭 이벤트.</summary>
        public event Action OnStatsClicked;

        // ── Properties ──────────────────────────────────────────────────────

        /// <summary>좌측 3슬롯: 무기, 목걸이, 반지 순.</summary>
        public Equipment.EquipmentSlotView[] LeftSlots
            => new[] { _leftSlot0, _leftSlot1, _leftSlot2 };

        /// <summary>우측 5슬롯: 모자, 상의, 하의, 장갑, 신발 순.</summary>
        public Equipment.EquipmentSlotView[] RightSlots
            => new[] { _rightSlot0, _rightSlot1, _rightSlot2, _rightSlot3, _rightSlot4 };

        // ── 초기화 ───────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_changeCharacterButton != null)
                _changeCharacterButton.onClick.AddListener(HandleChangeClicked);
            if (_statsButton != null)
                _statsButton.onClick.AddListener(HandleStatsClicked);
        }

        private void OnDestroy()
        {
            if (_changeCharacterButton != null)
                _changeCharacterButton.onClick.RemoveListener(HandleChangeClicked);
            if (_statsButton != null)
                _statsButton.onClick.RemoveListener(HandleStatsClicked);
        }

        /// <summary>런타임 생성 시 참조 주입 (LobbyHudSetup 에서 호출).</summary>
        public void InitReferences(
            Image illustrationImage,
            Button changeCharacterButton,
            Button statsButton,
            Equipment.EquipmentSlotView leftSlot0,
            Equipment.EquipmentSlotView leftSlot1,
            Equipment.EquipmentSlotView leftSlot2,
            Equipment.EquipmentSlotView rightSlot0,
            Equipment.EquipmentSlotView rightSlot1,
            Equipment.EquipmentSlotView rightSlot2,
            Equipment.EquipmentSlotView rightSlot3,
            Equipment.EquipmentSlotView rightSlot4)
        {
            _illustrationImage     = illustrationImage;
            _changeCharacterButton = changeCharacterButton;
            _statsButton           = statsButton;
            _leftSlot0 = leftSlot0;  _leftSlot1 = leftSlot1;  _leftSlot2 = leftSlot2;
            _rightSlot0 = rightSlot0; _rightSlot1 = rightSlot1; _rightSlot2 = rightSlot2;
            _rightSlot3 = rightSlot3; _rightSlot4 = rightSlot4;

            // 버튼 리스너 재등록
            if (_changeCharacterButton != null)
            {
                _changeCharacterButton.onClick.RemoveListener(HandleChangeClicked);
                _changeCharacterButton.onClick.AddListener(HandleChangeClicked);
            }
            if (_statsButton != null)
            {
                _statsButton.onClick.RemoveListener(HandleStatsClicked);
                _statsButton.onClick.AddListener(HandleStatsClicked);
            }
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>일러스트 스프라이트를 설정한다 (null 허용 — Private repo 주입 전).</summary>
        public void SetIllustration(Sprite sprite)
        {
            if (_illustrationImage == null) return;
            _illustrationImage.sprite         = sprite;
            _illustrationImage.preserveAspect = true;
            _illustrationImage.color          = sprite != null ? Color.white : new Color(0f, 0f, 0f, 0f);
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private void HandleChangeClicked()
        {
            OnChangeCharacterClicked?.Invoke();
        }

        private void HandleStatsClicked()
        {
            OnStatsClicked?.Invoke();
        }
    }
}
