using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Data.Equipment;

namespace Game.UI.Lobby.Shop
{
    /// <summary>
    /// 가챠 결과 모달 View.
    /// ShowResults()로 아이템 목록을 표시하고, 닫기 버튼 클릭 시 OnCloseClicked 발화.
    /// </summary>
    public class GachaResultModalView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset _font;

        private Transform _itemContainer;
        private Button    _closeButton;
        private Image     _flashImage;

        private readonly List<GachaResultItemView> _itemViews = new();

        /// <summary>닫기 버튼 클릭 시 발화.</summary>
        public event Action OnCloseClicked;

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        /// <summary>참조 주입. Awake 이전에 호출됨 — 여기서 리스너 등록.</summary>
        public void InitReferences(Transform itemContainer, Button closeButton, Image flashImage, TMP_FontAsset font)
        {
            _itemContainer = itemContainer;
            _closeButton   = closeButton;
            _flashImage    = flashImage;
            _font          = font;

            _closeButton?.onClick.AddListener(HandleCloseClicked);
        }

        /// <summary>모달 표시/숨김.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>아이템 목록을 표시한다. Epic/Unique는 강조 연출 재생.</summary>
        public void ShowResults(IReadOnlyList<EquipmentInstance> items)
        {
            if (items == null) return;

            bool hasHighGrade = false;
            Color highGradeColor = Color.white;

            // 기존 뷰 비활성화
            foreach (var view in _itemViews)
            {
                if (view != null)
                    view.gameObject.SetActive(false);
            }

            for (int i = 0; i < items.Count; i++)
            {
                GachaResultItemView itemView;
                if (i < _itemViews.Count)
                {
                    itemView = _itemViews[i];
                    itemView.gameObject.SetActive(true);
                }
                else
                {
                    // 동적 생성
                    itemView = CreateItemView();
                    _itemViews.Add(itemView);
                }

                itemView.Refresh(items[i]);

                // Epic/Unique 강조 연출
                if (items[i] != null &&
                    (items[i].Grade == EquipmentGrade.Epic || items[i].Grade == EquipmentGrade.Unique))
                {
                    hasHighGrade = true;
                    highGradeColor = EquipmentGradeHelper.GetGradeColor(items[i].Grade);
                    itemView.PlayHighlightEffect();
                }
            }

            // Epic/Unique 포함 시 전체 플래시 연출
            if (hasHighGrade && _flashImage != null)
                StartCoroutine(PlayFlash(highGradeColor, 0.4f, 0.3f));
        }

        // ── 내부 연출 ─────────────────────────────────────────────────────

        private IEnumerator PlayFlash(Color color, float startAlpha, float duration)
        {
            _flashImage.gameObject.SetActive(true);
            color.a = startAlpha;
            _flashImage.color = color;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                color.a = Mathf.Lerp(startAlpha, 0f, t);
                _flashImage.color = color;
                yield return null;
            }

            color.a = 0f;
            _flashImage.color = color;
            _flashImage.gameObject.SetActive(false);
        }

        // ── 내부 헬퍼 ─────────────────────────────────────────────────────

        private GachaResultItemView CreateItemView()
        {
            var go = new GameObject("GachaResultItem", typeof(RectTransform));
            go.transform.SetParent(_itemContainer, false);

            // 배경
            var bg  = go.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

            // GradeBackground
            var gradeBgGo  = new GameObject("GradeBackground", typeof(RectTransform));
            gradeBgGo.transform.SetParent(go.transform, false);
            var gradeBgImg = gradeBgGo.AddComponent<Image>();
            var gradeBgRect = gradeBgGo.GetComponent<RectTransform>();
            gradeBgRect.anchorMin = Vector2.zero;
            gradeBgRect.anchorMax = Vector2.one;
            gradeBgRect.offsetMin = gradeBgRect.offsetMax = Vector2.zero;

            // Icon
            var iconGo  = new GameObject("ItemIcon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconImg  = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.3f);
            iconRect.anchorMax = new Vector2(0.9f, 0.9f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;

            // ItemName
            var nameGo  = new GameObject("ItemName", typeof(RectTransform));
            nameGo.transform.SetParent(go.transform, false);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.font      = _font;
            nameTmp.fontSize  = 20;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color     = Color.white;
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0.3f);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;

            var itemView = go.AddComponent<GachaResultItemView>();
            itemView.InitReferences(iconImg, gradeBgImg, nameTmp);
            return itemView;
        }

        // ── Event Handlers ────────────────────────────────────────────────

        private void HandleCloseClicked()
        {
            OnCloseClicked?.Invoke();
        }
    }
}
