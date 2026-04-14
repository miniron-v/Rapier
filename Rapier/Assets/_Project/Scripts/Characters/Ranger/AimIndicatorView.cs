using UnityEngine;

namespace Game.Characters.Ranger
{
    /// <summary>
    /// 레인저 차지 조준 표시기.
    ///
    /// [표시 방식]
    ///   LineRenderer 기반 라인 + 화살촉 표시.
    ///   스프라이트 교체 대비 SerializeField 노출 (Phase 26-D 에서 에셋 할당 가능).
    ///
    /// [갱신]
    ///   UpdateAim(origin, direction, length) 을 매 프레임 호출하여 라인을 갱신한다.
    ///   Show() / Hide() 로 활성/비활성 전환.
    ///
    /// [생명주기]
    ///   RangerPresenter.OnEnable → Show
    ///   RangerPresenter.OnDisable / OnBeforeDeath / OnHoldRelease → Hide
    /// </summary>
    public class AimIndicatorView : MonoBehaviour
    {
        // ── 직렬화 필드 ───────────────────────────────────────────────────
        [Header("LineRenderer (기본 사용)")]
        [SerializeField] private LineRenderer _lineRenderer;

        [Header("화살촉 스프라이트 (Phase 26-D에서 할당 가능, null이면 라인만 표시)")]
        [SerializeField] private SpriteRenderer _arrowHeadSprite;

        [Header("라인 색상")]
        [SerializeField] private Color _lineColorStart = new Color(1f, 0.8f, 0f, 0.9f);
        [SerializeField] private Color _lineColorEnd   = new Color(1f, 0.8f, 0f, 0.2f);

        [Header("라인 두께")]
        [SerializeField] private float _lineWidth = 0.08f;

        // ── 초기화 ────────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureLineRenderer();
            Hide();
        }

        private void EnsureLineRenderer()
        {
            if (_lineRenderer != null) return;

            _lineRenderer = gameObject.GetComponent<LineRenderer>();
            if (_lineRenderer == null)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();

            _lineRenderer.positionCount  = 2;
            _lineRenderer.startWidth     = _lineWidth;
            _lineRenderer.endWidth       = _lineWidth * 0.3f;
            _lineRenderer.useWorldSpace  = true;
            _lineRenderer.sortingOrder   = 15;

            // 머티리얼 기본 설정
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.startColor = _lineColorStart;
            _lineRenderer.endColor   = _lineColorEnd;
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 조준 라인을 업데이트한다.
        /// </summary>
        /// <param name="origin">라인 시작점 (캐릭터 월드 위치)</param>
        /// <param name="direction">조준 방향 (정규화)</param>
        /// <param name="length">라인 길이 (차지량에 비례, unit)</param>
        public void UpdateAim(Vector2 origin, Vector2 direction, float length)
        {
            EnsureLineRenderer();

            Vector2 end = origin + direction * length;
            _lineRenderer.SetPosition(0, new Vector3(origin.x, origin.y, 0f));
            _lineRenderer.SetPosition(1, new Vector3(end.x,    end.y,    0f));

            // 화살촉 스프라이트 배치 (할당된 경우)
            if (_arrowHeadSprite != null)
            {
                _arrowHeadSprite.transform.position = new Vector3(end.x, end.y, 0f);
                float angle = Vector2.SignedAngle(Vector2.up, direction);
                _arrowHeadSprite.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>조준 인디케이터를 활성화한다.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>조준 인디케이터를 비활성화한다.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
