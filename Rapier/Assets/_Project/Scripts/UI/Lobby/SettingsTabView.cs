using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Lobby
{
    /// <summary>
    /// 탭 5 — 설정 패널 View.
    ///
    /// [B1 구현]
    ///   사운드/진동/그래픽 슬라이더·토글 UI 표시.
    ///   설정 값 저장은 B3 JSON 완성 전까지 PlayerPrefs를 임시 사용한다.
    ///   TODO(B3): PlayerPrefs → SaveManager(JSON) 교체. SaveManagerHook 슬롯 참고.
    ///
    /// [B3 hook - SaveManagerHook]
    ///   SaveManager가 완성되면 아래 _saveManagerHook 필드에
    ///   SaveManager 컴포넌트를 할당하고 PlayerPrefs 코드를 대체한다.
    /// </summary>
    public class SettingsTabView : LobbyTabViewBase
    {
        [Header("Sound")]
        [SerializeField] private Slider _bgmVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("Vibration")]
        [SerializeField] private Toggle _vibrationToggle;

        [Header("Graphics")]
        [SerializeField] private Slider _brightnessSlider;

        // ── B3 SaveManager Hook ───────────────────────────────────
        // TODO(B3): SaveManager 구현 완료 후 이 필드에 컴포넌트를 할당하고
        //           PlayerPrefs 저장/로드 코드를 SaveManager API로 교체한다.
        [Header("B3 Hook — SaveManager (assign after B3 complete)")]
        [SerializeField] private MonoBehaviour _saveManagerHook; // SaveManager 슬롯

        // PlayerPrefs 키 상수
        private const string KEY_BGM       = "Settings_BGMVolume";
        private const string KEY_SFX       = "Settings_SFXVolume";
        private const string KEY_VIBRATION = "Settings_Vibration";
        private const string KEY_BRIGHTNESS= "Settings_Brightness";

        // ── Presenter 접근용 프로퍼티 ─────────────────────────────
        /// <summary>BGM 볼륨 슬라이더. SettingsTabPresenter가 리스너를 등록한다.</summary>
        public Slider BgmVolumeSlider   => _bgmVolumeSlider;

        /// <summary>SFX 볼륨 슬라이더. SettingsTabPresenter가 리스너를 등록한다.</summary>
        public Slider SfxVolumeSlider   => _sfxVolumeSlider;

        /// <summary>진동 토글. SettingsTabPresenter가 리스너를 등록한다.</summary>
        public Toggle VibrationToggle   => _vibrationToggle;

        /// <summary>밝기 슬라이더. SettingsTabPresenter가 리스너를 등록한다.</summary>
        public Slider BrightnessSlider  => _brightnessSlider;

        /// <summary>
        /// SettingsTabPresenter가 초기화 시 호출한다.
        /// </summary>
        public void Init(
            Slider bgmSlider,
            Slider sfxSlider,
            Toggle vibrationToggle,
            Slider brightnessSlider)
        {
            _bgmVolumeSlider  = bgmSlider;
            _sfxVolumeSlider  = sfxSlider;
            _vibrationToggle  = vibrationToggle;
            _brightnessSlider = brightnessSlider;
        }

        /// <summary>
        /// 저장된 설정 값을 PlayerPrefs에서 읽어 UI에 반영한다.
        /// TODO(B3): SaveManager.Load()로 교체.
        /// </summary>
        public void LoadSettings()
        {
            if (_bgmVolumeSlider  != null) _bgmVolumeSlider.value  = PlayerPrefs.GetFloat(KEY_BGM,        1f);
            if (_sfxVolumeSlider  != null) _sfxVolumeSlider.value  = PlayerPrefs.GetFloat(KEY_SFX,        1f);
            if (_vibrationToggle  != null) _vibrationToggle.isOn   = PlayerPrefs.GetInt(KEY_VIBRATION,    1) == 1;
            if (_brightnessSlider != null) _brightnessSlider.value = PlayerPrefs.GetFloat(KEY_BRIGHTNESS, 1f);
        }

        /// <summary>
        /// 현재 UI 상태를 PlayerPrefs에 저장한다.
        /// TODO(B3): SaveManager.Save()로 교체.
        /// </summary>
        public void SaveSettings()
        {
            if (_bgmVolumeSlider  != null) PlayerPrefs.SetFloat(KEY_BGM,        _bgmVolumeSlider.value);
            if (_sfxVolumeSlider  != null) PlayerPrefs.SetFloat(KEY_SFX,        _sfxVolumeSlider.value);
            if (_vibrationToggle  != null) PlayerPrefs.SetInt(KEY_VIBRATION,     _vibrationToggle.isOn ? 1 : 0);
            if (_brightnessSlider != null) PlayerPrefs.SetFloat(KEY_BRIGHTNESS,  _brightnessSlider.value);
            PlayerPrefs.Save();
        }
    }
}
