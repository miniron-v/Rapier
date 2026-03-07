using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 카메라가 대상 Transform을 부드럽게 추적한다.
    /// 2D 전용 — Z축은 카메라 원래 값을 유지한다.
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

        private float _fixedZ;

        private void Awake()
        {
            _fixedZ = transform.position.z;
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
    }
}
