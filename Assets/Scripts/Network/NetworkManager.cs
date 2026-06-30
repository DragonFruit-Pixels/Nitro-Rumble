using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : PunSingleton<NetworkManager>
{
    public NetworkStatus NetworkStatus { get; private set; } = NetworkStatus.Offline;
    public event Action<NetworkStatus> OnStatusChanged;
    public event Action<DisconnectCause> OnDisconnect;

    #region MonoBehaviour Methods
    public override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(this.gameObject);

        // Registrar los tipos custom del proyecto en el protocolo de Photon, una sola vez,
        // antes de conectar. Así podemos enviarlos dentro de RaiseEvent (ver RaceManager).
        PhotonCustomTypes.Register();
    }
    #endregion

    private void ChangeNetworkStatus(NetworkStatus status)
    {
        if (NetworkStatus == status) return;
        Logger.Log($"[NetworkManager] Old Status: {NetworkStatus} - New Status: {status}");
        NetworkStatus = status;
        OnStatusChanged?.Invoke(NetworkStatus);
    }
    
    public void RequestConnection()
    {
        Logger.Log("[NetworkManager] Connection Requested\n" +
                  "[NetworkManager] Connecting Using Settings...");
        
        ChangeNetworkStatus(NetworkStatus.Connecting);
        
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.ConnectUsingSettings();
    }

    public void RequestJoinLobby()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.JoinLobby(TypedLobby.Default);
    }
    
    public override void OnConnectedToMaster()
    {
        ChangeNetworkStatus(NetworkStatus.Connected);
    }

    public bool SuppressOfflineOnDisconnect { get; set; }

    public override void OnDisconnected(DisconnectCause cause)
    {
        OnDisconnect?.Invoke(cause);
        if (!SuppressOfflineOnDisconnect)
            ChangeNetworkStatus(NetworkStatus.Offline);
    }
}

public enum NetworkStatus
{
    Offline,
    Connecting,
    Connected
}
