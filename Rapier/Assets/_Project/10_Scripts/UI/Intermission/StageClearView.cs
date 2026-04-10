using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Intermission
{
    /// <summary>
    /// 스테이지 클리어 결과 화면 뷰.
    ///
    /// [DesignDoc §8-4]
    ///   스테이지 클리어 후 → 로비 복귀 / 다음 스테이지 진입 선택.
    ///   RunStat은 StageManager가 이미 초기화한 상태.
    ///
    /// [CLAUDE.md §2]
    ///   View에 로직 없음 — 버튼 클릭 시 이벤트만 발행.
    ///
    /// [이벤트 구독 쌍]
    ///   OnEnable  : Button.onClick 등록
    ///   OnDisable : Button.onClick 해제
    /// </summary>
    public class StageClearView : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject      _panel;

        [Header("버튼")]
        [SerializeField] private Button          _returnToLobbyButton;
        [SerializeField] private Button          _nextStageButton;

        [Header("텍스트")]
        [SerializeField] private TextMeshProUGUI _titleText;

        // ── 이벤트 ───────────────────────────────────────────────────
        /// <summary>로비 복귀 버튼 클릭 시 발행.</summary>
        public event Action OnReturnToLobbyClicked;

        /// <summary>다음 스테이지 버튼 클릭 시 발행.</summary>
        public event Action OnNextStageClicked;

        // ── 구독 관리 ────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_returnToLobbyButton != null) _returnToLobbyButton.onClick.AddListener(HandleReturnToLobbyClicked);
            if (_nextStageButton     != null) _nextStageButton.onClick.AddListener(HandleNextStageClicked);
        }

        private void OnDisable()
        {
            if (_returnToLobbyButton != null) _returnToLobbyButton.onClick.RemoveListener(HandleReturnToLobbyClicked);
            if (_nextStageButton     != null) _nextStageButton.onClick.RemoveListener(HandleNextStageClicked);
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>클리어 화면을 표시하고 게임 시간을 정지한다.</summary>
        public void Show()
        {
            if (_titleText != null) _titleText.text = "STAGE CLEAR!";
            if (_panel     != null) _panel.SetActive(true);
            Time.timeScale = 0f;
        }

        /// <summary>클리어 화면을 닫고 게임 시간을 복구한다.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            Time.timeScale = 1f;
        }

        // ── 버튼 핸들러 ──────────────────────────────────────────────
        private void HandleReturnToLobbyClicked() => OnReturnToLobbyClicked?.Invoke();
        private void HandleNextStageClicked()      => OnNextStageClicked?.Invoke();
    }
}
