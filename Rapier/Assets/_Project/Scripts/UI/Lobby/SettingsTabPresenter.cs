using UnityEngine;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 5 — 설정 탭 Presenter.
    ///
    /// [역할]
    ///   - 슬라이더·토글 값 변경 시 설정 저장 (PlayerPrefs, B3 이후 SaveManager로 교체)
    ///   - 탭 진입 시 저장된 설정 값 로드
    ///
    /// [이벤트 구독/해제]
    ///   OnTabShown (LobbyPresenter가 탭 활성화 시 호출): 슬라이더·토글 리스너 등록 + 설정 로드
    ///   OnTabHidden (LobbyPresenter가 탭 비활성화 시 호출): 슬라이더·토글 리스너 해제
    ///
    /// ── 구독/이벤트 매핑 ────────────────────────────────────────────────
    /// | 이벤트                              | 구독 위치    | 해제 위치     | 핸들러                    |
    /// |-------------------------------------|-------------|--------------|--------------------------|
    /// | _view.BgmVolumeSlider.onValueChanged | OnTabShown  | OnTabHidden  | HandleBgmChanged         |
    /// | _view.SfxVolumeSlider.onValueChanged | OnTabShown  | OnTabHidden  | HandleSfxChanged         |
    /// | _view.VibrationToggle.onValueChanged | OnTabShown  | OnTabHidden  | HandleVibrationChanged   |
    /// | _view.BrightnessSlider.onValueChanged| OnTabShown  | OnTabHidden  | HandleBrightnessChanged  |
    /// ─────────────────────────────────────────────────────────────────────
    /// </summary>
    public class SettingsTabPresenter : MonoBehaviour
    {
        private SettingsTabView _view;

        /// <summary>LobbyPresenter가 초기화 시 호출한다.</summary>
        public void Init(SettingsTabView view)
        {
            _view = view;
        }

        // ── 탭 전환 진입점 (LobbyPresenter가 호출) ───────────────
        /// <summary>탭이 표시될 때 LobbyPresenter가 호출한다. 리스너를 등록하고 설정을 로드한다.</summary>
        public void OnTabShown()
        {
            if (_view == null) return;

            // 저장된 설정 로드
            _view.LoadSettings();

            // 슬라이더/토글 리스너 등록
            if (_view.BgmVolumeSlider  != null) _view.BgmVolumeSlider.onValueChanged.AddListener(HandleBgmChanged);
            if (_view.SfxVolumeSlider  != null) _view.SfxVolumeSlider.onValueChanged.AddListener(HandleSfxChanged);
            if (_view.VibrationToggle  != null) _view.VibrationToggle.onValueChanged.AddListener(HandleVibrationChanged);
            if (_view.BrightnessSlider != null) _view.BrightnessSlider.onValueChanged.AddListener(HandleBrightnessChanged);
        }

        /// <summary>탭이 숨겨질 때 LobbyPresenter가 호출한다. 리스너를 해제한다.</summary>
        public void OnTabHidden()
        {
            if (_view == null) return;

            // 슬라이더/토글 리스너 해제
            if (_view.BgmVolumeSlider  != null) _view.BgmVolumeSlider.onValueChanged.RemoveListener(HandleBgmChanged);
            if (_view.SfxVolumeSlider  != null) _view.SfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxChanged);
            if (_view.VibrationToggle  != null) _view.VibrationToggle.onValueChanged.RemoveListener(HandleVibrationChanged);
            if (_view.BrightnessSlider != null) _view.BrightnessSlider.onValueChanged.RemoveListener(HandleBrightnessChanged);
        }

        // ── Event Handlers ────────────────────────────────────────
        private void HandleBgmChanged(float value)
        {
            _view.SaveSettings();
            // TODO(B3): AudioManager.SetBGMVolume(value) 연동
        }

        private void HandleSfxChanged(float value)
        {
            _view.SaveSettings();
            // TODO(B3): AudioManager.SetSFXVolume(value) 연동
        }

        private void HandleVibrationChanged(bool isOn)
        {
            _view.SaveSettings();
            // TODO(B3): VibrationManager.SetEnabled(isOn) 연동
        }

        private void HandleBrightnessChanged(float value)
        {
            _view.SaveSettings();
            // TODO(B3): GraphicsManager.SetBrightness(value) 연동
        }
    }
}
