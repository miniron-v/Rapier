using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Characters;

namespace Game.Enemies
{
    /// <summary>
    /// 적 오브젝트 위에 따라다니는 월드 스페이스 HP 바 + 레이피어 표식 수 표시.
    ///
    /// [연결 방식]
    ///   EnemyPresenter.Spawn() 호출 시 Init(model)을 함께 호출한다.
    ///   EnemyModel.OnHpChanged (0~1 비율) 을 구독해 fillAmount 갱신.
    ///   EnemyModel.OnDeath 시 자기 자신을 숨긴다.
    ///
    /// [표식 표시]
    ///   RapierPresenter.OnMarkChanged 이벤트를 Init 시점에 구독.
    ///   자신의 EnemyPresenter와 일치하는 이벤트만 처리해 HP바 위 숫자로 표시.
    ///   표식 0이면 텍스트 숨김.
    ///
    /// [_markText 연결 규칙]
    ///   Inspector에서 직접 연결하거나, 없으면 Awake에서 자식 "MarkText" 오브젝트를 자동 탐색.
    /// </summary>
    public class EnemyHpBar : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("바 이미지 (Image - Filled)")]
        [SerializeField] private Image _fillImage;

        [Header("표식 수 텍스트 (TextMeshProUGUI, HP바 위) — 비워두면 자동 탐색")]
        [SerializeField] private TextMeshProUGUI _markText;

        [Header("항상 카메라를 향하도록")]
        [SerializeField] private bool _faceCameraAlways = true;

        // ── 내부 참조 ─────────────────────────────────────────────
        private EnemyModel      _model;
        private EnemyPresenter  _owner;
        private Camera          _mainCam;
        private RapierPresenter _rapierPresenter;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            _mainCam = Camera.main;
            _owner   = GetComponentInParent<EnemyPresenter>();

            // _markText Inspector 미연결 시 자식에서 이름으로 자동 탐색
            if (_markText == null)
            {
                var found = FindInChildren(transform, "MarkText");
                if (found != null)
                    _markText = found.GetComponent<TextMeshProUGUI>();

                if (_markText == null)
                    Debug.LogWarning("[EnemyHpBar] MarkText를 찾지 못했습니다. Inspector에서 직접 연결하거나 자식에 'MarkText' 오브젝트를 추가하세요.");
            }
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
            UnsubscribeRapier();

            _model = model;
            _model.OnHpChanged += OnHpChanged;
            _model.OnDeath     += OnDeath;

            // 레이피어 표식 구독 (씬에 RapierPresenter가 있으면)
            _rapierPresenter = Game.Core.ServiceLocator.Get<RapierPresenter>();
            if (_rapierPresenter != null)
                _rapierPresenter.OnMarkChanged += OnMarkChanged;

            SetFill(1f);
            SetMarkCount(0);
            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!_faceCameraAlways || _mainCam == null) return;
            transform.rotation = _mainCam.transform.rotation;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────
        private void OnHpChanged(float ratio) => SetFill(ratio);

        private void OnDeath()
        {
            gameObject.SetActive(false);
            if (_model != null)
            {
                _model.OnHpChanged -= OnHpChanged;
                _model.OnDeath     -= OnDeath;
                _model = null;
            }
            UnsubscribeRapier();
        }

        private void OnMarkChanged(EnemyPresenter target, int count)
        {
            if (target != _owner) return;
            SetMarkCount(count);
        }

        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.OnHpChanged -= OnHpChanged;
                _model.OnDeath     -= OnDeath;
            }
            UnsubscribeRapier();
        }

        // ── UI 갱신 ───────────────────────────────────────────────
        private void SetFill(float ratio)
        {
            if (_fillImage != null)
                _fillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        private void SetMarkCount(int count)
        {
            if (_markText == null) return;
            if (count <= 0)
            {
                _markText.gameObject.SetActive(false);
            }
            else
            {
                _markText.gameObject.SetActive(true);
                _markText.text = count.ToString();
            }
        }

        private void UnsubscribeRapier()
        {
            if (_rapierPresenter != null)
            {
                _rapierPresenter.OnMarkChanged -= OnMarkChanged;
                _rapierPresenter = null;
            }
        }

        // ── 유틸 ──────────────────────────────────────────────────
        private static Transform FindInChildren(Transform parent, string targetName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == targetName) return child;
                var found = FindInChildren(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
