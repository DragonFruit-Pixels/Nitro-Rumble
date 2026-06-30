using System;
using ExitGames.Client.Photon;
using Extensions;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class MatchmakingManager : PunSingleton<MatchmakingManager>
{
    [Header("Matchmaking Settings")]
    [SerializeField] private int _maxPlayers = 4;
    [SerializeField] private int _minPlayers = 2;

    public MatchmakingStatus MatchmakingStatus { get; private set; } = MatchmakingStatus.InLobby;
    public event Action<MatchmakingStatus> OnStatusChanged;
    public event Action<string>            OnFailed;

    public int MaxPlayers => _maxPlayers;
    public int MinPlayers => _minPlayers;

    public override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void ChangeMatchmakingStatus(MatchmakingStatus status)
    {
        if (MatchmakingStatus == status) return;
        Logger.Log($"[MatchmakingManager] {MatchmakingStatus} → {status}");
        MatchmakingStatus = status;
        OnStatusChanged?.Invoke(MatchmakingStatus);
    }

    public void RequestJoinRoom(string roomName)
    {
        if (roomName.IsEmpty())
        {
            PhotonNetwork.JoinRandomRoom();
            ChangeMatchmakingStatus(MatchmakingStatus.JoiningRandomRoom);
        }
        else
        {
            PhotonNetwork.JoinRoom(roomName);
            ChangeMatchmakingStatus(MatchmakingStatus.JoiningRoom);
        }
    }

    /// <summary>
    /// Crea una sala con la configuración por defecto del campeonato.
    /// El host la ajustará desde RoomConfigPanel una vez dentro.
    /// </summary>
    public void RequestCreateRoom(string roomName)
    {
        var roomOptions = new RoomOptions
        {
            IsVisible  = true,
            MaxPlayers = _maxPlayers,
            PlayerTtl    = 10000, // ms — mantiene el slot del jugador reservado tras desconexión (ReconnectAndRejoin)
            EmptyRoomTtl = 15000, // ms — mantiene la sala viva 15 s si caen TODOS, para permitir reconexión sin perderla
            // Exponer laps y raceCount al Lobby para que RoomObject los muestre
            CustomRoomPropertiesForLobby = new[] { Keys.LAPS_KEY, Keys.RACE_COUNT_KEY },
            CustomRoomProperties = new Hashtable
            {
                { Keys.LAPS_KEY,       3 },
                { Keys.RACE_COUNT_KEY, 5 },
                { Keys.MAP_POOL_KEY,   new int[0] }
            }
        };

        PhotonNetwork.CreateRoom(roomName, roomOptions);
        Logger.Log("[MatchmakingManager] Creando sala...");
        ChangeMatchmakingStatus(MatchmakingStatus.CreatingRoom);
    }

    public void RequestLeaveRoom()
    {
        if (PhotonNetwork.CurrentRoom != null)
        {
            // Leave manual: remover al jugador de la sala.
            // Las reconexiones por caida siguen usando PlayerTtl, pero el boton Leave no debe dejar un actor inactive.
            PhotonNetwork.LeaveRoom(false);
            ChangeMatchmakingStatus(MatchmakingStatus.LeavingRoom);
        }
    }

    public override void OnJoinedRoom()  => ChangeMatchmakingStatus(MatchmakingStatus.InRoom);
    public override void OnLeftRoom()    => ChangeMatchmakingStatus(MatchmakingStatus.InLobby);

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        ChangeMatchmakingStatus(MatchmakingStatus.InLobby);
        OnFailed?.Invoke(message);
    }

    // Random join can fail when no rooms are available — previously unhandled.
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        ChangeMatchmakingStatus(MatchmakingStatus.InLobby);
        OnFailed?.Invoke(message);
    }

    public override void OnCreatedRoom() => ChangeMatchmakingStatus(MatchmakingStatus.InRoom);

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        ChangeMatchmakingStatus(MatchmakingStatus.InLobby);
        OnFailed?.Invoke(message);
    }
}

public enum MatchmakingStatus
{
    InLobby,
    JoiningRoom,
    JoiningRandomRoom,
    CreatingRoom,
    InRoom,
    LeavingRoom
}
