using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class NetworkStatusText : MonoBehaviourPunCallbacks
{
    [Header("Network Status Texts")]
    [SerializeField] private string _offlineText = "Status: Offline - Press Play To Connect";
    [SerializeField] private string _connectingText = "Status: Connecting...";
    [SerializeField] private string _connectedText = "Status: Connected!";
    
    private TextMeshProUGUI _networkStatusText;

    private void Awake()
    {
        _networkStatusText = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        TrySubscribeToEvents();
        
        // Get first status
        UpdateStatusText(NetworkManager.Instance.NetworkStatus);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        TrySubscribeToEvents();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        NetworkManager.Instance.OnStatusChanged -= UpdateStatusText;
    }

    private void TrySubscribeToEvents()
    {
        if (NetworkManager.Instance)
        {
            NetworkManager.Instance.OnStatusChanged -= UpdateStatusText;            
            NetworkManager.Instance.OnStatusChanged += UpdateStatusText;
        }
    }

    private void UpdateStatusText(NetworkStatus newStatus)
    {
        switch (newStatus)
        {
            case NetworkStatus.Offline:
                _networkStatusText.text = _offlineText;
                break;
            case NetworkStatus.Connecting:
                _networkStatusText.text = _connectingText;
                break;
            case NetworkStatus.Connected:
                _networkStatusText.text = _connectedText;
                break;
        }
    }
}
