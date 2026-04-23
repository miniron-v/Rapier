using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core.Utils;
using Game.Data.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby.Equipment
{
    /// <summary>
    /// 강화 모달 View.
    /// 현재 단계 → 다음 단계 정보(메인 스탯 미리보기, 서브 강화 단계 강조, 성공률, 필요 가루)를 표시하고
    /// 강화하기 / 닫기 버튼을 제공한다.
    /// 로직 없음 — 표시 및 이벤트 발행만 담당. (MVP View 규칙)
    /// </summary>
    public class EnhanceModalView : MonoBehaviour
    {
        // ── Serialized Fields ────────────────────────────────────────────────

        [Header("아이콘 / 이름")]
        [SerializeField] private Image           _itemIcon;
        [SerializeField] private TextMeshProUGUI _itemNameText;

        [Header("강화 단계")]
        [SerializeField] private TextMeshProUGUI _enhanceLevelText;

        [Header("메인 스탯 미리보기")]
        [SerializeField] private TextMeshProUGUI _mainStatPreviewText;

        [Header("서브 강화 단계 강조")]
        [SerializeField] private TextMeshProUGUI _subStatBonusText;

        [Header("성공률 / 가루")]
        [SerializeField] private TextMeshProUGUI _successPercentText;
        [SerializeField] private TextMeshProUGUI _dustCostText;

        [Header("버튼")]
        [SerializeField] private Button          _enhanceButton;
        [SerializeField] private TextMeshProUGUI _enhanceButtonText;
        [SerializeField] private Button          _closeButton;

        [Header("연출 — 플래시")]
        [SerializeField] private Image           _flashImage;

        [Header("연출 — 파편")]
        [SerializeField] private List<Image>     _shardImages = new();

        [Header("연출 — 토스트")]
        [SerializeField] private TextMeshProUGUI _toastText;

        // ── 이벤트 (View → Presenter) ────────────────────────────────────────

        /// <summary>강화하기 버튼 클릭</summary>
        public event Action OnEnhanceClicked;

        /// <summary>닫기 버튼 클릭</summary>
        public event Action OnCloseClicked;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            _enhanceButton?.onClick.AddListener(() => OnEnhanceClicked?.Invoke());
            _closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _enhanceButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.RemoveAllListeners();
        }

        // ── Public 초기화 ────────────────────────────────────────────────────

        /// <summary>LobbyHudSetup 에서 참조를 주입한다.</summary>
        public void InitReferences(
            Image           itemIcon,
            TextMeshProUGUI itemNameText,
            TextMeshProUGUI enhanceLevelText,
            TextMeshProUGUI mainStatPreviewText,
            TextMeshProUGUI subStatBonusText,
            TextMeshProUGUI successPercentText,
            TextMeshProUGUI dustCostText,
            Button          enhanceButton,
            TextMeshProUGUI enhanceButtonText,
            Button          closeButton,
            Image           flashImage,
            List<Image>     shardImages,
            TextMeshProUGUI toastText)
        {
            _itemIcon            = itemIcon;
            _itemNameText        = itemNameText;
            _enhanceLevelText    = enhanceLevelText;
            _mainStatPreviewText = mainStatPreviewText;
            _subStatBonusText    = subStatBonusText;
            _successPercentText  = successPercentText;
            _dustCostText        = dustCostText;
            _enhanceButton       = enhanceButton;
            _enhanceButtonText   = enhanceButtonText;
            _closeButton         = closeButton;
            _flashImage          = flashImage;
            _shardImages         = shardImages ?? new List<Image>();
            _toastText           = toastText;

            _enhanceButton?.onClick.RemoveAllListeners();
            _enhanceButton?.onClick.AddListener(() => OnEnhanceClicked?.Invoke());
            _closeButton?.onClick.RemoveAllListeners();
            _closeButton?.onClick.AddListener(() => OnCloseClicked?.Invoke());

            // 연출 초기화
            if (_flashImage != null)
            {
                var c = _flashImage.color;
                c.a = 0f;
                _flashImage.color = c;
                _flashImage.gameObject.SetActive(false);
            }
            foreach (var shard in _shardImages)
            {
                if (shard != null) shard.gameObject.SetActive(false);
            }
            if (_toastText != null) _toastText.gameObject.SetActive(false);
        }

        // ── Public 메서드 (Presenter → View) ─────────────────────────────────

        /// <summary>모달 표시/숨김.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        /// <summary>
        /// 강화 모달 정보를 갱신한다.
        /// enhanceMultiplier 공식: base × (1 + 0.10 × level)
        /// </summary>
        public void SetData(
            EquipmentInstance instance,
            int dustCost,
            int currentDust,
            int successPercent)
        {
            if (instance == null) return;

            var data       = instance.Data;
            int curLevel   = instance.EnhanceLevel;
            int nextLevel  = curLevel + 1;

            // 아이콘 / 이름
            if (_itemIcon != null)
            {
                _itemIcon.sprite = data.Icon;
                _itemIcon.color  = data.Icon != null ? Color.white : EquipmentGradeHelper.GetGradeColor(instance.Grade);
            }
            if (_itemNameText != null)
                _itemNameText.text = instance.EnhanceLevel > 0
                    ? $"{data.ItemName} +{instance.EnhanceLevel}"
                    : data.ItemName;

            // 강화 단계
            if (_enhanceLevelText != null)
                _enhanceLevelText.text = $"+{curLevel} → +{nextLevel}";

            // 메인 스탯 미리보기 (base × BALANCE §6-1 구간별 가속 배율)
            if (_mainStatPreviewText != null)
            {
                bool isAccessory = data.SlotType == EquipmentSlotType.Necklace
                                || data.SlotType == EquipmentSlotType.Ring;
                var rawMain = isAccessory && instance.RolledMainStat.HasValue
                    ? instance.RolledMainStat.Value
                    : data.MainStat;

                float curMult  = EquipmentGradeHelper.GetEnhanceMultiplier(curLevel);
                float nextMult = EquipmentGradeHelper.GetEnhanceMultiplier(nextLevel);

                string statLabel = GetStatLabel(rawMain.statType);
                if (rawMain.flatValue != 0f)
                {
                    // flatValue 는 정수 스탯 — Provider/상세뷰와 동일하게 RoundHalfUp 적용
                    int curVal  = (int)MathUtils.RoundHalfUp(rawMain.flatValue * curMult);
                    int nextVal = (int)MathUtils.RoundHalfUp(rawMain.flatValue * nextMult);
                    int delta   = nextVal - curVal;
                    _mainStatPreviewText.text =
                        $"{statLabel} {curVal} → {nextVal} (+{delta})";
                }
                else if (rawMain.percentValue != 0f)
                {
                    // percentValue 는 소수 유지
                    float curVal  = rawMain.percentValue * curMult;
                    float nextVal = rawMain.percentValue * nextMult;
                    float delta   = nextVal - curVal;
                    _mainStatPreviewText.text =
                        $"{statLabel} {curVal:0.##}% → {nextVal:0.##}% (+{delta:0.##}%)";
                }
                else
                {
                    _mainStatPreviewText.text = statLabel;
                }
            }

            // 서브스탯 강화 단계 강조 (3·6·9·12·15)
            if (_subStatBonusText != null)
            {
                bool isSubStep = nextLevel == 3 || nextLevel == 6 || nextLevel == 9
                              || nextLevel == 12 || nextLevel == 15;
                _subStatBonusText.gameObject.SetActive(isSubStep);
                if (isSubStep)
                {
                    Color gradeColor = EquipmentGradeHelper.GetGradeColor(instance.Grade);
                    _subStatBonusText.text  = "★ 서브스탯 강화 단계!";
                    _subStatBonusText.color = gradeColor;
                }
            }

            // 성공률
            if (_successPercentText != null)
                _successPercentText.text = $"성공 확률: {successPercent}%";

            // 가루
            if (_dustCostText != null)
            {
                bool dustOk = currentDust >= dustCost;
                string dustStr = dustOk
                    ? $"<color=#FFFFFF>{currentDust}</color>"
                    : $"<color=#FF4444>{currentDust}</color>";
                _dustCostText.text = $"필요 가루: {dustCost} / 보유 {dustStr}";
            }

            // 강화하기 버튼 활성 여부
            bool canEnhance = currentDust >= dustCost;
            if (_enhanceButton != null)
            {
                _enhanceButton.interactable = canEnhance;
                if (_enhanceButton.image != null)
                    _enhanceButton.image.color = canEnhance
                        ? new Color(0.2f, 0.7f, 0.3f)
                        : new Color(0.4f, 0.4f, 0.4f);
            }
        }

        /// <summary>강화하기 버튼 잠금/해제 (애니메이션 중 중복 클릭 방지용).</summary>
        public void SetEnhanceButtonInteractable(bool interactable)
        {
            if (_enhanceButton == null) return;
            _enhanceButton.interactable = interactable;
            if (_enhanceButton.image != null)
                _enhanceButton.image.color = interactable
                    ? new Color(0.2f, 0.7f, 0.3f)
                    : new Color(0.4f, 0.4f, 0.4f);
        }

        // ── 연출 Public ──────────────────────────────────────────────────────

        /// <summary>성공 연출을 View MB 컨텍스트로 시작하고 Coroutine 핸들을 반환한다.</summary>
        public Coroutine StartSuccessEffect(Color gradeColor)
            => StartCoroutine(SuccessEffectRoutine(gradeColor));

        /// <summary>실패 연출을 View MB 컨텍스트로 시작하고 Coroutine 핸들을 반환한다.</summary>
        public Coroutine StartFailEffect()
            => StartCoroutine(FailEffectRoutine());

        private IEnumerator SuccessEffectRoutine(Color gradeColor)
        {
            // 플래시 + 파편 + 토스트 동시 시작, 총 1초
            StartCoroutine(PlayFlash(gradeColor, 0.5f, 0.5f));
            StartCoroutine(PlayShards(0.7f));
            yield return StartCoroutine(ShowToast("강화 성공!", Color.yellow, 1.0f));
        }

        private IEnumerator FailEffectRoutine()
        {
            // 흔들림 (±10px, 0.3초)
            yield return StartCoroutine(PlayShake(10f, 0.3f));

            // 회색 플래시 (alpha 0.4 → 0, 0.3초)
            yield return StartCoroutine(PlayFlash(new Color(0.5f, 0.5f, 0.5f), 0.4f, 0.3f));

            // 토스트 "강화 실패..." 1.5초
            yield return StartCoroutine(ShowToast("강화 실패...", new Color(0.8f, 0.8f, 0.8f), 1.5f));
        }

        // ── Private 연출 ─────────────────────────────────────────────────────

        private IEnumerator PlayFlash(Color color, float startAlpha, float duration)
        {
            if (_flashImage == null) yield break;
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

        private IEnumerator PlayShards(float duration)
        {
            if (_shardImages == null || _shardImages.Count == 0) yield break;

            int count = _shardImages.Count;
            var rects = new RectTransform[count];
            var startPositions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                if (_shardImages[i] == null) continue;
                _shardImages[i].gameObject.SetActive(true);
                rects[i] = _shardImages[i].GetComponent<RectTransform>();
                startPositions[i] = rects[i] != null ? rects[i].localPosition : Vector3.zero;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                for (int i = 0; i < count; i++)
                {
                    if (_shardImages[i] == null) continue;

                    // 360° 분산
                    float angle = (360f / count) * i * Mathf.Deg2Rad;
                    float dist  = Mathf.Lerp(0f, 200f, t);
                    if (rects[i] != null)
                        rects[i].localPosition = startPositions[i]
                            + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);

                    // scale 1 → 0, alpha 1 → 0
                    float s = Mathf.Lerp(1f, 0f, t);
                    if (rects[i] != null) rects[i].localScale = new Vector3(s, s, 1f);
                    var c = _shardImages[i].color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    _shardImages[i].color = c;
                }
                yield return null;
            }

            // 정리
            for (int i = 0; i < count; i++)
            {
                if (_shardImages[i] == null) continue;
                _shardImages[i].gameObject.SetActive(false);
                if (rects[i] != null)
                {
                    rects[i].localPosition = startPositions[i];
                    rects[i].localScale    = Vector3.one;
                }
                var c = _shardImages[i].color;
                c.a = 1f;
                _shardImages[i].color = c;
            }
        }

        private IEnumerator PlayShake(float amplitude, float duration)
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 origin     = rt.anchoredPosition;
            float   elapsed    = 0f;
            float   frequency  = 6f; // 진동 주파수

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t       = elapsed / duration;
                float shake   = Mathf.Sin(t * Mathf.PI * frequency * 2f) * amplitude * (1f - t);
                rt.anchoredPosition = new Vector2(origin.x + shake, origin.y);
                yield return null;
            }

            rt.anchoredPosition = origin;
        }

        private IEnumerator ShowToast(string message, Color color, float duration)
        {
            if (_toastText == null) yield break;
            _toastText.gameObject.SetActive(true);
            _toastText.text  = message;
            _toastText.color = color;

            yield return new WaitForSeconds(duration);

            _toastText.gameObject.SetActive(false);
        }

        // ── Private 유틸 ─────────────────────────────────────────────────────

        private static string GetStatLabel(StatType type)
            => StatLabelFormatter.GetLabel(type);
    }
}
