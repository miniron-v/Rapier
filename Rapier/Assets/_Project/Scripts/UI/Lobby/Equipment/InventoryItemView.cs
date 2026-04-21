using System;
using System.Collections;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 인벤토리 내 단일 장비 아이템 UI.
    /// 아이콘, 등급 색상 배경, 분해 모드 시각화(알파/테두리/자물쇠/게이지)를 표시한다.
    /// 로직 없음 — 표시만 담당. (MVP View 규칙)
    ///
    /// Phase 25-B 변경:
    ///   - 분해 모드 시각화 (alpha, 등급 테두리, 자물쇠 오버레이)
    ///   - 롱프레스 0.5초 게이지 (원형) → LongPressed 이벤트
    ///   - 분해 모드 탭 → DismantleToggled 이벤트 (장착 중이면 무반응)
    /// </summary>
    public class InventoryItemView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        // ── 상수 ─────────────────────────────────────────────────────────────

        private const float LONG_PRESS_HOLD_DELAY = 0.3f;  // 게이지 시작 전 홀드 대기
        private const float LONG_PRESS_DURATION   = 0.5f;  // 게이지 완충까지 시간
        private const float DARK_MULTIPLIER       = 0.45f; // 비활성 슬롯 어두움 배율

        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private Image _itemIcon;
        [SerializeField] private Image _gradeBackground;
        [SerializeField] private Button _itemButton;

        [Header("분해 모드 시각화")]
        [SerializeField] private Image _lockOverlay;        // (미사용 — 하위 호환용 유지)
        [SerializeField] private Image _longPressGauge;     // Filled 원형 게이지

        [Header("장착/잠금 표시")]
        [SerializeField] private Image            _equippedBorder;    // 연두색 테두리
        [SerializeField] private TextMeshProUGUI  _equippedBadgeText; // "E" 텍스트
        [SerializeField] private Image            _lockIcon;           // 자물쇠 아이콘

        // ── Private Fields ───────────────────────────────────────────────────

        private EquipmentInstance _instance;
        private bool _isDismantleMode;
        private bool _isSelected;
        private bool _isEquipped;
        private bool _isLocked;

        private Color _originalIconColor;
        private Color _originalBgColor;

        private Coroutine _longPressRoutine;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>일반 탭 클릭 이벤트 (분해 모드 아닐 때).</summary>
        public event Action<EquipmentInstance> OnClicked;

        /// <summary>분해 모드 탭 → 선택 토글 이벤트.</summary>
        public event Action<EquipmentInstance> OnDismantleToggled;

        /// <summary>롱프레스 완료 이벤트 (0.5초 게이지 완충).</summary>
        public event Action<EquipmentInstance> OnLongPressed;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (_itemButton != null)
                _itemButton.onClick.AddListener(HandleButtonClicked);

            // 기본 상태 — 게이지/오버레이 비활성
            if (_longPressGauge != null) { _longPressGauge.fillAmount = 0f; _longPressGauge.gameObject.SetActive(false); }
            if (_lockOverlay    != null) _lockOverlay.gameObject.SetActive(false);
            if (_equippedBorder    != null) _equippedBorder.gameObject.SetActive(false);
            if (_equippedBadgeText != null) _equippedBadgeText.gameObject.SetActive(false);
            if (_lockIcon          != null) _lockIcon.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_itemButton != null)
                _itemButton.onClick.RemoveListener(HandleButtonClicked);
            CancelLongPress();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>
        /// 런타임 생성 시 SerializeField 참조를 외부에서 주입한다 (LobbyHudSetup 에서 호출).
        /// </summary>
        public void InitReferences(Image itemIcon, Image gradeBackground, Button itemButton)
        {
            _itemIcon        = itemIcon;
            _gradeBackground = gradeBackground;
            _itemButton      = itemButton;
            if (_itemButton != null)
                _itemButton.onClick.AddListener(HandleButtonClicked);
        }

        /// <summary>장착/잠금 표시용 참조 주입 (LobbyHudSetup에서 호출).</summary>
        public void InitEquippedLockReferences(Image equippedBorder, TextMeshProUGUI equippedBadgeText, Image lockIcon)
        {
            _equippedBorder    = equippedBorder;
            _equippedBadgeText = equippedBadgeText;
            _lockIcon          = lockIcon;

            if (_equippedBorder    != null) _equippedBorder.gameObject.SetActive(false);
            if (_equippedBadgeText != null) _equippedBadgeText.gameObject.SetActive(false);
            if (_lockIcon          != null) _lockIcon.gameObject.SetActive(false);
        }

        /// <summary>분해 모드용 추가 참조 주입.</summary>
        public void InitDismantleReferences(Image selectionBorder, Image lockOverlay, Image longPressGauge)
        {
            _lockOverlay    = lockOverlay;
            _longPressGauge = longPressGauge;

            if (_longPressGauge != null) { _longPressGauge.fillAmount = 0f; _longPressGauge.gameObject.SetActive(false); }
            if (_lockOverlay    != null) _lockOverlay.gameObject.SetActive(false);
        }

        // ── Public 메서드 ────────────────────────────────────────────────────

        /// <summary>장비 인스턴스를 기반으로 UI를 갱신한다.</summary>
        public void Refresh(EquipmentInstance instance)
        {
            _instance = instance;

            if (instance == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            _itemIcon.sprite = instance.Data.Icon;

            if (ColorUtility.TryParseHtmlString(
                    EquipmentGradeHelper.GetGradeColorHex(instance.Data.Grade),
                    out var gradeColor))
                _gradeBackground.color = gradeColor;

            // 원래 색 캐싱 — 항상 최신 등급 색/흰색 기준으로 고정
            _originalIconColor = Color.white;
            _originalBgColor   = _gradeBackground.color;

            // 아이콘/배경만 갱신. 장착·잠금 상태 시각화는 SetDismantleMode → RefreshDismantleVisuals 에서 처리.
        }

        /// <summary>분해 모드 진입/종료 시 시각화 갱신.</summary>
        public void SetDismantleMode(bool isDismantle, bool isEquipped, bool isLocked)
        {
            _isDismantleMode = isDismantle;
            _isEquipped      = isEquipped;
            _isLocked        = isLocked;

            if (!isDismantle)
            {
                _isSelected = false;
                CancelLongPress();
            }

            RefreshDismantleVisuals();
        }

        /// <summary>분해 선택 상태 갱신.</summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            RefreshDismantleVisuals();
        }

        // ── IPointerDownHandler / IPointerUpHandler ──────────────────────────

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_instance == null) return;
            if (!_isDismantleMode) return;

            CancelLongPress();
            _longPressRoutine = StartCoroutine(LongPressRoutine());
        }

        /// <inheritdoc/>
        public void OnPointerUp(PointerEventData eventData)
        {
            CancelLongPress();
        }

        // ── Private 메서드 ────────────────────────────────────────────────────

        private void RefreshDismantleVisuals()
        {
            if (!_isDismantleMode)
            {
                // 일반 모드: 원래 색 복구 + E 배지 / 잠금 아이콘 표시
                RestoreColor();
                if (_itemButton        != null) _itemButton.interactable = true;
                if (_equippedBorder    != null) _equippedBorder.gameObject.SetActive(_isEquipped);
                if (_equippedBadgeText != null) _equippedBadgeText.gameObject.SetActive(_isEquipped);
                if (_lockIcon          != null) _lockIcon.gameObject.SetActive(_isLocked);
                return;
            }

            // 분해 모드 — 장착 중
            if (_isEquipped)
            {
                SetDark();
                if (_itemButton        != null) _itemButton.interactable = false;
                if (_equippedBorder    != null) _equippedBorder.gameObject.SetActive(true);
                if (_equippedBadgeText != null) _equippedBadgeText.gameObject.SetActive(true);
                if (_lockIcon          != null) _lockIcon.gameObject.SetActive(_isLocked);
            }
            else if (_isLocked)
            {
                // 분해 모드 — 잠김 (미장착)
                SetDark();
                if (_itemButton        != null) _itemButton.interactable = false;
                if (_equippedBorder    != null) _equippedBorder.gameObject.SetActive(false);
                if (_equippedBadgeText != null) _equippedBadgeText.gameObject.SetActive(false);
                if (_lockIcon          != null) _lockIcon.gameObject.SetActive(true);
            }
            else if (_isSelected)
            {
                // 분해 모드 — 선택됨: 원래 색 복구
                RestoreColor();
                if (_itemButton        != null) _itemButton.interactable = true;
                if (_equippedBorder    != null) _equippedBorder.gameObject.SetActive(false);
                if (_equippedBadgeText != null) _equippedBadgeText.gameObject.SetActive(false);
                if (_lockIcon          != null) _lockIcon.gameObject.SetActive(false);
            }
            else
            {
                // 분해 모드 — 미선택 + 미장착 + 미잠금
                SetDark();
                if (_itemButton        != null) _itemButton.interactable = true;
                if (_equippedBorder    != null) _equippedBorder.gameObject.SetActive(false);
                if (_equippedBadgeText != null) _equippedBadgeText.gameObject.SetActive(false);
                if (_lockIcon          != null) _lockIcon.gameObject.SetActive(false);
            }
        }

        private void RestoreColor()
        {
            if (_itemIcon        != null) _itemIcon.color        = _originalIconColor;
            if (_gradeBackground != null) _gradeBackground.color = _originalBgColor;
        }

        private void SetDark()
        {
            if (_itemIcon != null)
                _itemIcon.color = new Color(
                    _originalIconColor.r * DARK_MULTIPLIER,
                    _originalIconColor.g * DARK_MULTIPLIER,
                    _originalIconColor.b * DARK_MULTIPLIER,
                    _originalIconColor.a);
            if (_gradeBackground != null)
                _gradeBackground.color = new Color(
                    _originalBgColor.r * DARK_MULTIPLIER,
                    _originalBgColor.g * DARK_MULTIPLIER,
                    _originalBgColor.b * DARK_MULTIPLIER,
                    _originalBgColor.a);
        }

        private void CancelLongPress()
        {
            if (_longPressRoutine != null)
            {
                StopCoroutine(_longPressRoutine);
                _longPressRoutine = null;
            }
            if (_longPressGauge != null)
            {
                _longPressGauge.fillAmount = 0f;
                _longPressGauge.gameObject.SetActive(false);
            }
        }

        private IEnumerator LongPressRoutine()
        {
            // 0.3초 홀드 대기 — 이 구간엔 게이지 미표시
            float holdElapsed = 0f;
            while (holdElapsed < LONG_PRESS_HOLD_DELAY)
            {
                holdElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 0.3초 초과 → 게이지 표시 시작
            if (_longPressGauge != null)
            {
                _longPressGauge.fillAmount = 0f;
                _longPressGauge.gameObject.SetActive(true);
            }

            float elapsed = 0f;
            while (elapsed < LONG_PRESS_DURATION)
            {
                elapsed += Time.unscaledDeltaTime;
                float ratio = Mathf.Clamp01(elapsed / LONG_PRESS_DURATION);
                if (_longPressGauge != null)
                    _longPressGauge.fillAmount = ratio;
                yield return null;
            }

            // 게이지 완충 → 롱프레스 이벤트
            if (_longPressGauge != null)
            {
                _longPressGauge.fillAmount = 0f;
                _longPressGauge.gameObject.SetActive(false);
            }
            _longPressRoutine = null;
            OnLongPressed?.Invoke(_instance);
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleButtonClicked()
        {
            if (_instance == null) return;

            if (_isDismantleMode)
            {
                // 분해 모드: 장착 중이거나 잠긴 아이템이면 무반응
                if (_isEquipped || _isLocked) return;
                OnDismantleToggled?.Invoke(_instance);
            }
            else
            {
                OnClicked?.Invoke(_instance);
            }
        }
    }
}
