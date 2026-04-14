using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 인벤토리 스크롤 영역과 카테고리 3탭 사이에 위치하는 액션 바 View.
    /// 기본 상태: 우측 [분해] 버튼 + 좌측 가루 표시.
    /// 분해 모드: 좌측 [돌아가기], 우측 [일괄 선택 ▼] [분해하기] + 좌측 가루 표시.
    /// 로직 없음 — 표시만 담당.
    /// </summary>
    public class EquipmentActionBarView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("공통")]
        [SerializeField] private TextMeshProUGUI _dustText;

        [Header("기본 모드")]
        [SerializeField] private GameObject _defaultModeRoot;
        [SerializeField] private Button     _dismantleButton;

        [Header("분해 모드")]
        [SerializeField] private GameObject _dismantleModeRoot;
        [SerializeField] private Button     _backButton;
        [SerializeField] private Button     _bulkSelectButton;
        [SerializeField] private Button     _dosDismantleButton;
        [SerializeField] private TextMeshProUGUI _dosDismantleButtonText;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>[분해] 버튼 클릭 (기본 모드 → 분해 모드 진입 요청)</summary>
        public event Action OnDismantleEnterClicked;

        /// <summary>[돌아가기] 버튼 클릭 (분해 모드 종료 요청)</summary>
        public event Action OnBackClicked;

        /// <summary>[일괄 선택 ▼] 버튼 클릭</summary>
        public event Action OnBulkSelectClicked;

        /// <summary>[분해하기] 버튼 클릭</summary>
        public event Action OnDosDismantleClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (_dismantleButton     != null) _dismantleButton.onClick.AddListener(HandleDismantleEnterClicked);
            if (_backButton          != null) _backButton.onClick.AddListener(HandleBackClicked);
            if (_bulkSelectButton    != null) _bulkSelectButton.onClick.AddListener(HandleBulkSelectClicked);
            if (_dosDismantleButton  != null) _dosDismantleButton.onClick.AddListener(HandleDosDismantleClicked);
        }

        private void OnDestroy()
        {
            if (_dismantleButton     != null) _dismantleButton.onClick.RemoveListener(HandleDismantleEnterClicked);
            if (_backButton          != null) _backButton.onClick.RemoveListener(HandleBackClicked);
            if (_bulkSelectButton    != null) _bulkSelectButton.onClick.RemoveListener(HandleBulkSelectClicked);
            if (_dosDismantleButton  != null) _dosDismantleButton.onClick.RemoveListener(HandleDosDismantleClicked);
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>런타임 생성 시 SerializeField 참조를 주입한다 (LobbyHudSetup 호출).</summary>
        public void InitReferences(
            TextMeshProUGUI dustText,
            GameObject      defaultModeRoot,
            Button          dismantleButton,
            GameObject      dismantleModeRoot,
            Button          backButton,
            Button          bulkSelectButton,
            Button          dosDismantleButton,
            TextMeshProUGUI dosDismantleButtonText)
        {
            _dustText                = dustText;
            _defaultModeRoot         = defaultModeRoot;
            _dismantleButton         = dismantleButton;
            _dismantleModeRoot       = dismantleModeRoot;
            _backButton              = backButton;
            _bulkSelectButton        = bulkSelectButton;
            _dosDismantleButton      = dosDismantleButton;
            _dosDismantleButtonText  = dosDismantleButtonText;

            // 버튼 리스너 재등록 (InitReferences 가 Awake 이후 호출될 수 있으므로)
            if (_dismantleButton     != null) _dismantleButton.onClick.AddListener(HandleDismantleEnterClicked);
            if (_backButton          != null) _backButton.onClick.AddListener(HandleBackClicked);
            if (_bulkSelectButton    != null) _bulkSelectButton.onClick.AddListener(HandleBulkSelectClicked);
            if (_dosDismantleButton  != null) _dosDismantleButton.onClick.AddListener(HandleDosDismantleClicked);
        }

        // ── Public 메서드 (Presenter → View) ────────────────────────────────

        /// <summary>모드 전환 — 기본/분해 루트 활성화 토글.</summary>
        public void SetDismantleMode(bool isDismantle)
        {
            if (_defaultModeRoot   != null) _defaultModeRoot.SetActive(!isDismantle);
            if (_dismantleModeRoot != null) _dismantleModeRoot.SetActive(isDismantle);
        }

        /// <summary>가루 텍스트 갱신.</summary>
        public void SetDustText(int amount)
        {
            if (_dustText != null)
                _dustText.text = $"강화의 가루 ×{amount}";
        }

        /// <summary>[분해하기] 버튼 활성/비활성.</summary>
        public void SetDosDismantleInteractable(bool interactable)
        {
            if (_dosDismantleButton == null) return;
            _dosDismantleButton.interactable = interactable;
            if (_dosDismantleButtonText != null)
                _dosDismantleButtonText.color = interactable
                    ? Color.white
                    : new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleDismantleEnterClicked() => OnDismantleEnterClicked?.Invoke();
        private void HandleBackClicked()            => OnBackClicked?.Invoke();
        private void HandleBulkSelectClicked()      => OnBulkSelectClicked?.Invoke();
        private void HandleDosDismantleClicked()    => OnDosDismantleClicked?.Invoke();
    }
}
