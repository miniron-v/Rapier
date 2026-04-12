using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Characters;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 플레이어 HUD를 담당한다.
    ///   - HP 바 (Horizontal fill)
    ///   - 원형 차지 게이지 (Radial360 fill)
    ///   - 회피 쿨타임 게이지 (Vertical fill, 노랑, 캐릭터 우측 세로 막대)
    ///     → 게이지가 가득 차면(ratio == 1f) 배경째 숨김
    ///     → 회피 사용 시(ratio == 0f) 다시 표시
    ///
    /// [연결 방식]
    ///   Start에서 ServiceLocator로 IPlayerCharacter를 찾아 Model 이벤트를 구독한다.
    ///   구체 캐릭터 타입(PlayerPresenter, RapierPresenter 등)에 의존하지 않는다.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [Header("HP 바 Fill Image")]
        [SerializeField] private Image _hpFillImage;

        [Header("차지 게이지 Fill Image (Radial360)")]
        [SerializeField] private Image _chargeImage;

        [Header("회피 쿨타임 게이지 Fill Image (Vertical)")]
        [SerializeField] private Image _dodgeCooldownImage;

        private TextMeshProUGUI _hpText;
        private CharacterModel  _playerModel;
        private GameObject      _dodgeCooldownBg;

        private void Start()
        {
            // Inspector 미연결 시 이름으로 자동 탐색
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
            if (_dodgeCooldownImage == null)
            {
                var go = FindInChildren(transform, "DodgeCooldownFill");
                if (go != null)
                {
                    _dodgeCooldownImage = go.GetComponent<Image>();
                    _dodgeCooldownBg    = go.transform.parent?.gameObject;
                }
            }
            {
                var go = FindInChildren(transform, "HpText");
                if (go != null) _hpText = go.GetComponent<TextMeshProUGUI>();
            }

            // IPlayerCharacter 인터페이스로 참조 — 구체 타입 무관
            var player = ServiceLocator.Get<IPlayerCharacter>();
            if (player == null)
            {
                Debug.LogError("[HudView] IPlayerCharacter가 ServiceLocator에 없음.");
                return;
            }

            _playerModel = player.PublicModel;
            if (_playerModel == null)
            {
                Debug.LogError("[HudView] IPlayerCharacter.PublicModel이 null.");
                return;
            }

            _playerModel.OnHpChanged            += OnHpChanged;
            _playerModel.OnChargeChanged        += OnChargeChanged;
            _playerModel.OnDodgeCooldownChanged += OnDodgeCooldownChanged;

            SetHp(_playerModel.CurrentHp / _playerModel.MaxHp, _playerModel.CurrentHp);
            SetCharge(0f);
            SetDodgeCooldown(1f);

            Debug.Log($"[HudView] 초기화 완료. HpFill={_hpFillImage != null}, " +
                      $"ChargeImage={_chargeImage != null}, DodgeCooldown={_dodgeCooldownImage != null}, " +
                      $"HpText={_hpText != null}");
        }

        private void OnDestroy()
        {
            if (_playerModel == null) return;
            _playerModel.OnHpChanged            -= OnHpChanged;
            _playerModel.OnChargeChanged        -= OnChargeChanged;
            _playerModel.OnDodgeCooldownChanged -= OnDodgeCooldownChanged;
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────
        private void OnHpChanged(float currentHp)
        {
            if (_playerModel == null) return;
            SetHp(currentHp / _playerModel.MaxHp, currentHp);
        }

        private void OnChargeChanged(float ratio)        => SetCharge(ratio);
        private void OnDodgeCooldownChanged(float ratio) => SetDodgeCooldown(ratio);

        // ── UI 갱신 ───────────────────────────────────────────────
        private void SetHp(float ratio, float currentHp)
        {
            if (_hpFillImage != null)
                _hpFillImage.fillAmount = Mathf.Clamp01(ratio);
            if (_hpText != null)
                _hpText.text = currentHp.ToString("F0");
        }

        private void SetCharge(float ratio)
        {
            if (_chargeImage != null)
                _chargeImage.fillAmount = Mathf.Clamp01(ratio);
        }

        private void SetDodgeCooldown(float ratio)
        {
            if (_dodgeCooldownImage != null)
                _dodgeCooldownImage.fillAmount = Mathf.Clamp01(ratio);

            if (_dodgeCooldownBg == null) return;

            if (ratio >= 1f)
                _dodgeCooldownBg.SetActive(false);
            else if (ratio <= 0f)
                _dodgeCooldownBg.SetActive(true);
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
