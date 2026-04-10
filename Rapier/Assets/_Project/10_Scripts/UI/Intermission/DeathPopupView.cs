using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Intermission
{
    /// <summary>
    /// 플레이어 사망 시 표시되는 이어하기 / 로비 복귀 팝업 뷰.
    ///
    /// [DesignDoc §8-4]
    ///   - 이어하기: RunStat 유지, 현재 보스 방에서 재시작
    ///   - 로비 복귀: 진행도 + RunStat 초기화
    ///
    /// [CLAUDE.md §2]
    ///   View에 로직 없음 — 버튼 클릭 시 이벤트만 발행.
    ///
    /// [이벤트 구독 쌍]
    ///   OnEnable  : Button.onClick 등록
    ///   OnDisable : Button.onClick 해제
    /// </summary>
    public class DeathPopupView : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject      _panel;

        [Header("버튼")]
        [SerializeField] private Button          _continueButton;
        [SerializeField] private Button          _returnToLobbyButton;

        [Header("텍스트")]
        [SerializeField] private TextMeshProUGUI _titleText;

        // ── 이벤트 ───────────────────────────────────────────────────
        /// <summary>이어하기 버튼 클릭 시 발행. IntermissionManager가 구독.</summary>
        public event Action OnContinueClicked;

        /// <summary>로비 복귀 버튼 클릭 시 발행. IntermissionManager가 구독.</summary>
        public event Action OnReturnToLobbyClicked;

        // ── 구독 관리 ────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_continueButton      != null) _continueButton.onClick.AddListener(HandleContinueClicked);
            if (_returnToLobbyButton != null) _returnToLobbyButton.onClick.AddListener(HandleReturnToLobbyClicked);
        }

        private void OnDisable()
        {
            if (_continueButton      != null) _continueButton.onClick.RemoveListener(HandleContinueClicked);
            if (_returnToLobbyButton != null) _returnToLobbyButton.onClick.RemoveListener(HandleReturnToLobbyClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>사망 팝업을 표시하고 게임 시간을 정지한다.</summary>
        public void Show()
        {
            if (_titleText != null) _titleText.text = "전투 불능!";
            if (_panel     != null) _panel.SetActive(true);
            Time.timeScale = 0f;
        }

        /// <summary>사망 팝업을 닫고 게임 시간을 복구한다.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            Time.timeScale = 1f;
        }

        // ── 버튼 핸들러 ──────────────────────────────────────────────
        private void HandleContinueClicked()      => OnContinueClicked?.Invoke();
        private void HandleReturnToLobbyClicked() => OnReturnToLobbyClicked?.Invoke();
    }
}
