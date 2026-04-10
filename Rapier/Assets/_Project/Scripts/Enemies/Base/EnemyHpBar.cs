using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Characters;

namespace Game.Enemies
{
    /// <summary>
    /// 적 오브젝트 위에 따라다니는 월드 스페이스 HP 바 + 레이피어 표식 수 표시.
    /// EnemyPresenterBase를 참조하므로 일반 적/보스 모두 호환.
    /// </summary>
    public class EnemyHpBar : MonoBehaviour
    {
        [Header("바 이미지 (Image - Filled)")]
        [SerializeField] private Image _fillImage;

        [Header("표식 수 텍스트 (TextMeshProUGUI, HP바 위) — 비워두면 자동 탐색")]
        [SerializeField] private TextMeshProUGUI _markText;

        [Header("항상 카메라를 향하도록")]
        [SerializeField] private bool _faceCameraAlways = true;

        // ── 내부 참조 ─────────────────────────────────────────────
        private EnemyModel          _model;
        private EnemyPresenterBase  _owner;
        private Camera              _mainCam;
        private RapierPresenter     _rapierPresenter;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            _mainCam = Camera.main;
            _owner   = GetComponentInParent<EnemyPresenterBase>();

            if (_markText == null)
            {
                var found = FindInChildren(transform, "MarkText");
                if (found != null)
                    _markText = found.GetComponent<TextMeshProUGUI>();

                if (_markText == null)
                    Debug.LogWarning("[EnemyHpBar] MarkText를 찾지 못했습니다.");
            }
        }

        /// <summary>EnemyPresenterBase.Spawn()에서 호출.</summary>
        public void Init(EnemyModel model)
        {
            if (_model != null)
            {
                _model.OnHpChanged -= OnHpChanged;
                _model.OnDeath     -= OnDeath;
            }
            UnsubscribeRapier();

            _model = model;
            _model.OnHpChanged += OnHpChanged;
            _model.OnDeath     += OnDeath;

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

        private void OnMarkChanged(EnemyPresenterBase target, int count)
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
