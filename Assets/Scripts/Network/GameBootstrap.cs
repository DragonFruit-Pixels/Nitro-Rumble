using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Solo para testing: conecta a Photon y une una sala fija antes de que
/// PlayerSpawner spawee el auto. Permite entrar directo a la escena de juego
/// sin pasar por el menú/lobby.
/// Desactivá este GameObject en el build final.
/// </summary>
public class GameBootstrap : MonoBehaviourPunCallbacks
{
    [SerializeField] private string _testRoomName = "TestRoom";
    [SerializeField] private int    _maxPlayers   = 2;
    [SerializeField] private PlayerSpawner _spawner;

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            // Ya conectado (venimos del menú/lobby) — el spawner arranca normalmente.
            Logger.Log("[GameBootstrap] Ya en sala — no es necesario reconectar.");
            return;
        }

        Logger.Log("[GameBootstrap] Conectando a Photon para testing...");
        _spawner.enabled = false; // esperar hasta estar en sala
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Logger.Log("[GameBootstrap] Conectado — uniéndose a sala de test...");
        RoomOptions options = new RoomOptions { MaxPlayers = _maxPlayers };
        PhotonNetwork.JoinOrCreateRoom(_testRoomName, options, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Logger.Log($"[GameBootstrap] En sala '{_testRoomName}' — spawneando auto.");
        _spawner.enabled = true;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Logger.LogError($"[GameBootstrap] Error al unirse a sala: {message}");
    }
}
