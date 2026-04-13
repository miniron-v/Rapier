using System;
using System.Collections.Generic;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 룬 인벤토리 팝업 View.
    /// 캐릭터별 탭(Rapier/Assassin) + 룬 목록 + 해제 버튼을 표시한다.
    /// 로직 없음 — 표시 및 이벤트 발행만 담당. (MVP View 규칙)
    /// </summary>
    public class RuneInventoryPopupView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("탭 버튼")]
        [SerializeField] private Button          _rapierTabButton;
        [SerializeField] private TextMeshProUGUI _rapierTabText;
        [SerializeField] private Button          _assassinTabButton;
        [SerializeField] private TextMeshProUGUI _assassinTabText;

        [Header("룬 목록")]
        [SerializeField] private Transform       _runeListContent;
        [SerializeField] private RuneItemRowView _runeItemRowPrefab;

        [Header("버튼")]
        [SerializeField] private Button          _unequipButton;
        [SerializeField] private Button          _closeButton;

        // ── 상수 ─────────────────────────────────────────────────────────────

        private static readonly Color COLOR_TAB_ACTIVE   = new Color(0.9f, 0.8f, 0.2f, 1f);
        private static readonly Color COLOR_TAB_INACTIVE = new Color(0.5f, 0.5f, 0.5f, 1f);

        // ── 이벤트 (View → Presenter) ────────────────────────────────────────

        /// <summary>탭 전환 (캐릭터 ID)</summary>
        public event Action<string> OnTabSelected;

        /// <summary>룬 아이템 클릭 (룬 SO)</summary>
        public event Action<RuneItemData> OnRuneClicked;

        /// <summary>해제 버튼 클릭</summary>
        public event Action OnUnequipClicked;

        /// <summary>닫기 버튼 클릭</summary>
        public event Action OnCloseClicked;

        // ── 내부 상태 ────────────────────────────────────────────────────────

        private readonly List<RuneItemRowView> _rowViews = new();
        private string _currentTabId = "Rapier";

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _rapierTabButton?.onClick.AddListener(() => HandleTabClicked("Rapier"));
            _assassinTabButton?.onClick.AddListener(() => HandleTabClicked("Assassin"));
            _unequipButton?.onClick.AddListener(() => OnUnequipClicked?.Invoke());
            _closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _rapierTabButton?.onClick.RemoveAllListeners();
            _assassinTabButton?.onClick.RemoveAllListeners();
            _unequipButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.RemoveAllListeners();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 참조를 주입한다.</summary>
        public void InitReferences(
            Button          rapierTabButton,
            TextMeshProUGUI rapierTabText,
            Button          assassinTabButton,
            TextMeshProUGUI assassinTabText,
            Transform       runeListContent,
            RuneItemRowView runeItemRowPrefab,
            Button          unequipButton,
            Button          closeButton)
        {
            _rapierTabButton   = rapierTabButton;
            _rapierTabText     = rapierTabText;
            _assassinTabButton = assassinTabButton;
            _assassinTabText   = assassinTabText;
            _runeListContent   = runeListContent;
            _runeItemRowPrefab = runeItemRowPrefab;
            _unequipButton     = unequipButton;
            _closeButton       = closeButton;

            _rapierTabButton?.onClick.RemoveAllListeners();
            _rapierTabButton?.onClick.AddListener(() => HandleTabClicked("Rapier"));
            _assassinTabButton?.onClick.RemoveAllListeners();
            _assassinTabButton?.onClick.AddListener(() => HandleTabClicked("Assassin"));
            _unequipButton?.onClick.RemoveAllListeners();
            _unequipButton?.onClick.AddListener(() => OnUnequipClicked?.Invoke());
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        // ── Public 메서드 (Presenter → View) ─────────────────────────────────

        /// <summary>팝업을 표시하거나 숨긴다.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        /// <summary>룬 목록을 갱신한다.</summary>
        public void RefreshRuneList(IReadOnlyList<RuneItemData> runes)
        {
            // 기존 행 비활성화 후 재사용
            foreach (var row in _rowViews)
                row.gameObject.SetActive(false);

            for (int i = 0; i < runes.Count; i++)
            {
                RuneItemRowView row;
                if (i < _rowViews.Count)
                {
                    row = _rowViews[i];
                }
                else
                {
                    row = Instantiate(_runeItemRowPrefab, _runeListContent);
                    row.OnClicked += HandleRuneRowClicked;
                    _rowViews.Add(row);
                }
                row.gameObject.SetActive(true);
                row.Refresh(runes[i]);
            }
        }

        /// <summary>현재 활성 탭을 표시한다.</summary>
        public void SetActiveTab(string characterId)
        {
            _currentTabId = characterId;
            if (_rapierTabText   != null) _rapierTabText.color   = characterId == "Rapier"   ? COLOR_TAB_ACTIVE : COLOR_TAB_INACTIVE;
            if (_assassinTabText != null) _assassinTabText.color = characterId == "Assassin" ? COLOR_TAB_ACTIVE : COLOR_TAB_INACTIVE;
        }

        /// <summary>해제 버튼 활성/비활성.</summary>
        public void SetUnequipInteractable(bool interactable)
        {
            if (_unequipButton != null)
                _unequipButton.interactable = interactable;
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void HandleTabClicked(string characterId)
        {
            OnTabSelected?.Invoke(characterId);
        }

        private void HandleRuneRowClicked(RuneItemData rune)
        {
            OnRuneClicked?.Invoke(rune);
        }
    }
}
