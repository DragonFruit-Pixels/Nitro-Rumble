using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : Singleton<GameSceneManager>
{
    [Header("Scene References")]
    [SerializeField] private string _mainMenuScene = "Menu";
    [SerializeField] private string _lobbyScene = "Lobby";
    
    [Header("Other References")]
    [SerializeField] private TransitionManager _transitionManager;
    
    public override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        TrySubscribeToEvents();
    }

    private void OnEnable()
    {
        TrySubscribeToEvents();
    }

    private void OnDisable()
    {
        NetworkManager.Instance.OnStatusChanged -= OnNetworkStatusChanged;
    }
    
    private void TrySubscribeToEvents()
    {
        if (NetworkManager.Instance)
        {
            NetworkManager.Instance.OnStatusChanged -= OnNetworkStatusChanged;
            NetworkManager.Instance.OnStatusChanged += OnNetworkStatusChanged;
        }
    }

    private void OnNetworkStatusChanged(NetworkStatus newStatus)
    {
        switch (newStatus)
        {
            case NetworkStatus.Connected:
                _transitionManager.TransitionToScene(_lobbyScene);
                break;
            case NetworkStatus.Offline:
                _transitionManager.TransitionToScene(_mainMenuScene);
                break;
        }
    }
}
