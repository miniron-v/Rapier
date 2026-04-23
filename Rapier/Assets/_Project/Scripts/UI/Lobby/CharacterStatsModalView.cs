using System;
using Game.Data.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 캐릭터 스탯 보기 모달 View.
    /// 9행 스탯 표 + 닫기 버튼 + 딤머(클릭 닫기)를 표시한다.
    /// 로직 없음 — 표시 및 이벤트 발행만 담당. (MVP View 규칙)
    /// </summary>
    public class CharacterStatsModalView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("딤머")]
        [SerializeField] private Button _dimmerButton;

        [Header("제목")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("스탯 행 (이름 / 값 쌍, 9개)")]
        [SerializeField] private TextMeshProUGUI _labelHp;
        [SerializeField] private TextMeshProUGUI _valueHp;

        [SerializeField] private TextMeshProUGUI _labelAtk;
        [SerializeField] private TextMeshProUGUI _valueAtk;

        [SerializeField] private TextMeshProUGUI _labelMs;
        [SerializeField] private TextMeshProUGUI _valueMs;

        [SerializeField] private TextMeshProUGUI _labelCritChance;
        [SerializeField] private TextMeshProUGUI _valueCritChance;

        [SerializeField] private TextMeshProUGUI _labelCritDamage;
        [SerializeField] private TextMeshProUGUI _valueCritDamage;

        [SerializeField] private TextMeshProUGUI _labelSkillDamage;
        [SerializeField] private TextMeshProUGUI _valueSkillDamage;

        [SerializeField] private TextMeshProUGUI _labelDodgeCdr;
        [SerializeField] private TextMeshProUGUI _valueDodgeCdr;

        [SerializeField] private TextMeshProUGUI _labelChargeTime;
        [SerializeField] private TextMeshProUGUI _valueChargeTime;

        [SerializeField] private TextMeshProUGUI _labelInvinc;
        [SerializeField] private TextMeshProUGUI _valueInvinc;

        [Header("닫기 버튼")]
        [SerializeField] private Button _closeButton;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>닫기 버튼 또는 딤머 클릭 시 발행.</summary>
        public event Action OnCloseClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (_closeButton  != null) _closeButton.onClick.AddListener(HandleCloseClicked);
            if (_dimmerButton != null) _dimmerButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDestroy()
        {
            if (_closeButton  != null) _closeButton.onClick.RemoveListener(HandleCloseClicked);
            if (_dimmerButton != null) _dimmerButton.onClick.RemoveListener(HandleCloseClicked);
        }

        // ── 초기화 ───────────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 런타임 참조를 주입한다.</summary>
        public void InitReferences(
            Button           dimmerButton,
            TextMeshProUGUI  titleText,
            TextMeshProUGUI  labelHp,    TextMeshProUGUI valueHp,
            TextMeshProUGUI  labelAtk,   TextMeshProUGUI valueAtk,
            TextMeshProUGUI  labelMs,    TextMeshProUGUI valueMs,
            TextMeshProUGUI  labelCritChance,  TextMeshProUGUI valueCritChance,
            TextMeshProUGUI  labelCritDamage,  TextMeshProUGUI valueCritDamage,
            TextMeshProUGUI  labelSkillDamage, TextMeshProUGUI valueSkillDamage,
            TextMeshProUGUI  labelDodgeCdr,    TextMeshProUGUI valueDodgeCdr,
            TextMeshProUGUI  labelChargeTime,  TextMeshProUGUI valueChargeTime,
            TextMeshProUGUI  labelInvinc,      TextMeshProUGUI valueInvinc,
            Button           closeButton)
        {
            _dimmerButton    = dimmerButton;
            _titleText       = titleText;
            _labelHp         = labelHp;         _valueHp         = valueHp;
            _labelAtk        = labelAtk;        _valueAtk        = valueAtk;
            _labelMs         = labelMs;         _valueMs         = valueMs;
            _labelCritChance = labelCritChance; _valueCritChance = valueCritChance;
            _labelCritDamage = labelCritDamage; _valueCritDamage = valueCritDamage;
            _labelSkillDamage= labelSkillDamage;_valueSkillDamage= valueSkillDamage;
            _labelDodgeCdr   = labelDodgeCdr;   _valueDodgeCdr   = valueDodgeCdr;
            _labelChargeTime = labelChargeTime; _valueChargeTime = valueChargeTime;
            _labelInvinc     = labelInvinc;     _valueInvinc     = valueInvinc;
            _closeButton     = closeButton;

            if (_closeButton  != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
                _closeButton.onClick.AddListener(HandleCloseClicked);
            }
            if (_dimmerButton != null)
            {
                _dimmerButton.onClick.RemoveListener(HandleCloseClicked);
                _dimmerButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        // ── Public 메서드 ────────────────────────────────────────────────────

        /// <summary>스냅샷 데이터를 채우고 모달을 표시한다.</summary>
        public void Show(CharacterStatSnapshot snap)
        {
            if (snap == null) return;

            SetText(_labelHp,          "HP");
            SetText(_valueHp,          $"{snap.Hp:F0}");

            SetText(_labelAtk,         "공격력");
            SetText(_valueAtk,         $"{snap.Atk:F0}");

            SetText(_labelMs,          "이동속도");
            SetText(_valueMs,          $"{snap.MoveSpeed:F2}");

            SetText(_labelCritChance,  "치명타 확률");
            SetText(_valueCritChance,  $"{snap.CritChancePercent:F1}%");

            SetText(_labelCritDamage,  "치명타 피해");
            SetText(_valueCritDamage,  $"{snap.CritDamagePercent:F1}%");

            SetText(_labelSkillDamage, "스킬 피해");
            SetText(_valueSkillDamage, $"{snap.SkillDamagePercent:F1}%");

            SetText(_labelDodgeCdr,    "회피 쿨타임");
            SetText(_valueDodgeCdr,    $"{snap.DodgeCdrReductionPercent:F1}%");

            SetText(_labelChargeTime,  "차지 시간");
            SetText(_valueChargeTime,  $"{snap.ChargeTimeReductionPercent:F1}%");

            SetText(_labelInvinc,      "무적 시간");
            SetText(_valueInvinc,      $"{snap.InvincReductionPercent:F1}%");

            gameObject.SetActive(true);
        }

        /// <summary>모달을 숨긴다.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ── Private Methods ──────────────────────────────────────────────────

        private static void SetText(TextMeshProUGUI tmp, string text)
        {
            if (tmp != null) tmp.text = text;
        }

        private void HandleCloseClicked()
        {
            OnCloseClicked?.Invoke();
        }
    }
}
