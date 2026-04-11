using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Screen.safeArea 를 읽어 대상 RectTransform 의 anchorMin/anchorMax 를 재계산한다.
    /// 컴포넌트를 부착한 GameObject 의 RectTransform 을 대상으로 적용한다.
    ///
    /// OnEnable 시 1회 적용하고, Update 에서 safeArea 또는 화면 크기 변경을 감지할 때만 재적용한다.
    /// Portrait 고정 프로젝트 전용이므로 화면 회전 대응 로직은 포함하지 않는다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect          _lastSafeArea;
        private Vector2Int    _lastScreenSize;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            var currentScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (_lastSafeArea == Screen.safeArea && _lastScreenSize == currentScreenSize)
                return;

            ApplySafeArea();
        }

        /// <summary>
        /// Screen.safeArea 를 즉시 RectTransform 에 적용한다.
        /// 외부에서 강제 갱신이 필요한 경우 호출할 수 있다.
        /// </summary>
        public void ApplySafeArea()
        {
            if (_rectTransform == null) return;

            var safeArea   = Screen.safeArea;
            var screenSize = new Vector2(Screen.width, Screen.height);

            // 스크린 크기가 0 인 경우 부모 Canvas 아직 미초기화 상태 — 적용 스킵
            if (screenSize.x <= 0f || screenSize.y <= 0f) return;

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            _lastSafeArea   = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
