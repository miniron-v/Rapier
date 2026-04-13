using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI.Stage.Pause
{
    /// <summary>
    /// 일시정지 Presenter.
    ///
    /// [역할]
    ///   - Pause 버튼 클릭 → Time.timeScale = 0f, PausePanelView.Show().
    ///   - 재개 클릭       → PausePanelView.Hide(), Time.timeScale = _prevTimeScale.
    ///   - 나가기 클릭     → PausePanelView.Hide(), Time.timeScale 복원 후 SceneController.LoadLobby().
    ///
    /// [timeScale 복원 정책]
    ///   Pause 진입 직전 Time.timeScale 을 _prevTimeScale 에 저장.
    ///   재개/나가기 모두 _prevTimeScale 로 복원.
    ///   SceneController.LoadLobby() 내부에서도 1f로 강제 복원하므로 이중 안전장치.
    ///
    /// [잠금/해제 쌍 검증]
    ///   Pause 진입: Time.timeScale = 0f  (HandlePauseClicked)
    ///   재개       : Time.timeScale = _prevTimeScale  (HandleResume)
    ///   나가기     : Time.timeScale = _prevTimeScale → SceneController.LoadLobby() 내부 1f 재설정
    ///   OnDisable  : IsPaused 상태면 timeScale 강제 복원 (씬 언로드/오브젝트 파괴 시 안전)
    ///
    /// [이벤트 구독 쌍]
    ///   OnEnable  : PausePanelView 이벤트 + PauseButton.onClick 등록
    ///   OnDisable : 구독 해제
    /// </summary>
    public class PausePanelPresenter : MonoBehaviour
    {
        [Header("뷰 참조")]
        [SerializeField] private PausePanelView _pausePanelView;

        [Header("Pause 버튼 (HUD 우상단)")]
        [SerializeField] private Button _pauseButton;

        // ── 내부 상태 ────────────────────────────────────────────────
        private float _prevTimeScale = 1f;

        /// <summary>현재 일시정지 상태 여부.</summary>
        public bool IsPaused { get; private set; }

        // ── 구독 관리 ────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_pauseButton    != null) _pauseButton.onClick.AddListener(HandlePauseClicked);

            if (_pausePanelView != null)
            {
                _pausePanelView.OnResumeClicked      += HandleResume;
                _pausePanelView.OnExitToLobbyClicked += HandleExitToLobby;
            }
        }

        private void OnDisable()
        {
            if (_pauseButton    != null) _pauseButton.onClick.RemoveListener(HandlePauseClicked);

            if (_pausePanelView != null)
            {
                _pausePanelView.OnResumeClicked      -= HandleResume;
                _pausePanelView.OnExitToLobbyClicked -= HandleExitToLobby;
            }

            // 씬 언로드 / 오브젝트 파괴 시 timeScale 복원 (안전장치)
            if (IsPaused)
            {
                Time.timeScale = _prevTimeScale;
                IsPaused = false;
            }
        }

        // ── 버튼 핸들러 ──────────────────────────────────────────────
        private void HandlePauseClicked()
        {
            if (IsPaused) return;

            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            IsPaused       = true;

            _pausePanelView?.Show();

            Debug.Log("[PausePanelPresenter] 일시정지. timeScale=0");
        }

        private void HandleResume()
        {
            if (!IsPaused) return;

            _pausePanelView?.Hide();
            Time.timeScale = _prevTimeScale;
            IsPaused       = false;

            Debug.Log($"[PausePanelPresenter] 재개. timeScale={_prevTimeScale}");
        }

        private void HandleExitToLobby()
        {
            // IsPaused 여부와 관계없이 항상 timeScale 복원 후 로비 전환
            _pausePanelView?.Hide();
            Time.timeScale = _prevTimeScale; // 선제 복원 (SceneController.LoadLobby도 내부에서 1f 설정)
            IsPaused       = false;

            Debug.Log("[PausePanelPresenter] 나가기 → 로비 복귀. timeScale 복원 후 LoadLobby.");
            SceneController.LoadLobby();
        }
    }
}
