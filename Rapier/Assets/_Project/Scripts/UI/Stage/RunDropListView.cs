using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Data.Equipment;

namespace Game.UI.Stage
{
    /// <summary>
    /// 스테이지 클리어 후 드롭 아이템 목록을 표시하는 UI 패널.
    ///
    /// [배치]
    ///   StageClearView와 동일 캔버스에 독립 패널로 배치한다.
    ///   Show(drops) / Hide() 로 수명을 관리한다.
    ///
    /// [슬롯 구성]
    ///   - Circle 아이콘 (등급 색상)
    ///   - 아이템명 (TextMeshProUGUI)
    ///   - 등급 텍스트 (TextMeshProUGUI, 등급 색상)
    /// </summary>
    public class RunDropListView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform  _listParent;  // ScrollView Content
        [SerializeField] private GameObject _emptyText;   // "획득한 장비 없음" 텍스트 오브젝트

        // ── 공개 API ─────────────────────────────────────────────────

        /// <summary>드롭 목록 패널을 표시한다. drops가 비어있으면 안내 텍스트를 표시한다.</summary>
        public void Show(IReadOnlyList<EquipmentInstance> drops)
        {
            _panel?.SetActive(true);

            // 기존 슬롯 정리
            ClearSlots();

            if (drops == null || drops.Count == 0)
            {
                _emptyText?.SetActive(true);
                return;
            }

            _emptyText?.SetActive(false);

            foreach (var drop in drops)
                CreateSlot(drop);
        }

        /// <summary>드롭 목록 패널을 숨긴다.</summary>
        public void Hide()
        {
            ClearSlots();
            _panel?.SetActive(false);
        }

        // ── 내부 유틸 ─────────────────────────────────────────────────
        private void ClearSlots()
        {
            if (_listParent == null) return;
            for (int i = _listParent.childCount - 1; i >= 0; i--)
                Destroy(_listParent.GetChild(i).gameObject);
        }

        private void CreateSlot(EquipmentInstance drop)
        {
            if (_listParent == null || drop == null) return;

            Color gradeColor = EquipmentGradeHelper.GetGradeColor(drop.Grade);

            // 슬롯 루트
            var slot = new GameObject("DropSlot");
            slot.transform.SetParent(_listParent, false);

            var slotRect = slot.AddComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(0f, 50f);

            // HorizontalLayoutGroup으로 아이콘+이름+등급 배치
            var layout = slot.AddComponent<HorizontalLayoutGroup>();
            layout.spacing              = 10f;
            layout.padding              = new RectOffset(5, 5, 5, 5);
            layout.childAlignment       = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth  = false;
            layout.childControlHeight = false;

            // 아이콘
            var iconGo   = new GameObject("Icon");
            iconGo.transform.SetParent(slot.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(40f, 40f);
            var img         = iconGo.AddComponent<Image>();
            img.sprite      = CreateCircleSprite(32);
            img.color       = gradeColor;

            // 아이템명
            var nameGo   = new GameObject("Name");
            nameGo.transform.SetParent(slot.transform, false);
            var nameRect = nameGo.AddComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(200f, 40f);
            var nameText     = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text    = drop.Data?.ItemName ?? "Unknown";
            nameText.fontSize = 18f;
            nameText.color   = Color.white;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;

            // 등급 텍스트
            var gradeGo   = new GameObject("Grade");
            gradeGo.transform.SetParent(slot.transform, false);
            var gradeRect = gradeGo.AddComponent<RectTransform>();
            gradeRect.sizeDelta = new Vector2(100f, 40f);
            var gradeText     = gradeGo.AddComponent<TextMeshProUGUI>();
            gradeText.text    = drop.Grade.ToString();
            gradeText.fontSize = 16f;
            gradeText.color   = gradeColor;
            gradeText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float cx   = size * 0.5f - 0.5f;
            float cy   = size * 0.5f - 0.5f;
            float rSq  = (size * 0.5f) * (size * 0.5f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                pixels[y * size + x] = (dx * dx + dy * dy) <= rSq
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
