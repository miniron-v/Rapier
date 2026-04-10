using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 카메라가 대상 Transform을 부드럽게 추적한다.
    /// 2D 전용 — Z축은 카메라 원래 값을 유지한다.
    ///
    /// [줌 펀치]
    ///   TriggerZoomPunch() 호출 시 orthographicSize를 zoomCurve에 따라 변화.
    ///   unscaledTime 기반 — 슬로우모션 중에도 정상 동작.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("추적 대상")]
        [SerializeField] private Transform _target;

        [Header("추적 설정")]
        [Tooltip("낮을수록 부드럽고, 높을수록 빠르게 추적. 권장: 0.05~0.2")]
        [SerializeField, Range(0.01f, 1f)] private float _smoothing = 0.1f;

        [Tooltip("카메라 오프셋 (월드 좌표 기준)")]
        [SerializeField] private Vector2 _offset = Vector2.zero;

        [Header("줌 펀치 (Just Dodge)")]
        [Tooltip("x=시간 정규화(0~1), y=orthographicSize 배율(1=기본, 0.85=15% 줌인)")]
        [SerializeField] private AnimationCurve zoomCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),   // 시작: 기본 크기
            new Keyframe(0.15f, 0.85f),   // 빠르게 줌인
            new Keyframe(0.75f, 0.88f),   // 줌 유지
            new Keyframe(1.00f, 1.00f)    // 서서히 복귀
        );
        [Tooltip("줌 펀치 지속 시간 (실제 시간, 초). slowDuration과 맞추는 것을 권장.")]
        [SerializeField] private float zoomDuration = 3f;

        // ── 내부 상태 ─────────────────────────────────────────────
        private Camera    _cam;
        private float     _baseOrthoSize;
        private float     _fixedZ;
        private Coroutine _zoomCoroutine;

        private void Awake()
        {
            _fixedZ = transform.position.z;
            _cam    = GetComponent<Camera>();
            if (_cam != null)
                _baseOrthoSize = _cam.orthographicSize;

            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<CameraFollow>();
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector2 targetPos = (Vector2)_target.position + _offset;
            Vector2 smoothed  = Vector2.Lerp(
                transform.position,
                targetPos,
                1f - Mathf.Pow(_smoothing, Time.deltaTime));

            transform.position = new Vector3(smoothed.x, smoothed.y, _fixedZ);
        }

        /// <summary>런타임에서 추적 대상을 교체한다.</summary>
        public void SetTarget(Transform target) => _target = target;

        /// <summary>Just Dodge 발동 시 줌 펀치 연출을 시작한다.</summary>
        public void TriggerZoomPunch()
        {
            if (_cam == null) return;
            if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
            _zoomCoroutine = StartCoroutine(ZoomPunchRoutine());
        }

        private IEnumerator ZoomPunchRoutine()
        {
            float elapsed = 0f;

            while (elapsed < zoomDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / zoomDuration);
                _cam.orthographicSize = _baseOrthoSize * zoomCurve.Evaluate(t);
                yield return null;
            }

            _cam.orthographicSize = _baseOrthoSize;
            _zoomCoroutine = null;
        }
    }
}
