using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuHandler : MonoBehaviourPunCallbacks, ICarChooserListener
{
    [Header("Basic Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _settingsButton;

    [Header("Settings")]
    [SerializeField] private SettingsPanel _settingsPanel;

    [Header("Customization - Car Skin")]
    [SerializeField] private GameObject _carButtonContainer;
    [SerializeField] private CarSkinCatalogueSO _carSkinCatalogue;
    [SerializeField] private CarChooseButton _carButtonPrefab;
    [SerializeField] private CarSkinLoader _carSkinLoader;

    // Botones de skin instanciados (se rastrean para poder reconstruir si LiveOps cambia la disponibilidad).
    private readonly List<CarChooseButton> _spawnedCarButtons = new List<CarChooseButton>();

    public override void OnEnable()
    {
        _playButton.onClick.AddListener(OnPlayPressed);
        _quitButton.onClick.AddListener(OnQuitPressed);
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(OnSettingsPressed);

        // LiveOps: si la config llega DESPUÉS de cargar el menú, reconstruir los botones.
        if (LiveOpsConfig.Instance != null)
            LiveOpsConfig.Instance.OnConfigApplied += OnLiveOpsConfigApplied;
    }

    public override void OnDisable()
    {
        _playButton.onClick.RemoveListener(OnPlayPressed);
        _quitButton.onClick.RemoveListener(OnQuitPressed);
        if (_settingsButton != null)
            _settingsButton.onClick.RemoveListener(OnSettingsPressed);

        if (LiveOpsConfig.Instance != null)
            LiveOpsConfig.Instance.OnConfigApplied -= OnLiveOpsConfigApplied;
    }

    private void Awake()
    {
        RebuildCarButtons();
    }

    private void OnLiveOpsConfigApplied()
    {
        RebuildCarButtons();
        EnsureSelectedSkinAvailable();
    }

    private void RebuildCarButtons()
    {
        if (_carSkinCatalogue == null || _carButtonPrefab == null || _carButtonContainer == null)
            return;

        // Limpiar botones previos (por si cambió la disponibilidad de skins en LiveOps).
        foreach (CarChooseButton button in _spawnedCarButtons)
            if (button != null) Destroy(button.gameObject);
        _spawnedCarButtons.Clear();

        foreach (CarSkinSO skin in _carSkinCatalogue.Skins)
        {
            if (skin == null) continue;

            // LiveOps: ocultar skins deshabilitadas desde Remote Config (fail-open si no está listo).
            if (LiveOpsConfig.Instance != null && !LiveOpsConfig.Instance.IsSkinAvailable(skin.skinID))
                continue;

            CarChooseButton carButton = Instantiate(_carButtonPrefab, _carButtonContainer.transform);
            carButton.Init(this, skin);
            _spawnedCarButtons.Add(carButton);
        }
    }

    /// <summary>
    /// Si la skin guardada quedó deshabilitada por LiveOps, cambiar a la primera disponible
    /// para que el jugador no entre a la carrera con una skin bloqueada.
    /// </summary>
    private void EnsureSelectedSkinAvailable()
    {
        if (LiveOpsConfig.Instance == null || _carSkinCatalogue == null) return;
        if (LocalSaveManager.Instance == null) return;

        int current = LocalSaveManager.Instance.Profile.selectedSkin;
        if (LiveOpsConfig.Instance.IsSkinAvailable(current)) return;

        foreach (CarSkinSO skin in _carSkinCatalogue.Skins)
        {
            if (skin == null) continue;
            if (!LiveOpsConfig.Instance.IsSkinAvailable(skin.skinID)) continue;

            Logger.Log($"[LiveOps] Skin {current} deshabilitada; cambiando a {skin.skinID} ({skin.SkinName}).");

            if (_carSkinLoader != null)
                _carSkinLoader.ChangeCurrentSkin(skin);   // guarda en LSM + actualiza visual
            else
                LocalSaveManager.Instance.SaveSkin(skin.skinID);
            return;
        }
    }

    public void ChangeCarSkin(CarSkinSO skin)
    {
        _carSkinLoader?.ChangeCurrentSkin(skin);
    }

    private void OnPlayPressed()
    {
        _playButton.interactable = false;

        // Invitado: sin nombre → auto anónimo. Logueado: el NickName ya lo fijó AuthSession al loguear.
        if (!AuthSession.IsLoggedIn)
            PhotonNetwork.NickName = string.Empty;

        NetworkManager.Instance.RequestConnection();
    }

    private void OnSettingsPressed()
    {
        _settingsPanel?.Open();
    }

    private void OnQuitPressed()
    {
        _quitButton.interactable = false;

        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}

public interface ICarChooserListener
{
    public void ChangeCarSkin(CarSkinSO skin);
}
