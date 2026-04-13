using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Stage.Pause
{
    /// <summary>
    /// 일시정지 패널 뷰.
    ///
    /// [역할]
    ///   - 반투명 검정 오버레이 패널 표시/숨김.
    ///   - 재개(Resume) / 나가기(Lobby) 버튼 클릭 이벤트 발행.
    ///   - View에 로직 없음 — 클릭 시 이벤트만 발행, PausePanelPresenter가 처리.
    ///
    /// [이벤트 구독 쌍]
    ///   OnEnable  : Button.onClick 등록
    ///   OnDisable : Button.onClick 해제
    /// </summary>
    public class PausePanelView : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject _panel;

        [Header("버튼")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _exitToLobbyButton;

        // ── 이벤트 ───────────────────────────────────────────────────
        /// <summary>재개 버튼 클릭 시 발행. PausePanelPresenter가 구독.</summary>
        public event Action OnResumeClicked;

        /// <summary>나가기 버튼 클릭 시 발행. PausePanelPresenter가 구독.</summary>
        public event Action OnExitToLobbyClicked;

        // ── 구독 관리 ────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_resumeButton      != null) _resumeButton.onClick.AddListener(HandleResumeClicked);
            if (_exitToLobbyButton != null) _exitToLobbyButton.onClick.AddListener(HandleExitToLobbyClicked);
        }

        private void OnDisable()
        {
            if (_resumeButton      != null) _resumeButton.onClick.RemoveListener(HandleResumeClicked);
            if (_exitToLobbyButton != null) _exitToLobbyButton.onClick.RemoveListener(HandleExitToLobbyClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>일시정지 패널을 표시한다. Time.timeScale 제어는 Presenter가 담당.</summary>
        public void Show()
        {
            if (_panel != null) _panel.SetActive(true);
        }

        /// <summary>일시정지 패널을 숨긴다. Time.timeScale 제어는 Presenter가 담당.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        // ── 버튼 핸들러 ──────────────────────────────────────────────
        private void HandleResumeClicked()      => OnResumeClicked?.Invoke();
        private void HandleExitToLobbyClicked() => OnExitToLobbyClicked?.Invoke();
    }
}
