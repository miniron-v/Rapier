using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Enemies;

namespace Game.UI
{
    /// <summary>
    /// 보스 HUD 런타임 View.
    /// ProgressionManager, BossRushManager 등 보스를 스폰하는 모든 드라이버가 공용으로 사용.
    ///
    /// [구성]
    ///   - 화면 상단: 보스 이름 + 페이즈 텍스트 + 대형 HP바 + 스테이지 텍스트
    ///   - 승리 패널: "보스 처치!" + 스테이지 정보 + "다음 스테이지" 버튼
    ///   - 결과 패널: ALL CLEAR / GAME OVER 텍스트 + "로비로" 버튼
    ///
    /// [초기화]
    ///   BossHudSetup(Editor)이 Init()을 호출하여 모든 참조를 주입한다.
    ///   Reflection 미사용. [SerializeField]로 직렬화되어 씬에 저장됨.
    ///
    /// [이벤트]
    ///   OnNextStageRequested: 다음 스테이지 요청을 외부 드라이버에 위임 (HUD는 매니저 타입을 모른다).
    ///
    /// [게임 루프]
    ///   플레이어 사망 → ShowResult(false)
    ///   전체 클리어   → ShowResult(true)
    ///   "로비로" 버튼 → SceneController.LoadLobby()
    /// </summary>
    public class BossHudView : MonoBehaviour
    {
        // ── Inspector / Init으로 주입 ──────────────────────────────
        [Header("보스 HP 바 (상단)")]
        [SerializeField] private Image           _bossHpFill;
        [SerializeField] private TextMeshProUGUI _bossHpText;
        [SerializeField] private TextMeshProUGUI _bossNameText;
        [SerializeField] private TextMeshProUGUI _bossPhaseText;
        [SerializeField] private TextMeshProUGUI _stageText;

        [Header("승리 패널")]
        [SerializeField] private GameObject      _victoryPanel;
        [SerializeField] private TextMeshProUGUI _victoryText;
        [SerializeField] private Button          _nextStageButton;

        [Header("결과 패널 (클리어 / 게임오버)")]
        [SerializeField] private GameObject      _resultPanel;
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button          _toLobbyButton;

        // ── 이벤트 ────────────────────────────────────────────────
        /// <summary>
        /// 다음 스테이지 버튼 클릭 시 발행.
        /// 외부 드라이버(BossRushManager 등)가 구독하여 SpawnNextBoss 등을 처리한다.
        /// </summary>
        public event Action OnNextStageRequested;

        // ── 내부 참조 ─────────────────────────────────────────────
        private EnemyModel _bossModel;

        // ── 에디터 Setup 진입점 ───────────────────────────────────
        /// <summary>
        /// BossHudSetup이 호출하는 공개 초기화 메서드.
        /// Reflection 없이 직접 [SerializeField] 필드에 주입한다.
        /// </summary>
        public void Init(
            Image           bossHpFill,
            TextMeshProUGUI bossHpText,
            TextMeshProUGUI bossNameText,
            TextMeshProUGUI bossPhaseText,
            TextMeshProUGUI stageText,
            GameObject      victoryPanel,
            TextMeshProUGUI victoryText,
            Button          nextStageButton,
            GameObject      resultPanel,
            TextMeshProUGUI resultText,
            Button          toLobbyButton)
        {
            _bossHpFill      = bossHpFill;
            _bossHpText      = bossHpText;
            _bossNameText    = bossNameText;
            _bossPhaseText   = bossPhaseText;
            _stageText       = stageText;
            _victoryPanel    = victoryPanel;
            _victoryText     = victoryText;
            _nextStageButton = nextStageButton;
            _resultPanel     = resultPanel;
            _resultText      = resultText;
            _toLobbyButton   = toLobbyButton;
        }

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            if (_nextStageButton != null)
                _nextStageButton.onClick.AddListener(OnNextStageClicked);

