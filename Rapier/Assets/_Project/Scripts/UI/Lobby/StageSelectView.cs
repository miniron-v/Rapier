using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 스테이지 선택 패널 View.
    ///
    /// [역할]
    ///   - Show(unlockedCount) 호출 시 스테이지 버튼 목록 갱신
    ///   - 스테이지 선택 → OnStageSelected 이벤트 발행
    ///   - 닫기 버튼 → OnCloseClicked 이벤트 발행
    ///
    /// [CLAUDE.md §2]
    ///   View에 로직 없음. 버튼 클릭 시 이벤트만 발행.
    /// </summary>
    public class StageSelectView : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject _panel;

        [Header("버튼")]
        [SerializeField] private Button     _closeButton;

        [Header("스테이지 버튼 (Inspector에서 순서대로 연결)")]
        [Tooltip("10개의 스테이지 버튼 배열. 순서 = 스테이지 1~10.")]
        [SerializeField] private Button[]     _stageButtons;
        [SerializeField] private TMP_Text[]   _stageButtonLabels;

        // ── 이벤트 ───────────────────────────────────────────────────
        /// <summary>스테이지 선택 시 발행. 인자 = 1-based 스테이지 인덱스.</summary>
        public event Action<int> OnStageSelected;

        /// <summary>닫기 버튼 클릭 시 발행.</summary>
        public event Action OnCloseClicked;

        // ── 구독 관리 ────────────────────────────────────────────────
        private readonly List<UnityEngine.Events.UnityAction> _stageHandlers = new();

        private void Awake()
        {
            // 스테이지 버튼 핸들러 미리 캐싱
            _stageHandlers.Clear();
            if (_stageButtons != null)
            {
                for (int i = 0; i < _stageButtons.Length; i++)
                {
                    int captured = i + 1; // 1-based
                    _stageHandlers.Add(() => OnStageSelected?.Invoke(captured));
                }
            }
        }

        private void OnEnable()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(HandleCloseClicked);

            if (_stageButtons != null)
            {
                for (int i = 0; i < _stageButtons.Length && i < _stageHandlers.Count; i++)
                {
                    if (_stageButtons[i] != null)
                        _stageButtons[i].onClick.AddListener(_stageHandlers[i]);
                }
            }
        }

        private void OnDisable()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(HandleCloseClicked);

            if (_stageButtons != null)
            {
                for (int i = 0; i < _stageButtons.Length && i < _stageHandlers.Count; i++)
                {
                    if (_stageButtons[i] != null)
                        _stageButtons[i].onClick.RemoveListener(_stageHandlers[i]);
                }
            }
        }

        // ── 공개 API ─────────────────────────────────────────────────
        /// <summary>
        /// 패널을 열고 해금된 스테이지까지 버튼을 활성화한다.
        /// </summary>
        /// <param name="unlockedCount">선택 가능한 최대 스테이지 번호 (1-based).</param>
        public void Show(int unlockedCount)
        {
            if (_panel != null) _panel.SetActive(true);

            if (_stageButtons == null) return;
            for (int i = 0; i < _stageButtons.Length; i++)
            {
                if (_stageButtons[i] == null) continue;
                bool unlocked = (i + 1) <= unlockedCount;
                _stageButtons[i].interactable = unlocked;

                if (_stageButtonLabels != null && i < _stageButtonLabels.Length && _stageButtonLabels[i] != null)
                    _stageButtonLabels[i].text = unlocked ? $"Stage {i + 1}" : $"Stage {i + 1}\n[LOCKED]";
            }
        }

        /// <summary>패널을 닫는다.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        // ── 핸들러 ───────────────────────────────────────────────────
        private void HandleCloseClicked() => OnCloseClicked?.Invoke();
    }
}
