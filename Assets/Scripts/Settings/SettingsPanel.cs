using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador del panel de configuración. Conectar desde el Inspector:
/// — Un objeto raíz que se activa/desactiva (el panel completo).
/// — Los cuatro GameObjects de contenido de cada tab.
/// — Sliders, dropdowns, toggles, y botones de la UI.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("Panel root")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Tab content objects")]
    [SerializeField] private GameObject _contentAudio;
    [SerializeField] private GameObject _contentVideo;
    [SerializeField] private GameObject _contentLanguage;
    [SerializeField] private GameObject _contentVoice;

    [Header("Tab buttons")]
    [SerializeField] private Button _tabAudio;
    [SerializeField] private Button _tabVideo;
    [SerializeField] private Button _tabLanguage;
    [SerializeField] private Button _tabVoice;

    [Header("Audio")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Slider _voiceVolumeSlider;

    [Header("Video")]
    [SerializeField] private TMP_Dropdown _resolutionDropdown;
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private TMP_Dropdown _qualityDropdown;

    [Header("Language")]
    [SerializeField] private Button _btnSpanish;
    [SerializeField] private Button _btnEnglish;

    [Header("Voice")]
    [SerializeField] private Toggle _voiceEnabledToggle;
    [SerializeField] private TMP_Dropdown _micDropdown;
    [SerializeField] private TMP_Text _micStatusLabel;
    [SerializeField] private Button _btnRetryAudioOutput;
    [SerializeField] private TMP_Text _audioOutputLabel;

    [Header("Footer")]
    [SerializeField] private Button _btnSave;
    [SerializeField] private Button _btnCancel;
    [SerializeField] private Button _btnRestore;
    [SerializeField] private Button _btnClose;

    // Snapshot de valores al abrir, para poder cancelar.
    float _snapMaster, _snapMusic, _snapSfx, _snapVoice;
    bool  _snapVoiceEnabled, _snapFullscreen;
    int   _snapRes, _snapQuality, _snapLang;
    string _snapMic;

    string[] _micDevices;

    // ── Panel open/close ─────────────────────────────────────────────────────

    public void Open()
    {
        TakeSnapshot();
        PopulateAll();
        _panelRoot.SetActive(true);
        ShowTab(_contentAudio);
    }

    public void Close()
    {
        _panelRoot.SetActive(false);
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        _panelRoot.SetActive(false);

        _tabAudio.onClick.AddListener(()    => ShowTab(_contentAudio));
        _tabVideo.onClick.AddListener(()    => ShowTab(_contentVideo));
        _tabLanguage.onClick.AddListener(() => ShowTab(_contentLanguage));
        _tabVoice.onClick.AddListener(()    => ShowTab(_contentVoice));

        _masterSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetMasterVolume(v));
        _musicSlider.onValueChanged.AddListener(v  => SettingsManager.Instance.SetMusicVolume(v));
        _sfxSlider.onValueChanged.AddListener(v    => SettingsManager.Instance.SetSfxVolume(v));
        _voiceVolumeSlider.onValueChanged.AddListener(v => SettingsManager.Instance.SetVoiceVolume(v));

        _resolutionDropdown.onValueChanged.AddListener(i => SettingsManager.Instance.SetResolutionIndex(i));
        _fullscreenToggle.onValueChanged.AddListener(b   => SettingsManager.Instance.SetFullscreen(b));
        _qualityDropdown.onValueChanged.AddListener(i    => SettingsManager.Instance.SetQualityIndex(i));

        _btnSpanish.onClick.AddListener(() => SetLanguage(Language.Spanish));
        _btnEnglish.onClick.AddListener(() => SetLanguage(Language.English));

        _voiceEnabledToggle.onValueChanged.AddListener(b => OnVoiceEnabledChanged(b));
        _micDropdown.onValueChanged.AddListener(i => OnMicSelected(i));
        _btnRetryAudioOutput.onClick.AddListener(OnRetryAudioOutputClicked);

        _btnSave.onClick.AddListener(OnSave);
        _btnCancel.onClick.AddListener(OnCancel);
        _btnRestore.onClick.AddListener(OnRestore);
        _btnClose.onClick.AddListener(OnCancel);
    }

    // ── Tab navigation ───────────────────────────────────────────────────────

    void ShowTab(GameObject target)
    {
        _contentAudio.SetActive(_contentAudio    == target);
        _contentVideo.SetActive(_contentVideo    == target);
        _contentLanguage.SetActive(_contentLanguage == target);
        _contentVoice.SetActive(_contentVoice   == target);
    }

    // ── Populate controls from SettingsManager ───────────────────────────────

    void PopulateAll()
    {
        SettingsManager sm = SettingsManager.Instance;

        // Audio
        _masterSlider.SetValueWithoutNotify(sm.MasterVolume);
        _musicSlider.SetValueWithoutNotify(sm.MusicVolume);
        _sfxSlider.SetValueWithoutNotify(sm.SfxVolume);
        _voiceVolumeSlider.SetValueWithoutNotify(sm.VoiceVolume);

        // Video — resoluciones
        Resolution[] resolutions = Screen.resolutions;
        _resolutionDropdown.ClearOptions();
        var opts = new List<string>(resolutions.Length);
        foreach (Resolution r in resolutions)
            opts.Add($"{r.width}x{r.height} @ {r.refreshRate}Hz");
        _resolutionDropdown.AddOptions(opts);
        _resolutionDropdown.SetValueWithoutNotify(sm.ResolutionIndex);

        _fullscreenToggle.SetIsOnWithoutNotify(sm.Fullscreen);

        // Video — calidad
        _qualityDropdown.ClearOptions();
        _qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        _qualityDropdown.SetValueWithoutNotify(sm.QualityIndex);

        // Voz
        _voiceEnabledToggle.SetIsOnWithoutNotify(sm.VoiceEnabled);
        PopulateMicDropdown(sm.MicDevice);
        RefreshMicStatusLabel(sm.MicDevice);
        RefreshAudioOutputLabel();
    }

    void PopulateMicDropdown(string currentDevice)
    {
        _micDevices = Microphone.devices;
        _micDropdown.ClearOptions();

        var opts = new List<string>();
        opts.Add(LocalizationManager.Get("settings.voice.defaultMic"));

        foreach (string d in _micDevices)
            opts.Add(d);

        _micDropdown.AddOptions(opts);

        // Seleccionar el dispositivo guardado.
        int idx = 0;
        if (!string.IsNullOrEmpty(currentDevice))
        {
            for (int i = 0; i < _micDevices.Length; i++)
            {
                if (_micDevices[i] == currentDevice) { idx = i + 1; break; }
            }
        }
        _micDropdown.SetValueWithoutNotify(idx);

        bool hasMics = _micDevices.Length > 0;
        _micDropdown.interactable = hasMics && _voiceEnabledToggle.isOn;
        if (!hasMics)
            _micStatusLabel.text = LocalizationManager.Get("settings.voice.noMic");
    }

    void RefreshMicStatusLabel(string device)
    {
        if (_micDevices == null || _micDevices.Length == 0)
        {
            _micStatusLabel.text = LocalizationManager.Get("settings.voice.noMic");
            return;
        }
        string name = string.IsNullOrEmpty(device)
            ? LocalizationManager.Get("settings.voice.defaultMic")
            : device;
        _micStatusLabel.text = $"{LocalizationManager.Get("settings.voice.using")}: {name}";
    }

    // ── Language ─────────────────────────────────────────────────────────────

    void SetLanguage(Language lang)
    {
        SettingsManager.Instance.SetLanguage(lang);
    }

    // ── Voice ────────────────────────────────────────────────────────────────

    void OnVoiceEnabledChanged(bool enabled)
    {
        SettingsManager.Instance.SetVoiceEnabled(enabled);
        _micDropdown.interactable = enabled && (_micDevices?.Length ?? 0) > 0;
    }

    void OnMicSelected(int index)
    {
        // Índice 0 = default del OS (string vacío).
        string device = index == 0 ? "" : _micDevices[index - 1];
        SettingsManager.Instance.SetMicDevice(device);
        RefreshMicStatusLabel(device);
    }

    void OnRetryAudioOutputClicked()
    {
        SettingsManager.Instance.RetryAudioOutput();
        RefreshAudioOutputLabel();
    }

    void RefreshAudioOutputLabel()
    {
        if (_audioOutputLabel == null) return;
        _audioOutputLabel.text = $"{LocalizationManager.Get("settings.voice.output")}: {SettingsManager.Instance.DescribeAudioOutput()}";
    }

    // ── Footer buttons ────────────────────────────────────────────────────────

    void OnSave()
    {
        SettingsManager.Instance.Save();
        Close();
    }

    void OnCancel()
    {
        RestoreSnapshot();
        Close();
    }

    void OnRestore()
    {
        SettingsManager.Instance.ResetToDefaults();
        PopulateAll();
    }

    // ── Snapshot ─────────────────────────────────────────────────────────────

    void TakeSnapshot()
    {
        SettingsManager sm = SettingsManager.Instance;
        _snapMaster       = sm.MasterVolume;
        _snapMusic        = sm.MusicVolume;
        _snapSfx          = sm.SfxVolume;
        _snapVoice        = sm.VoiceVolume;
        _snapVoiceEnabled = sm.VoiceEnabled;
        _snapMic          = sm.MicDevice;
        _snapRes          = sm.ResolutionIndex;
        _snapFullscreen   = sm.Fullscreen;
        _snapQuality      = sm.QualityIndex;
        _snapLang         = (int)sm.CurrentLanguage;
    }

    void RestoreSnapshot()
    {
        SettingsManager sm = SettingsManager.Instance;
        sm.SetMasterVolume(_snapMaster);
        sm.SetMusicVolume(_snapMusic);
        sm.SetSfxVolume(_snapSfx);
        sm.SetVoiceVolume(_snapVoice);
        sm.SetVoiceEnabled(_snapVoiceEnabled);
        sm.SetMicDevice(_snapMic);
        sm.SetResolutionIndex(_snapRes);
        sm.SetFullscreen(_snapFullscreen);
        sm.SetQualityIndex(_snapQuality);
        sm.SetLanguage((Language)_snapLang);
    }
}