            if (_toLobbyButton != null)
                _toLobbyButton.onClick.AddListener(OnToLobbyClicked);

            _victoryPanel?.SetActive(false);
            _resultPanel?.SetActive(false);
        }

        // ── 외부 API ──────────────────────────────────────────────

        /// <summary>보스 스폰 시 호출. HP 구독 + UI 초기화.</summary>
        public void SetupBoss(string bossName, BossPresenterBase boss, int stage, int totalStages)
        {
            if (_bossModel != null)
            {
                _bossModel.OnHpChanged -= UpdateHp;
                _bossModel.OnDeath     -= OnBossModelDeath;
            }

            _bossModel = boss.GetModel();
            if (_bossModel != null)
            {
                _bossModel.OnHpChanged += UpdateHp;
                _bossModel.OnDeath     += OnBossModelDeath;
            }

            if (_bossHpFill   != null) _bossHpFill.fillAmount  = 1f;
            if (_bossHpText   != null && _bossModel != null)
                _bossHpText.text = _bossModel.CurrentHp.ToString("F0");
            if (_bossNameText  != null) _bossNameText.text      = bossName.ToUpper();
            if (_bossPhaseText != null) _bossPhaseText.text     = "PHASE 1";
            if (_stageText     != null) _stageText.text         = $"STAGE {stage} / {totalStages}";
        }

        /// <summary>페이즈 변경 시 호출.</summary>
        public void UpdatePhase(BossPresenterBase.BossPhase phase)
        {
            if (_bossPhaseText == null) return;
            _bossPhaseText.text  = phase == BossPresenterBase.BossPhase.Phase2 ? "PHASE 2 !" : "PHASE 1";
            _bossPhaseText.color = phase == BossPresenterBase.BossPhase.Phase2
                ? new Color(1f, 0.5f, 0f)
                : Color.white;
        }

        /// <summary>스테이지 클리어 시 승리 패널 표시.</summary>
        public void ShowVictoryPanel(int clearedStage, int totalStages)
        {
            if (_victoryPanel != null) _victoryPanel.SetActive(true);
            if (_victoryText  != null)
                _victoryText.text = $"STAGE {clearedStage} CLEAR!\n\n다음 보스를 소환하시겠습니까?";
        }

        /// <summary>승리 패널 숨김.</summary>
        public void HideVictoryPanel()
        {
            _victoryPanel?.SetActive(false);
        }

        /// <summary>
        /// 결과 패널 표시.
        /// isCleared=true → ALL CLEAR / false → GAME OVER
        /// </summary>
        public void ShowResult(bool isCleared)
        {
            _victoryPanel?.SetActive(false);
            if (_resultPanel != null) _resultPanel.SetActive(true);
            if (_resultText  != null)
            {
                _resultText.text  = isCleared ? "ALL CLEAR!" : "GAME OVER";
                _resultText.color = isCleared ? Color.yellow : new Color(1f, 0.3f, 0.3f);
            }
        }

        /// <summary>결과 패널 숨김 (이어하기 진입 시 호출).</summary>
        public void HideResultPanel()
        {
            _resultPanel?.SetActive(false);
        }

        // ── 내부 ──────────────────────────────────────────────────
        private void UpdateHp(float ratio)
        {
            if (_bossHpFill != null)
                _bossHpFill.fillAmount = Mathf.Clamp01(ratio);
            if (_bossHpText != null && _bossModel != null)
                _bossHpText.text = _bossModel.CurrentHp.ToString("F0");
        }

        private void OnBossModelDeath()
        {
            if (_bossHpText != null)
                _bossHpText.text = "0";
            if (_bossModel != null)
            {
                _bossModel.OnHpChanged -= UpdateHp;
                _bossModel.OnDeath     -= OnBossModelDeath;
            }
        }

        private void OnNextStageClicked()
        {
            OnNextStageRequested?.Invoke();
        }

        private void OnToLobbyClicked()
        {
            SceneController.LoadLobby();
        }
    }
}
