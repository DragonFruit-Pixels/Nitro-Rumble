using System;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // Events
    public static event Action OnAudioChanged;
    public static event Action OnVideoChanged;
    public static event Action<Language> OnLanguageChanged;
    public static event Action OnVoiceChanged;

    // PlayerPrefs keys
    const string K_MASTER   = "settings.masterVolume";
    const string K_MUSIC    = "settings.musicVolume";
    const string K_SFX      = "settings.sfxVolume";
    const string K_VOICE    = "settings.voiceVolume";
    const string K_VOICE_EN = "settings.voiceEnabled";
    const string K_MIC      = "settings.micDevice";
    const string K_RES      = "settings.resolutionIndex";
    const string K_FULLSCR  = "settings.fullscreen";
    const string K_LANG     = "settings.language";
    const string K_QUALITY  = "settings.qualityIndex";

    // ── Properties ──────────────────────────────────────────────────────────

    public float MasterVolume   { get; private set; }
    public float MusicVolume    { get; private set; }
    public float SfxVolume      { get; private set; }
    public float VoiceVolume    { get; private set; }
    public bool  VoiceEnabled   { get; private set; }
    public string MicDevice     { get; private set; }
    public int   ResolutionIndex { get; private set; }
    public bool  Fullscreen     { get; private set; }
    public Language CurrentLanguage { get; private set; }
    public int   QualityIndex   { get; private set; }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        ApplyAll();
    }

    // ── Load / Save ──────────────────────────────────────────────────────────

    void Load()
    {
        MasterVolume    = PlayerPrefs.GetFloat(K_MASTER,  1f);
        MusicVolume     = PlayerPrefs.GetFloat(K_MUSIC,   1f);
        SfxVolume       = PlayerPrefs.GetFloat(K_SFX,     1f);
        VoiceVolume     = PlayerPrefs.GetFloat(K_VOICE,   1f);
        VoiceEnabled    = PlayerPrefs.GetInt(K_VOICE_EN,  1) == 1;
        MicDevice       = PlayerPrefs.GetString(K_MIC,   "");
        Fullscreen      = PlayerPrefs.GetInt(K_FULLSCR,   1) == 1;
        CurrentLanguage = (Language)PlayerPrefs.GetInt(K_LANG, 0);
        QualityIndex    = PlayerPrefs.GetInt(K_QUALITY, QualitySettings.GetQualityLevel());

        int maxRes = Screen.resolutions.Length - 1;
        ResolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(K_RES, maxRes), 0, maxRes);
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(K_MASTER,  MasterVolume);
        PlayerPrefs.SetFloat(K_MUSIC,   MusicVolume);
        PlayerPrefs.SetFloat(K_SFX,     SfxVolume);
        PlayerPrefs.SetFloat(K_VOICE,   VoiceVolume);
        PlayerPrefs.SetInt(K_VOICE_EN,  VoiceEnabled ? 1 : 0);
        PlayerPrefs.SetString(K_MIC,    MicDevice);
        PlayerPrefs.SetInt(K_RES,       ResolutionIndex);
        PlayerPrefs.SetInt(K_FULLSCR,   Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(K_LANG,      (int)CurrentLanguage);
        PlayerPrefs.SetInt(K_QUALITY,   QualityIndex);
        PlayerPrefs.Save();
    }

    public void ResetToDefaults()
    {
        MasterVolume    = 1f;
        MusicVolume     = 1f;
        SfxVolume       = 1f;
        VoiceVolume     = 1f;
        VoiceEnabled    = true;
        MicDevice       = "";
        ResolutionIndex = Screen.resolutions.Length - 1;
        Fullscreen      = true;
        CurrentLanguage = Language.Spanish;
        QualityIndex    = QualitySettings.names.Length - 1;
        Save();
        ApplyAll();
    }

    // ── Setters (llamados desde SettingsPanel) ───────────────────────────────

    public void SetMasterVolume(float v)    { MasterVolume    = v; ApplyAudio(); }
    public void SetMusicVolume(float v)     { MusicVolume     = v; OnAudioChanged?.Invoke(); }
    public void SetSfxVolume(float v)       { SfxVolume       = v; OnAudioChanged?.Invoke(); }
    public void SetVoiceVolume(float v)     { VoiceVolume     = v; OnVoiceChanged?.Invoke(); }
    public void SetVoiceEnabled(bool b)     { VoiceEnabled    = b; OnVoiceChanged?.Invoke(); }
    public void SetMicDevice(string d)      { MicDevice       = d; }
    public void SetResolutionIndex(int i)   { ResolutionIndex = i; ApplyVideo(); }
    public void SetFullscreen(bool b)       { Fullscreen      = b; ApplyVideo(); }
    public void SetLanguage(Language lang)  { CurrentLanguage = lang; LocalizationManager.SetLanguage(lang); OnLanguageChanged?.Invoke(lang); }
    public void SetQualityIndex(int i)      { QualityIndex    = i; ApplyVideo(); }

    // ── Apply ────────────────────────────────────────────────────────────────

    void ApplyAll()
    {
        ApplyAudio();
        ApplyVideo();
        LocalizationManager.SetLanguage(CurrentLanguage);
    }

    void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
        OnAudioChanged?.Invoke();
    }

    void ApplyVideo()
    {
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions.Length > 0 && ResolutionIndex < resolutions.Length)
        {
            Resolution r = resolutions[ResolutionIndex];
            Screen.SetResolution(r.width, r.height, Fullscreen);
        }
        else
        {
            Screen.fullScreen = Fullscreen;
        }

        QualitySettings.SetQualityLevel(QualityIndex, true);
        OnVideoChanged?.Invoke();
    }

    /// <summary>
    /// Aplica la configuración de voz a un Recorder recién spawneado.
    /// Llamar desde PlayerSpawner tras instanciar el auto.
    /// </summary>
    public void ApplyVoice(Recorder recorder)
    {
        if (recorder == null) return;
        recorder.TransmitEnabled  = VoiceEnabled;
        recorder.MicrophoneDevice = string.IsNullOrEmpty(MicDevice)
            ? DeviceInfo.Default
            : new DeviceInfo(MicDevice);
    }

    /// <summary>
    /// Describe el dispositivo de salida activo. También fuerza a Unity a re-detectar el
    /// default del SO (por si cambió mientras el juego estaba abierto) — Unity no sigue
    /// cambios de dispositivo en vivo de forma confiable por su cuenta.
    /// </summary>
    public string DescribeAudioOutput()
    {
        AudioSettings.Reset(AudioSettings.GetConfiguration());

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        string deviceName = WindowsAudioDeviceInfo.GetDefaultOutputDeviceName();
        if (!string.IsNullOrEmpty(deviceName))
            return deviceName;
#endif
        AudioConfiguration config = AudioSettings.GetConfiguration();
        return $"{config.sampleRate} Hz · {config.speakerMode}";
    }

    /// <summary>
    /// Lista de dispositivos de salida disponibles (solo informativa — Unity no permite
    /// elegir a cuál reproducir, siempre usa el default del SO). Vacía fuera de Windows.
    /// </summary>
    public string[] GetAvailableOutputDevices()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return WindowsAudioDeviceInfo.GetOutputDeviceNames();
#else
        return new string[0];
#endif
    }
}
