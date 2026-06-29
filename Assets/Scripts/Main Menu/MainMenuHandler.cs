using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuHandler : MonoBehaviourPunCallbacks, ICarChooserListener
{
    [Header("Basic Buttons")] 
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _quitButton;

    [Header("Customization - Name")]
    [SerializeField] private TMP_InputField _playerName;
    
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
        
        _playerName.onValueChanged.AddListener(OnNameChanged);
        _playerName.onEndEdit.AddListener(OnNameEditDone);

        // LiveOps: si la config llega DESPUÉS de cargar el menú, reconstruir los botones.
        if (LiveOpsConfig.Instance != null)
            LiveOpsConfig.Instance.OnConfigApplied += OnLiveOpsConfigApplied;
    }

    public override void OnDisable()
    {
        _playButton.onClick.RemoveListener(OnPlayPressed);
        _quitButton.onClick.RemoveListener(OnQuitPressed);
        
        _playerName.onValueChanged.RemoveListener(OnNameChanged);
        _playerName.onEndEdit.RemoveListener(OnNameEditDone);

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

    private void Start()
    {
        _playerName.characterLimit = 16;
        LoadPlayerName();
    }

    private void LoadPlayerName()
    {
        if (!LocalSaveManager.Instance) return;

        string playerName = LocalSaveManager.Instance.Profile.nickname;

        if (playerName != string.Empty)
            _playerName.text = playerName;
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

        // Si el jugador dejó el campo vacío, asignar nombre random antes de conectar.
        if (_playerName != null && string.IsNullOrWhiteSpace(_playerName.text))
        {
            string fallback = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
            _playerName.text = fallback; // dispara OnNameChanged → guarda en LSM y setea NickName
        }

        NetworkManager.Instance.RequestConnection();
    }

    private void OnQuitPressed()
    {
        _quitButton.interactable = false;
        
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
    
    private void OnNameChanged(string newName)
    {
        // Solo actualiza el NickName en memoria — sin I/O de disco por cada tecla.
        PhotonNetwork.LocalPlayer.NickName = newName;
    }

    private void OnNameEditDone(string newName)
    {
        // Guarda en disco solo cuando el jugador termina de escribir (Enter o click afuera).
        if (LocalSaveManager.Instance)
            LocalSaveManager.Instance.SaveNickname(newName);
    }
}

public interface ICarChooserListener
{
    public void ChangeCarSkin(CarSkinSO skin);
}
