using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Enemies;

namespace Game.UI
{
    /// <summary>
    /// 보스 러시 HUD 런타임 View.
    ///
    /// [구성]
    ///   - 화면 상단: 보스 이름 + 페이즈 텍스트 + 대형 HP바 + 스테이지 텍스트
    ///   - 승리 패널: "보스 처치!" + 스테이지 정보 + "다음 스테이지" 버튼
    ///   - 클리어 패널: "ALL CLEAR!" 텍스트
    ///
    /// BossRushHudSetup.cs(Editor)가 이 컴포넌트를 자동으로 Canvas에 부착한다.
    /// </summary>
    public class BossRushHudView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("보스 HP 바 (상단)")]
        [SerializeField] private Image           _bossHpFill;
        [SerializeField] private TextMeshProUGUI _bossNameText;
        [SerializeField] private TextMeshProUGUI _bossPhaseText;
        [SerializeField] private TextMeshProUGUI _stageText;

        [Header("승리 패널")]
        [SerializeField] private GameObject      _victoryPanel;
        [SerializeField] private TextMeshProUGUI _victoryText;
        [SerializeField] private Button          _nextStageButton;

        [Header("클리어 패널")]
        [SerializeField] private GameObject      _allClearPanel;

        // ── 내부 참조 ─────────────────────────────────────────────
        private EnemyModel         _bossModel;
        private BossRushManager    _manager;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            _manager = GetComponentInParent<BossRushManager>();
            if (_manager == null)
                _manager = FindObjectOfType<BossRushManager>();

            if (_nextStageButton != null)
                _nextStageButton.onClick.AddListener(OnNextStageClicked);

            _victoryPanel?.SetActive(false);
            _allClearPanel?.SetActive(false);
        }

        // ── 외부 API ──────────────────────────────────────────────

        /// <summary>보스 스폰 시 호출. HP 구독 + UI 초기화.</summary>
        public void SetupBoss(string bossName, BossPresenterBase boss, int stage, int totalStages)
        {
            // 이전 구독 해제
            if (_bossModel != null)
            {
                _bossModel.OnHpChanged -= UpdateHp;
                _bossModel.OnDeath     -= OnBossModelDeath;
            }

            // 새 모델 구독 (Reflection 없이 공개 프로퍼티 접근)
            _bossModel = boss.GetModel();
            if (_bossModel != null)
            {
                _bossModel.OnHpChanged += UpdateHp;
                _bossModel.OnDeath     += OnBossModelDeath;
            }

            if (_bossHpFill  != null) _bossHpFill.fillAmount = 1f;
            if (_bossNameText != null) _bossNameText.text     = bossName.ToUpper();
            if (_bossPhaseText != null) _bossPhaseText.text   = "PHASE 1";
            if (_stageText    != null) _stageText.text        = $"STAGE {stage} / {totalStages}";
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

        /// <summary>보스 처치 후 승리 패널 표시.</summary>
        public void ShowVictoryPanel(int clearedStage, int totalStages)
        {
            if (_victoryPanel != null) _victoryPanel.SetActive(true);
            if (_victoryText  != null)
                _victoryText.text = $"STAGE {clearedStage} CLEAR!\n\n다음 보스를 소환하시겠습니까?";
        }

        public void HideVictoryPanel()
        {
            _victoryPanel?.SetActive(false);
        }

        /// <summary>전체 클리어 패널 표시.</summary>
        public void ShowAllClear()
        {
            _victoryPanel?.SetActive(false);
            _allClearPanel?.SetActive(true);
        }

        // ── 내부 ──────────────────────────────────────────────────
        private void UpdateHp(float ratio)
        {
            if (_bossHpFill != null)
                _bossHpFill.fillAmount = Mathf.Clamp01(ratio);
        }

        private void OnBossModelDeath()
        {
            if (_bossModel != null)
            {
                _bossModel.OnHpChanged -= UpdateHp;
                _bossModel.OnDeath     -= OnBossModelDeath;
            }
        }

        private void OnNextStageClicked()
        {
            if (_manager != null)
                _manager.SpawnNextBoss();
        }
    }
}
