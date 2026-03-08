using UnityEngine;
using UnityEngine.UI;

namespace Game.Enemies
{
    /// <summary>
    /// 적 오브젝트 위에 따라다니는 월드 스페이스 HP 바.
    ///
    /// [연결 방식]
    ///   EnemyPresenter.Spawn() 호출 시 Init(model)을 함께 호출한다.
    ///   EnemyModel.OnHpChanged (0~1 비율) 을 구독해 fillAmount 갱신.
    ///   EnemyModel.OnDeath 시 자기 자신을 숨긴다.
    ///
    /// [설치 방법]
    ///   Enemy 프리팹 자식으로 World Space Canvas → EnemyHpBar 오브젝트를 추가하고
    ///   이 컴포넌트를 붙인다. _fillImage 는 Radial / Simple fill Image.
    /// </summary>
    public class EnemyHpBar : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("바 이미지 (Image - Filled)")]
        [SerializeField] private Image _fillImage;

        [Header("항상 카메라를 향하도록")]
        [SerializeField] private bool _faceCameraAlways = true;

        // ── 내부 참조 ─────────────────────────────────────────────
        private EnemyModel _model;
        private Camera     _mainCam;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            _mainCam = Camera.main;
        }

        /// <summary>EnemyPresenter.Spawn()에서 호출.</summary>
        public void Init(EnemyModel model)
        {
            // 이전 구독 해제
            if (_model != null)
            {
                _model.OnHpChanged -= OnHpChanged;
                _model.OnDeath     -= OnDeath;
            }

            _model = model;
            _model.OnHpChanged += OnHpChanged;
            _model.OnDeath     += OnDeath;

            SetFill(1f);
            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!_faceCameraAlways || _mainCam == null) return;
            // 2D URP에서는 카메라가 Z축을 향하므로 빌보드 처리
            transform.rotation = _mainCam.transform.rotation;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────
        private void OnHpChanged(float ratio) => SetFill(ratio);

        private void OnDeath()
        {
            gameObject.SetActive(false);
            if (_model == null) return;
            _model.OnHpChanged -= OnHpChanged;
            _model.OnDeath     -= OnDeath;
            _model = null;
        }

        private void OnDestroy()
        {
            if (_model == null) return;
            _model.OnHpChanged -= OnHpChanged;
            _model.OnDeath     -= OnDeath;
        }

        // ── UI 갱신 ───────────────────────────────────────────────
        private void SetFill(float ratio)
        {
            if (_fillImage != null)
                _fillImage.fillAmount = Mathf.Clamp01(ratio);
        }
    }
}
