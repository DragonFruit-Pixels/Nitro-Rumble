using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class ReconnectionManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private int   _maxRetries  = 3;
    [SerializeField] private float _retryDelay  = 2f;

    public static ReconnectionManager Instance { get; private set; }

    private int  _retryCount;
    private bool _reconnecting;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnDisconnect += OnNetworkDisconnect;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnDisconnect -= OnNetworkDisconnect;
    }

    private void OnNetworkDisconnect(DisconnectCause cause)
    {
        if (_reconnecting) return;
        if (!IsUnexpectedDisconnect(cause)) return;

        _retryCount   = 0;
        _reconnecting = true;
        NetworkManager.Instance.SuppressOfflineOnDisconnect = true;
        StartCoroutine(ReconnectRoutine());
    }

    private IEnumerator ReconnectRoutine()
    {
        while (_retryCount < _maxRetries)
        {
            _retryCount++;
            Logger.Log($"[ReconnectionManager] Intento {_retryCount}/{_maxRetries} en {_retryDelay}s...");

            yield return new WaitForSeconds(_retryDelay);

            if (!PhotonNetwork.ReconnectAndRejoin())
            {
                Logger.Log("[ReconnectionManager] ReconnectAndRejoin() falló inmediatamente.");
                Fail();
                yield break;
            }

            // Esperar callback: OnJoinedRoom, OnJoinRoomFailed u OnDisconnected
            yield break;
        }

        Fail();
    }

    public override void OnJoinedRoom()
    {
        if (!_reconnecting) return;
        Logger.Log("[ReconnectionManager] Reconexión exitosa.");
        Reset();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (!_reconnecting) return;
        Logger.Log($"[ReconnectionManager] No se pudo rejoin la sala: {message}");
        Fail();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!_reconnecting) return;

        if (_retryCount < _maxRetries)
        {
            Logger.Log($"[ReconnectionManager] Desconexión durante reconexión ({cause}), reintentando...");
            StartCoroutine(ReconnectRoutine());
        }
        else
        {
            Fail();
        }
    }

    private void Fail()
    {
        Logger.Log("[ReconnectionManager] Reconexión fallida. Volviendo al menú.");
        Reset();
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.SuppressOfflineOnDisconnect = false;
        PhotonNetwork.Disconnect();
    }

    private void Reset()
    {
        _reconnecting = false;
        _retryCount   = 0;
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.SuppressOfflineOnDisconnect = false;
    }

    private static bool IsUnexpectedDisconnect(DisconnectCause cause)
    {
        switch (cause)
        {
            case DisconnectCause.DisconnectByClientLogic:
            case DisconnectCause.None:
                return false;
            default:
                return true;
        }
    }
}
