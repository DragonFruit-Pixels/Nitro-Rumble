using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de configuración del campeonato dentro de la sala.
/// Host puede editar; no-host ve en modo lectura.
/// Se sincroniza via Room Custom Properties.
/// </summary>
public class RoomConfigPanel : LobbySubcategory
{
    [Header("Mapa Pool")]
    [SerializeField] private TrackCatalogueSO _trackCatalogue;
    [SerializeField] private Transform        _toggleContainer;
    [SerializeField] private GameObject       _togglePrefab;   // Tiene Toggle + TMP_Text hijo

    [Header("Carreras")]
    [SerializeField] private Button   _racesDown;
    [SerializeField] private Button   _racesUp;
    [SerializeField] private TMP_Text _racesLabel;

    [Header("Resumen")]
    [SerializeField] private TMP_Text _summaryLabel;

    private int                               _raceCount = 5;
    private readonly HashSet<int>             _pool      = new();
    private readonly List<(TrackSO Track, Toggle Toggle, GameObject Go)> _toggles = new();
    private bool                              _anyTogglesSpawnedYet;

    private void Start()
    {
        SpawnToggles();
        RefreshStepperLabels();
        RefreshSummary();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        if (_racesDown != null) _racesDown.onClick.AddListener(() => ChangeRaces(-1));
        if (_racesUp   != null) _racesUp.onClick.AddListener(()   => ChangeRaces(+1));

        // LiveOps: si la config cambia mientras el panel está abierto (poll del dashboard,
        // resume desde background, etc.), reconstruir la lista de mapas disponibles.
        if (LiveOpsConfig.Instance != null)
            LiveOpsConfig.Instance.OnConfigApplied += OnLiveOpsConfigApplied;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (_racesDown != null) _racesDown.onClick.RemoveAllListeners();
        if (_racesUp   != null) _racesUp.onClick.RemoveAllListeners();

        if (LiveOpsConfig.Instance != null)
            LiveOpsConfig.Instance.OnConfigApplied -= OnLiveOpsConfigApplied;
    }

    private void OnLiveOpsConfigApplied()
    {
        RebuildToggles();
    }

    public override void OnJoinedRoom()
    {
        RefreshInteractable();
        if (PhotonNetwork.IsMasterClient)
            PushToRoomProperties();
        else
            SyncFromProps(PhotonNetwork.CurrentRoom.CustomProperties);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient)
            SyncFromProps(changedProps);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshInteractable();
    }

    // ── Toggles ────────────────────────────────────────────────────────────────

    private void SpawnToggles()
    {
        if (_trackCatalogue == null || _togglePrefab == null || _toggleContainer == null) return;

        foreach (TrackSO track in _trackCatalogue.Tracks)
        {
            if (track == null) continue;

            // LiveOps: ocultar mapas deshabilitados desde Remote Config.
            // (Si el servicio no está listo, fail-open: el mapa se muestra.)
            if (LiveOpsConfig.Instance != null && !LiveOpsConfig.Instance.IsTrackAvailable(track.TrackID))
                continue;

            GameObject go     = Instantiate(_togglePrefab, _toggleContainer);
            Toggle     toggle = go.GetComponentInChildren<Toggle>();
            TMP_Text   label  = go.GetComponentInChildren<TMP_Text>();

            if (label  != null) label.text = track.TrackName;

            if (toggle != null)
            {
                int id = track.TrackID;
                // Si el toggle ya existía en el pool (por una reconstrucción), respetar su estado.
                toggle.SetIsOnWithoutNotify(_pool.Contains(id) || !_anyTogglesSpawnedYet);
                if (toggle.isOn) _pool.Add(id);
                toggle.onValueChanged.AddListener(isOn => OnToggleChanged(id, isOn));
            }

            _toggles.Add((track, toggle, go));
        }

        _anyTogglesSpawnedYet = true;
    }

    /// <summary>
    /// Reconstruye la lista de toggles de mapas para reflejar la disponibilidad actual de LiveOps.
    /// Se llama al iniciar y cada vez que LiveOpsConfig aplica una config nueva (poll, resume, etc.).
    /// </summary>
    private void RebuildToggles()
    {
        if (_toggleContainer == null) return;

        // Si un mapa que estaba en el pool quedó deshabilitado, sacarlo del pool
        // (solo el master client toca el pool/propiedades de sala).
        if (PhotonNetwork.IsMasterClient && LiveOpsConfig.Instance != null)
        {
            _pool.RemoveWhere(id => !LiveOpsConfig.Instance.IsTrackAvailable(id));
        }

        foreach (var entry in _toggles)
            if (entry.Go != null) Destroy(entry.Go);
        _toggles.Clear();

        SpawnToggles();
        RefreshInteractable();
        RefreshSummary();

        if (PhotonNetwork.IsMasterClient)
            PushToRoomProperties();
    }

    private void OnToggleChanged(int trackID, bool isOn)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (isOn) _pool.Add(trackID);
        else      _pool.Remove(trackID);
        PushToRoomProperties();
        RefreshSummary();
    }

    // ── Steppers ────────────────────────────────────────────────────────────────

    private void ChangeRaces(int delta)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        _raceCount = Mathf.Clamp(_raceCount + delta, 1, 10);
        RefreshStepperLabels();
        PushToRoomProperties();
        RefreshSummary();
    }

    private void RefreshStepperLabels()
    {
        if (_racesLabel != null) _racesLabel.text = _raceCount.ToString();
    }

    // ── Room Properties ─────────────────────────────────────────────────────────

    private void PushToRoomProperties()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

        int[] poolArray = new int[_pool.Count];
        int   idx       = 0;
        foreach (int id in _pool) poolArray[idx++] = id;

        var props = new Hashtable
        {
            { Keys.LAPS_KEY,       3 },          // siempre 3, no configurable
            { Keys.RACE_COUNT_KEY, _raceCount },
            { Keys.MAP_POOL_KEY,   poolArray }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    private void SyncFromProps(Hashtable props)
    {
        bool changed = false;

        if (props.TryGetValue(Keys.RACE_COUNT_KEY, out object rc) && rc is int raceCount)
        {
            _raceCount = raceCount;
            changed = true;
        }
        if (props.TryGetValue(Keys.MAP_POOL_KEY, out object p) && p is int[] pool)
        {
            _pool.Clear();
            foreach (int id in pool) _pool.Add(id);

            foreach (var entry in _toggles)
                if (entry.Toggle != null)
                    entry.Toggle.SetIsOnWithoutNotify(_pool.Contains(entry.Track.TrackID));
            changed = true;
        }

        if (!changed) return;
        RefreshStepperLabels();
        RefreshSummary();
    }

    private void RefreshInteractable()
    {
        bool host = PhotonNetwork.IsMasterClient;

        if (_racesDown != null) _racesDown.interactable = host;
        if (_racesUp   != null) _racesUp.interactable   = host;

        foreach (var entry in _toggles)
            if (entry.Toggle != null) entry.Toggle.interactable = host;
    }

    private void RefreshSummary()
    {
        if (_summaryLabel == null) return;
        _summaryLabel.text = $"{_raceCount} Races · 3 Laps · {_pool.Count} Maps in pool";
    }
}