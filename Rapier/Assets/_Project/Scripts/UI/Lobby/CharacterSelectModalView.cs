using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 캐릭터 변경 모달 View.
    ///
    /// [Phase 23c]
    ///   - ContentArea 전체를 덮는 오버레이
    ///   - 중앙 일러스트 (빈 Image, Raycast Target off)
    ///   - 하단 반투명 설명 패널 + 캐릭터 이름 + 설명 텍스트
    ///   - "선택하기" 버튼
    ///   - 좌/우 화살표 버튼으로 캐릭터 전환
    ///   - 잠금 오버레이 (미구현 캐릭터용)
    ///
    /// 로직 없음 — 표시 전용 (MVP View 규칙).
    /// </summary>
    public class CharacterSelectModalView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("일러스트")]
        [SerializeField] private Image _illustrationImage;

        [Header("설명 패널")]
        [SerializeField] private TextMeshProUGUI _characterNameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;

        [Header("버튼")]
        [SerializeField] private Button _selectButton;
        [SerializeField] private Button _leftArrowButton;
        [SerializeField] private Button _rightArrowButton;

        [Header("잠금 오버레이")]
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private TextMeshProUGUI _lockText;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>선택하기 버튼 클릭.</summary>
        public event Action OnSelectClicked;

        /// <summary>이전 캐릭터로 전환 (좌 화살표).</summary>
        public event Action OnPrevClicked;

        /// <summary>다음 캐릭터로 전환 (우 화살표).</summary>
        public event Action OnNextClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            RegisterButtonListeners();
        }

        private void OnDestroy()
        {
            UnregisterButtonListeners();
        }

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>런타임 생성 시 참조 주입 (CharacterInfoPanelSetup 에서 호출).</summary>
        public void InitReferences(
            Image            illustrationImage,
            TextMeshProUGUI  characterNameText,
            TextMeshProUGUI  descriptionText,
            Button           selectButton,
            Button           leftArrowButton,
            Button           rightArrowButton,
            GameObject       lockOverlay,
            TextMeshProUGUI  lockText)
        {
            _illustrationImage  = illustrationImage;
            _characterNameText  = characterNameText;
            _descriptionText    = descriptionText;
            _selectButton       = selectButton;
            _leftArrowButton    = leftArrowButton;
            _rightArrowButton   = rightArrowButton;
            _lockOverlay        = lockOverlay;
            _lockText           = lockText;

            UnregisterButtonListeners();
            RegisterButtonListeners();
        }

        // ── Public Methods ───────────────────────────────────────────────────

        /// <summary>모달을 표시한다.</summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary>모달을 숨긴다.</summary>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>캐릭터 정보를 갱신한다.</summary>
        /// <param name="characterName">캐릭터 이름.</param>
        /// <param name="description">캐릭터 설명 텍스트.</param>
        /// <param name="illustration">일러스트 스프라이트 (null 허용).</param>
        /// <param name="isLocked">미구현 캐릭터 잠금 여부.</param>
        public void RefreshCharacter(string characterName, string description, Sprite illustration, bool isLocked)
        {
            if (_characterNameText != null)
                _characterNameText.text = characterName;

            if (_descriptionText != null)
                _descriptionText.text = description;

            if (_illustrationImage != null)
            {
                _illustrationImage.sprite         = illustration;
                _illustrationImage.preserveAspect = true;
                _illustrationImage.color          = illustration != null ? Color.white : new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }

            // 잠금 오버레이
            if (_lockOverlay != null)
                _lockOverlay.SetActive(isLocked);

            // 선택하기 버튼 — 잠금 시 비활성
            if (_selectButton != null)
                _selectButton.interactable = !isLocked;
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private void RegisterButtonListeners()
        {
            if (_selectButton    != null) _selectButton.onClick.AddListener(HandleSelectClicked);
            if (_leftArrowButton != null) _leftArrowButton.onClick.AddListener(HandlePrevClicked);
            if (_rightArrowButton != null) _rightArrowButton.onClick.AddListener(HandleNextClicked);
        }

        private void UnregisterButtonListeners()
        {
            if (_selectButton    != null) _selectButton.onClick.RemoveListener(HandleSelectClicked);
            if (_leftArrowButton != null) _leftArrowButton.onClick.RemoveListener(HandlePrevClicked);
            if (_rightArrowButton != null) _rightArrowButton.onClick.RemoveListener(HandleNextClicked);
        }

        private void HandleSelectClicked() => OnSelectClicked?.Invoke();
        private void HandlePrevClicked()   => OnPrevClicked?.Invoke();
        private void HandleNextClicked()   => OnNextClicked?.Invoke();
    }
}
