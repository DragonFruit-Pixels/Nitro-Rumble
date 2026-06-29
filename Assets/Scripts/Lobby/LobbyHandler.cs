using System.Collections.Generic;
using Extensions;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyHandler : MonoBehaviourPunCallbacks, ILobbyHandlerCommands
{
    [Header("Lobby Screens")]
    [SerializeField] private CanvasGroup _joinOrCreateScreen;
    [SerializeField] private CanvasGroup _inRoomScreen;

    [Header("Lobby Handlers")]
    [SerializeField] private List<LobbySubcategory> _lobbySubcategories;

    [Header("Settings")]
    [SerializeField] private TrackCatalogueSO _trackCatalogue;

    private void Awake()
    {
        foreach (LobbySubcategory sub in _lobbySubcategories)
            sub.Init(this);
    }

    private void Start()
    {
        // Al volver desde una carrera/podio, seguimos en la sala pero OnJoinedRoom no refuega.
        // Detectamos el estado manualmente para mostrar la pantalla correcta.
        if (PhotonNetwork.InRoom)
        {
            JoinedRoom();
            foreach (LobbySubcategory sub in _lobbySubcategories)
                sub.OnJoinedRoom();
        }
        else
            LeftRoom();
    }

    public void RequestCreateRoom(string roomName)
    {
        if (MatchmakingManager.Instance)
            MatchmakingManager.Instance.RequestCreateRoom(roomName);
    }

    public void RequestJoinRoom(string roomName)
    {
        if (MatchmakingManager.Instance)
            MatchmakingManager.Instance.RequestJoinRoom(roomName);
    }

    public void JoinedRoom()
    {
        _joinOrCreateScreen.ToggleVisibility(false);
        _inRoomScreen.ToggleVisibility(true);
    }

    public void LeftRoom()
    {
        _joinOrCreateScreen.ToggleVisibility(true);
        _inRoomScreen.ToggleVisibility(false);
    }

    public void RequestStartGame()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null) return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        
        var props = PhotonNetwork.CurrentRoom.CustomProperties;

        int   laps      = props.TryGetValue(Keys.LAPS_KEY,       out object l)  && l  is int li ? li : 3;
        int   raceCount = props.TryGetValue(Keys.RACE_COUNT_KEY, out object rc) && rc is int ri ? ri : 1;
        int[] pool      = props.TryGetValue(Keys.MAP_POOL_KEY,   out object p)  && p  is int[] pa ? pa : null;

        // Convertir pool de IDs a scene names
        var scenes = new List<string>();
        if (pool != null)
        {
            foreach (int id in pool)
            {
                foreach (TrackSO t in _trackCatalogue.Tracks)
                {
                    if (t != null && t.TrackID == id)
                    {
                        scenes.Add(t.TrackSceneName);
                        break;
                    }
                }
            }
        }

        if (scenes.Count == 0)
        {
            Logger.LogWarning("[LobbyHandler] No hay mapas seleccionados. El host debe elegir al menos uno.");
            return;
        }

        // Shuffle Fisher-Yates
        for (int i = scenes.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (scenes[i], scenes[j]) = (scenes[j], scenes[i]);
        }

        // Construir queue de raceCount carreras ciclando el pool
        var queue = new string[raceCount];
        for (int i = 0; i < raceCount; i++)
            queue[i] = scenes[i % scenes.Count];

        RaceGridManager.AssignGridFromCurrentRoom();

        if (ChampionshipManager.Instance != null)
            ChampionshipManager.Instance.InitializeChampionship(queue, laps, raceCount);

        PhotonNetwork.LoadLevel(queue[0]);
    }

    public void RequestLeaveRoom()
    {
        if (MatchmakingManager.Instance)
            MatchmakingManager.Instance.RequestLeaveRoom();
    }
}

public interface ILobbyHandlerCommands
{
    void RequestCreateRoom(string roomName);
    void RequestJoinRoom(string roomName);
    void JoinedRoom();
    void LeftRoom();
    void RequestStartGame();
    void RequestLeaveRoom();
}

public class LobbySubcategory : MonoBehaviourPunCallbacks
{
    protected ILobbyHandlerCommands LobbyHandlerCommands;

    public void Init(ILobbyHandlerCommands handler)
    {
        LobbyHandlerCommands = handler;
    }
}
