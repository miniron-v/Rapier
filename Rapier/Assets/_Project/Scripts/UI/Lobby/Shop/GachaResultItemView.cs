using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Data.Equipment;

namespace Game.UI.Lobby.Shop
{
    /// <summary>
    /// 가챠 결과 모달의 아이템 셀 1개 View.
    /// Epic/Unique 획득 시 PlayHighlightEffect()로 강조 연출을 재생한다.
    /// </summary>
    public class GachaResultItemView : MonoBehaviour
    {
        private Image           _icon;
        private Image           _gradeBackground;
        private TextMeshProUGUI _itemNameText;

        /// <summary>참조 주입.</summary>
        public void InitReferences(Image icon, Image gradeBackground, TextMeshProUGUI itemNameText)
        {
            _icon            = icon;
            _gradeBackground = gradeBackground;
            _itemNameText    = itemNameText;
        }

        /// <summary>EquipmentInstance 정보로 셀을 갱신한다.</summary>
        public void Refresh(EquipmentInstance instance)
        {
            if (instance == null) return;

            // 아이콘
            if (_icon != null)
            {
                _icon.sprite  = instance.Data?.Icon;
                _icon.enabled = instance.Data?.Icon != null;
            }

            // 등급 배경색
            if (_gradeBackground != null)
                _gradeBackground.color = EquipmentGradeHelper.GetGradeColor(instance.Grade);

            // 이름
            if (_itemNameText != null)
                _itemNameText.text = instance.Data?.ItemName ?? "";
        }

        /// <summary>Epic/Unique 획득 강조 연출 (scale 펀치 + color flash). 약 0.4초.</summary>
        public void PlayHighlightEffect()
        {
            StartCoroutine(HighlightRoutine());
        }

        private IEnumerator HighlightRoutine()
        {
            var originalScale = transform.localScale;
            var originalColor = _gradeBackground != null ? _gradeBackground.color : Color.white;

            // 스케일 펀치: 0.5 → 1.2 → 1.0
            float elapsed = 0f;
            float growDuration = 0.15f;
            while (elapsed < growDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / growDuration;
                float scale = Mathf.Lerp(0.5f, 1.2f, t);
                transform.localScale = originalScale * scale;
                yield return null;
            }

            elapsed = 0f;
            float shrinkDuration = 0.15f;
            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shrinkDuration;
                float scale = Mathf.Lerp(1.2f, 1.0f, t);
                transform.localScale = originalScale * scale;
                yield return null;
            }

            transform.localScale = originalScale;

            // 색상 플래시 (밝게 → 원래 색)
            if (_gradeBackground != null)
            {
                elapsed = 0f;
                float flashDuration = 0.10f;
                var flashColor = Color.white;
                while (elapsed < flashDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / flashDuration;
                    _gradeBackground.color = Color.Lerp(flashColor, originalColor, t);
                    yield return null;
                }
                _gradeBackground.color = originalColor;
            }
        }
    }
}
