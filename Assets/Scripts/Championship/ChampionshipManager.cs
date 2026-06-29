using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Singleton DontDestroyOnLoad que orquesta el campeonato estilo Mario Kart.
/// Vive desde Menu → Lobby → todas las carreras → Podio.
/// Solo el Master Client escribe en Room Custom Properties; todos leen de ahí.
/// </summary>
public class ChampionshipManager : Singleton<ChampionshipManager>, IInRoomCallbacks
{
    // PhotonNetwork.LoadLevel puede ignorar una recarga si el cliente ya está en esa escena.
    // Pasamos por Loading solo entre carreras, donde puede repetirse la misma pista.
    private const string LoadingSceneName = "Loading";
    private const string PowerUpBoxPrefix = "pu_box_";
    private const int PowerUpBoxAvailable = -1;

    public int TotalRaces  { get; private set; }
    public int CurrentRace { get; private set; }   // 0-based
    public int TotalLaps   { get; private set; }

    private string[] _mapQueue;                    // scene names en orden de juego
    private int[]    _points = new int[4];         // indexed por actorNumber - 1
    private string   _nextScene;

    public bool IsActive => TotalRaces > 0 && _mapQueue != null && _mapQueue.Length > 0;

    public event Action OnStandingsUpdated;

    public override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (PhotonNetwork.InRoom)
            SyncFromRoomProperties();
    }

    // ── Inicialización (solo Master Client, al hacer click en Iniciar) ──────────

    public void InitializeChampionship(string[] mapQueue, int laps, int raceCount)
    {
        _mapQueue   = mapQueue;
        TotalLaps   = laps;
        TotalRaces  = raceCount;
        CurrentRace = 0;
        _points     = new int[GetRequiredActorCapacity()];

        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

        var props = new Hashtable
        {
            { Keys.MAP_QUEUE_KEY,    mapQueue },
            { Keys.LAPS_KEY,         laps },
            { Keys.RACE_COUNT_KEY,   raceCount },
            { Keys.CURRENT_RACE_KEY, 0 },
            { Keys.POINTS_KEY,       _points }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    // ── Sync desde Room Properties (todos los clientes en cada scene load) ──────

    public void SyncFromRoomProperties()
    {
        if (!PhotonNetwork.InRoom) return;
        var props = PhotonNetwork.CurrentRoom.CustomProperties;

        if (props.TryGetValue(Keys.MAP_QUEUE_KEY, out object q) && q is string[] queue)
            _mapQueue = queue;

        if (props.TryGetValue(Keys.LAPS_KEY, out object l) && l is int laps)
            TotalLaps = laps;

        if (props.TryGetValue(Keys.RACE_COUNT_KEY, out object rc) && rc is int raceCount)
            TotalRaces = raceCount;

        if (props.TryGetValue(Keys.CURRENT_RACE_KEY, out object cr) && cr is int curRace)
            CurrentRace = curRace;

        if (props.TryGetValue(Keys.POINTS_KEY, out object pts) && pts is int[] ptsArr)
            _points = ptsArr;
        else
            _points = new int[4];

        if (props.TryGetValue(Keys.NEXT_SCENE_KEY, out object ns) && ns is string nextScene)
            _nextScene = nextScene;

        OnStandingsUpdated?.Invoke();
    }

    // ── Registrar resultados de carrera (llamado por RaceResultsPanel) ────────────

    /// <summary>
    /// Calcula y acumula los puntos de esta carrera.
    /// Devuelve el array de puntos ganados (indexed por actorNumber-1) para mostrar en UI.
    /// </summary>
    public int[] RecordRaceResults(IReadOnlyList<Racer> racersByPosition)
    {
        if (_points == null || _points.Length < 4)
            _points = new int[4];

        foreach (var racer in racersByPosition)
        {
            int actorNum = racer.photonView?.Owner?.ActorNumber ?? 0;
            EnsurePointsCapacity(actorNum);
        }

        int[] earned = new int[_points.Length];

        foreach (var racer in racersByPosition)
        {
            // DNF: no llegó a la meta → 0 puntos
            if (!racer.IsFinished) continue;

            int actorNum = racer.photonView?.Owner?.ActorNumber ?? 0;
            if (actorNum < 1) continue;

            int pts                = PointsSystem.GetPoints(racer.Position);
            _points[actorNum - 1] += pts;
            earned[actorNum - 1]   = pts;
        }

        // Solo el MC escribe en Room Props; los demás calculan igual (determinístico).
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            var props = new Hashtable { { Keys.POINTS_KEY, _points } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        OnStandingsUpdated?.Invoke();
        return earned;
    }

    // ── Avanzar al siguiente mapa o al Podio (solo Master Client) ────────────────

    public void AdvanceChampionship()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        CurrentRace++;

        string nextScene;
        if (CurrentRace >= TotalRaces)
            nextScene = "Podium";
        else if (_mapQueue != null && CurrentRace < _mapQueue.Length)
            nextScene = _mapQueue[CurrentRace];
        else
            nextScene = "Podium";

        PrepareNetworkStateForNextScene(nextScene);
        PhotonNetwork.LoadLevel(nextScene == "Podium" ? nextScene : LoadingSceneName);
    }
    // -- Consultas ────────────────────────────────────────────────────────────────

    public int GetPoints(int actorNumber)
    {
        if (_points == null || actorNumber < 1 || actorNumber > _points.Length) return 0;
        return _points[actorNumber - 1];
    }

    private int GetRequiredActorCapacity()
    {
        int capacity = 4;
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return capacity;

        foreach (int actorNumber in PhotonNetwork.CurrentRoom.Players.Keys)
            if (actorNumber > capacity)
                capacity = actorNumber;

        return capacity;
    }

    private void EnsurePointsCapacity(int actorNumber)
    {
        if (actorNumber < 1) return;

        if (_points == null)
        {
            _points = new int[Math.Max(4, actorNumber)];
            return;
        }

        if (actorNumber > _points.Length)
            Array.Resize(ref _points, actorNumber);
    }

    public List<(Player player, int points)> GetStandings()
    {
        var result = new List<(Player player, int points)>();
        if (!PhotonNetwork.InRoom) return result;

        foreach (var kv in PhotonNetwork.CurrentRoom.Players)
            result.Add((kv.Value, GetPoints(kv.Value.ActorNumber)));

        result.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return result;
    }

    public string GetCurrentMapSceneName()
    {
        if (_mapQueue == null || CurrentRace < 0 || CurrentRace >= _mapQueue.Length)
            return null;
        return _mapQueue[CurrentRace];
    }


    public string GetNextSceneName()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return GetCurrentMapSceneName();

        if (!string.IsNullOrEmpty(_nextScene))
            return _nextScene;

        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Keys.NEXT_SCENE_KEY, out object next)
            ? next as string
            : GetCurrentMapSceneName();
    }

    private void PrepareNetworkStateForNextScene(string nextScene)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;

        _nextScene = nextScene;

        var props = new Hashtable
        {
            { Keys.CURRENT_RACE_KEY, CurrentRace },
            { Keys.NEXT_SCENE_KEY, nextScene }
        };

        foreach (object keyObj in PhotonNetwork.CurrentRoom.CustomProperties.Keys)
        {
            if (keyObj is string key && key.StartsWith(PowerUpBoxPrefix, StringComparison.Ordinal))
                props[key] = PowerUpBoxAvailable;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        if (nextScene != "Podium")
            RaceGridManager.AssignGridFromCurrentRoom();

        PhotonNetwork.DestroyAll();
    }
    // ── IInRoomCallbacks ─────────────────────────────────────────────────────────
    // Necesario para que el no-MC reciba los Room Props actualizados ANTES de que
    // la escena cargue, evitando la race condition donde los puntos del campeonato
    // anterior se leían antes de que POINTS_KEY = new int[4] llegara de la red.

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (PhotonNetwork.InRoom)
            SyncFromRoomProperties();
    }

    public void OnPlayerEnteredRoom(Player newPlayer) { }
    public void OnPlayerLeftRoom(Player otherPlayer) { }
    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }
    public void OnMasterClientSwitched(Player newMasterClient) { }
}
