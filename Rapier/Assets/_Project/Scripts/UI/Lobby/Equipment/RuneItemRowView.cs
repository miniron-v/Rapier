using System;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 룬 인벤토리 팝업의 단일 룬 행 View.
    /// 룬 아이콘, 이름, 효과 요약을 표시한다.
    /// 로직 없음 — 표시 및 클릭 이벤트만 담당. (MVP View 규칙)
    /// </summary>
    public class RuneItemRowView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private Image           _runeIcon;
        [SerializeField] private TextMeshProUGUI _runeNameText;
        [SerializeField] private TextMeshProUGUI _effectText;
        [SerializeField] private Button          _rowButton;

        // ── 내부 상태 ────────────────────────────────────────────────────────

        private RuneItemData _rune;

        // ── 이벤트 ──────────────────────────────────────────────────────────

        /// <summary>행 클릭 이벤트 (룬 데이터 전달)</summary>
        public event Action<RuneItemData> OnClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _rowButton?.onClick.AddListener(HandleButtonClicked);
        }

        private void OnDestroy()
        {
            _rowButton?.onClick.RemoveAllListeners();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 참조를 주입한다.</summary>
        public void InitReferences(Image icon, TextMeshProUGUI nameText, TextMeshProUGUI effectText, Button rowButton)
        {
            _runeIcon      = icon;
            _runeNameText  = nameText;
            _effectText    = effectText;
            _rowButton     = rowButton;

            _rowButton?.onClick.RemoveAllListeners();
            _rowButton?.onClick.AddListener(HandleButtonClicked);
        }

        // ── Public 메서드 ────────────────────────────────────────────────────

        /// <summary>룬 데이터를 기반으로 행을 갱신한다.</summary>
        public void Refresh(RuneItemData rune)
        {
            _rune = rune;
            if (rune == null)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_runeIcon != null)
            {
                if (rune.Icon != null)
                {
                    _runeIcon.sprite = rune.Icon;
                    _runeIcon.color  = Color.white;
                }
                else
                {
                    _runeIcon.color = Color.cyan;
                }
            }

            if (_runeNameText  != null) _runeNameText.text  = rune.RuneName;
            if (_effectText    != null) _effectText.text    = rune.EffectDescription;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleButtonClicked()
        {
            OnClicked?.Invoke(_rune);
        }
    }
}
