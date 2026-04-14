using System;
using System.Collections.Generic;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 일괄 선택 드롭다운 View.
    /// 액션바 위쪽으로 펼치는 오버레이. 등급 4종(Normal/Rare/Epic/Unique) 항목을 표시한다.
    /// 항목 선택 시 콜백을 통해 선택된 등급을 전달하고 자동으로 닫힌다.
    /// 로직 없음 — 표시만 담당.
    /// </summary>
    public class BulkSelectDropdownView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [SerializeField] private List<Button>          _gradeButtons   = new();
        [SerializeField] private List<Image>           _gradeDotImages = new();
        [SerializeField] private List<TextMeshProUGUI> _gradeTexts     = new();

        // ── Private Fields ───────────────────────────────────────────────────

        private Action<EquipmentGrade> _onGradeSelected;

        private static readonly EquipmentGrade[] GRADES =
        {
            EquipmentGrade.Normal,
            EquipmentGrade.Rare,
            EquipmentGrade.Epic,
            EquipmentGrade.Unique,
        };

        private static readonly string[] GRADE_LABELS = { "Normal", "Rare", "Epic", "Unique" };

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            InitButtonListeners();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>런타임 생성 시 SerializeField 참조를 주입한다 (LobbyHudSetup 호출).</summary>
        public void InitReferences(
            List<Button>          gradeButtons,
            List<Image>           gradeDotImages,
            List<TextMeshProUGUI> gradeTexts)
        {
            _gradeButtons   = gradeButtons;
            _gradeDotImages = gradeDotImages;
            _gradeTexts     = gradeTexts;
            InitButtonListeners();
        }

        // ── Public 메서드 (Presenter → View) ────────────────────────────────

        /// <summary>드롭다운을 표시한다. 등급 선택 시 onGradeSelected 콜백 호출 후 자동 닫힘.</summary>
        public void Show(EquipmentPanelView.InventoryTab tab, Action<EquipmentGrade> onGradeSelected)
        {
            _onGradeSelected = onGradeSelected;
            RefreshGradeDots();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        /// <summary>드롭다운을 닫는다.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            _onGradeSelected = null;
        }

        // ── Private 메서드 ────────────────────────────────────────────────────

        private void InitButtonListeners()
        {
            for (int i = 0; i < _gradeButtons.Count && i < GRADES.Length; i++)
            {
                int index = i; // 클로저 캡처
                _gradeButtons[i].onClick.RemoveAllListeners();
                _gradeButtons[i].onClick.AddListener(() => HandleGradeButtonClicked(index));
            }
        }

        private void RefreshGradeDots()
        {
            for (int i = 0; i < GRADES.Length; i++)
            {
                var gradeColor = EquipmentGradeHelper.GetGradeColor(GRADES[i]);
                if (i < _gradeDotImages.Count && _gradeDotImages[i] != null)
                    _gradeDotImages[i].color = gradeColor;
                if (i < _gradeTexts.Count && _gradeTexts[i] != null)
                    _gradeTexts[i].text = GRADE_LABELS[i];
            }
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleGradeButtonClicked(int index)
        {
            if (index < 0 || index >= GRADES.Length) return;
            var grade = GRADES[index];
            var cb    = _onGradeSelected;
            Hide(); // 먼저 닫고 콜백 (콜백에서 재호출 방지)
            cb?.Invoke(grade);
        }
    }
}
