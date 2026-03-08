using UnityEngine;
using UnityEngine.UI;
using Game.Characters;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 플레이어 HUD를 담당한다.
    ///   - 화면 상단: HP 바 (Slider 기반)
    ///   - 플레이어 주변: 원형 차지 게이지 (Image Radial360 fill)
    ///
    /// [연결 방식]
    ///   Start에서 ServiceLocator로 PlayerPresenter를 찾아
    ///   Model.OnHpChanged / Model.OnChargeChanged 를 구독한다.
    ///   PlayerPresenter가 ServiceLocator에 등록되어 있어야 한다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        // ── Inspector (자동 찾기 실패 시 수동 연결용) ────────────────
        [Header("HP 바 Fill Image")]
        [SerializeField] private Image _hpFillImage;

        [Header("차지 게이지 Fill Image (Radial360)")]
        [SerializeField] private Image _chargeImage;

        // ── 내부 참조 ─────────────────────────────────────────────
        private CharacterModel _playerModel;

        // ── 라이프사이클 ──────────────────────────────────────────
private void Start()
        {
            if (_hpFillImage == null)
            {
                var go = FindInChildren(transform, "HpFill");
                if (go != null) _hpFillImage = go.GetComponent<Image>();
            }
            if (_chargeImage == null)
            {
                var go = FindInChildren(transform, "ChargeGaugeFill");
                if (go != null) _chargeImage = go.GetComponent<Image>();
            }

            var player = ServiceLocator.Get<PlayerPresenter>();
            if (player == null) { Debug.LogError("[HudView] PlayerPresenter가 ServiceLocator에 없음."); return; }

            _playerModel = player.PublicModel;
            if (_playerModel == null) { Debug.LogError("[HudView] PlayerPresenter.PublicModel이 null."); return; }

            _playerModel.OnHpChanged     += OnHpChanged;
            _playerModel.OnChargeChanged += OnChargeChanged;

            SetHp(_playerModel.CurrentHp / _playerModel.StatData.maxHp);
            SetCharge(0f);
            Debug.Log($"[HudView] 초기화 완료. HpFill={_hpFillImage != null}, ChargeImage={_chargeImage != null}");
        }

        private void OnDestroy()
        {
            if (_playerModel == null) return;
            _playerModel.OnHpChanged     -= OnHpChanged;
            _playerModel.OnChargeChanged -= OnChargeChanged;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────
        private void OnHpChanged(float currentHp)
        {
            if (_playerModel == null) return;
            SetHp(currentHp / _playerModel.StatData.maxHp);
        }

private void OnChargeChanged(float ratio) => SetCharge(ratio);

        // ── UI 갱신 ───────────────────────────────────────────────
private void SetHp(float ratio)
        {
            if (_hpFillImage != null)
                _hpFillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        private void SetCharge(float ratio)
        {
            if (_chargeImage != null)
                _chargeImage.fillAmount = Mathf.Clamp01(ratio);
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
