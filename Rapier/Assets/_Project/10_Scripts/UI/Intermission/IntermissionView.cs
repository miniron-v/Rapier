using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Data.RunStats;

namespace Game.UI.Intermission
{
    /// <summary>
    /// 인터미션 방 UI 뷰.
    ///
    /// [역할]
    ///   - 스탯 카드 2개를 표시하고 사용자 선택을 IntermissionManager에 전달.
    ///   - HP 회복은 ProgressionManager가 담당하므로 여기서는 안내 텍스트만 표시.
    ///
    /// [이벤트]
    ///   OnStatSelected: 카드 선택 시 발행 (RunStatEntry 전달).
    ///
    /// [CLAUDE.md §2]
    ///   View에 로직 없음 — 선택 버튼 클릭 시 이벤트만 발행.
    ///
    /// [이벤트 구독 쌍]
    ///   OnEnable  : Button.onClick 등록
    ///   OnDisable : Button.onClick 해제
    /// </summary>
    public class IntermissionView : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject _panel;

        [Header("카드 A")]
        [SerializeField] private Button          _cardAButton;
        [SerializeField] private TextMeshProUGUI _cardATitle;
        [SerializeField] private TextMeshProUGUI _cardADesc;

        [Header("카드 B")]
        [SerializeField] private Button          _cardBButton;
        [SerializeField] private TextMeshProUGUI _cardBTitle;
        [SerializeField] private TextMeshProUGUI _cardBDesc;

        [Header("안내 텍스트")]
        [SerializeField] private TextMeshProUGUI _healNoticeText;

        // ── 이벤트 ───────────────────────────────────────────────────
        /// <summary>카드 선택 완료 시 발행. IntermissionManager가 구독.</summary>
        public event Action<RunStatEntry> OnStatSelected;

        // ── 내부 상태 ────────────────────────────────────────────────
        private RunStatEntry _entryA;
        private RunStatEntry _entryB;

        // ── 구독 관리 ────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_cardAButton != null) _cardAButton.onClick.AddListener(HandleCardAClicked);
            if (_cardBButton != null) _cardBButton.onClick.AddListener(HandleCardBClicked);
        }

        private void OnDisable()
        {
            if (_cardAButton != null) _cardAButton.onClick.RemoveListener(HandleCardAClicked);
            if (_cardBButton != null) _cardBButton.onClick.RemoveListener(HandleCardBClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>
        /// 두 스탯 카드를 표시하고 패널을 연다.
        /// </summary>
        public void Show(RunStatEntry entryA, RunStatEntry entryB)
        {
            _entryA = entryA;
            _entryB = entryB;

            if (_cardATitle != null) _cardATitle.text = entryA.DisplayName;
            if (_cardADesc  != null) _cardADesc.text  = entryA.Description;
            if (_cardBTitle != null) _cardBTitle.text = entryB.DisplayName;
            if (_cardBDesc  != null) _cardBDesc.text  = entryB.Description;

            if (_healNoticeText != null)
                _healNoticeText.text = "HP가 100% 회복되었습니다!";

            if (_panel != null) _panel.SetActive(true);

            // 전투 일시 정지
            Time.timeScale = 0f;
        }

        /// <summary>패널을 닫고 게임 시간을 복구한다.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            Time.timeScale = 1f;
        }

        // ── 버튼 핸들러 ──────────────────────────────────────────────
        private void HandleCardAClicked()
        {
            OnStatSelected?.Invoke(_entryA);
        }

        private void HandleCardBClicked()
        {
            OnStatSelected?.Invoke(_entryB);
        }
    }
}
