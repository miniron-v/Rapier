using System.Collections;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 카메라가 대상 Transform을 부드럽게 추적한다.
    /// 2D 전용 — Z축은 카메라 원래 값을 유지한다.
    ///
    /// [줌 연출 — Just Dodge 2단계]
    ///   TriggerZoomIn()     : 저스트 회피 발동 시 호출. 줌인 후 값을 유지한다.
    ///   TriggerZoomReturn() : 슬로우 Exit 시작 시 호출. 현재 줌에서 기본 크기로 복귀한다.
    ///   두 메서드 모두 unscaledDeltaTime 기반 — 슬로우모션 중에도 정상 동작.
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

        [Header("줌 — 진입 (Just Dodge 발동 시)")]
        [Tooltip("x=진행 비율(0→1), y=orthographicSize 배율(1=기본, 0.85=15% 줌인). 끝 값에서 유지된다.")]
        [SerializeField] private AnimationCurve zoomInCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),   // 시작: 기본 크기
            new Keyframe(1.00f, 0.85f)    // 줌인 완료
        );
        [Tooltip("줌인 지속 시간 (실제 시간, 초). 이후 끝 배율을 유지한다.")]
        [SerializeField] private float zoomInDuration = 0.25f;

        [Header("줌 — 복귀 (슬로우 Exit 시작 시)")]
        [Tooltip("x=진행 비율(0→1). 현재 크기에서 기본 크기로 보간하는 Ease 커브.")]
        [SerializeField] private AnimationCurve zoomReturnEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        // duration은 런타임에 TriggerZoomReturn(duration) 으로 전달 — exitDuration과 자동 동기화

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

        /// <summary>
        /// Just Dodge 발동 시 호출. zoomInCurve에 따라 줌인하고 끝 배율을 유지한다.
        /// 슬로우 Exit 시작 전까지 유지 — TriggerZoomReturn이 복귀를 담당한다.
        /// </summary>
        public void TriggerZoomIn()
        {
            if (_cam == null) return;
            if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
            _zoomCoroutine = StartCoroutine(ZoomInRoutine());
        }

        /// <summary>
        /// 슬로우 Exit 구간 시작 시 호출. 현재 orthographicSize에서 기본 크기로 복귀한다.
        /// duration은 CharacterPresenterBase의 exitDuration과 일치하도록 전달한다.
        /// </summary>
        public void TriggerZoomReturn(float duration)
        {
            if (_cam == null) return;
            if (_zoomCoroutine != null) StopCoroutine(_zoomCoroutine);
            _zoomCoroutine = StartCoroutine(ZoomReturnRoutine(duration));
        }

        private IEnumerator ZoomInRoutine()
        {
            float elapsed    = 0f;
            float endSize    = _baseOrthoSize * zoomInCurve.Evaluate(1f);

            while (elapsed < zoomInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / zoomInDuration);
                _cam.orthographicSize = _baseOrthoSize * zoomInCurve.Evaluate(t);
                yield return null;
            }

            // 줌인 완료 후 슬로우가 끝날 때까지 값을 유지한다.
            _cam.orthographicSize = endSize;
            _zoomCoroutine = null;
        }

        private IEnumerator ZoomReturnRoutine(float duration)
        {
            float startSize = _cam.orthographicSize;
            float elapsed   = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _cam.orthographicSize = Mathf.Lerp(startSize, _baseOrthoSize, zoomReturnEase.Evaluate(t));
                yield return null;
            }

            _cam.orthographicSize = _baseOrthoSize;
            _zoomCoroutine = null;
        }
    }
}
