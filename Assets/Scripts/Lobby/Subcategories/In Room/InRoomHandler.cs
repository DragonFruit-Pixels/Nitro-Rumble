using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InRoomHandler : LobbySubcategory
{
    [Header("In Room - Título")]
    [SerializeField] private TextMeshProUGUI _joinedRoomName;

    [Header("In Room - Botones")]
    [SerializeField] private Button _joinedRoomStartButton;
    [SerializeField] private Button _joinedRoomLeaveButton;

    [Header("In Room - Jugadores")]
    [SerializeField] private TextMeshProUGUI _joinedRoomPlayerCount;
    [SerializeField] private TextMeshProUGUI[] _playerChipLabels;

    public override void OnEnable()
    {
        base.OnEnable();
        _joinedRoomStartButton.onClick.AddListener(OnStartClicked);
        _joinedRoomLeaveButton.onClick.AddListener(OnLeaveClicked);
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;

        if (PhotonNetwork.InRoom)
        {
            LoadRoomInfo();
            ReloadPlayerInfo();
            ReloadStartButton();
        }
    }

    public override void OnDisable()
    {
        base.OnDisable();
        _joinedRoomStartButton.onClick.RemoveListener(OnStartClicked);
        _joinedRoomLeaveButton.onClick.RemoveListener(OnLeaveClicked);
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(Language _)
    {
        if (PhotonNetwork.InRoom)
            ReloadPlayerInfo();
    }

    public override void OnJoinedRoom()
    {
        LobbyHandlerCommands.JoinedRoom();
        LoadRoomInfo();
        ReloadPlayerInfo();
        ReloadStartButton();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        ReloadPlayerInfo();
        ReloadStartButton();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        ReloadPlayerInfo();
        ReloadStartButton();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        ReloadPlayerInfo();
        ReloadStartButton();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        ReloadStartButton();
    }

// El stepper de Bots (RoomConfigPanel) cambia esta property — hay que refrescar la
    // lista de jugadores/bots ni bien cambia, no solo cuando entra/sale un jugador real.
public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey(Keys.BOTS_COUNT_KEY))
        {
            ReloadPlayerInfo();
            ReloadStartButton();
        }
    }


    public override void OnLeftRoom()
    {
        LobbyHandlerCommands.LeftRoom();
    }

    private void LoadRoomInfo()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        _joinedRoomName.SetText($"{PhotonNetwork.CurrentRoom.Name}");
    }

private void ReloadPlayerInfo() => ReloadPlayerInfo(null);

    // botsCountOverride: usado por RoomConfigPanel para reflejar el cambio de bots de una
    // en la propia UI del Master Client sin esperar el round-trip de Room Custom Properties
    // (PUN no actualiza el cache local ni dispara OnRoomPropertiesUpdate para quien hizo el
    // cambio hasta que el evento vuelve del servidor).
    private void ReloadPlayerInfo(int? botsCountOverride)
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        List<Player> players = PhotonNetwork.CurrentRoom.Players.Values
            .Where(player => !player.IsInactive)
            .OrderBy(player => player.ActorNumber)
            .ToList();
        int count = players.Count;

        int botsCount = 0;
        if (botsCountOverride.HasValue)
            botsCount = botsCountOverride.Value;
        else if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Keys.BOTS_COUNT_KEY, out object botsObj) && botsObj is int bc)
            botsCount = bc;

        _joinedRoomPlayerCount.SetText(string.Format(LocalizationManager.Get("inroom.playersCount"), count, PhotonNetwork.CurrentRoom.MaxPlayers));

        if (_playerChipLabels == null) return;

        for (int i = 0; i < _playerChipLabels.Length; i++)
        {
            if (_playerChipLabels[i] == null) continue;

            string label;
            if (i < count)
                label = GetDisplayName(players[i]);
            else if (i < count + botsCount)
                label = Racer.BotNames[(i - count) % Racer.BotNames.Length];
            else
                label = "-";

            _playerChipLabels[i].SetText(label);
        }
    }

public void NotifyBotsCountChanged(int botsCount)
    {
        ReloadPlayerInfo(botsCount);
        ReloadStartButton(botsCount);
    }

    private static string GetDisplayName(Player player)
    {
        if (player == null) return "-";

        if (!string.IsNullOrWhiteSpace(player.NickName))
            return player.NickName;

        if (player.IsLocal)
        {
            if (!string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
                return PhotonNetwork.NickName;

            if (LocalSaveManager.Instance != null &&
                !string.IsNullOrWhiteSpace(LocalSaveManager.Instance.Profile.nickname))
                return LocalSaveManager.Instance.Profile.nickname;
        }

        return $"Player {player.ActorNumber}";
    }

private void ReloadStartButton() => ReloadStartButton(null);

    // botsCountOverride: mismo motivo que en ReloadPlayerInfo — el Master Client necesita ver
    // el boton habilitarse al toque al sumar un bot, sin esperar el round-trip de red.
    private void ReloadStartButton(int? botsCountOverride)
    {
        if (PhotonNetwork.CurrentRoom == null || !MatchmakingManager.Instance) return;

        int botsCount = 0;
        if (botsCountOverride.HasValue)
            botsCount = botsCountOverride.Value;
        else if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Keys.BOTS_COUNT_KEY, out object botsObj) && botsObj is int bc)
            botsCount = bc;

        // Los bots cuentan para el minimo de participantes: la gracia de sumarlos es justamente
        // poder arrancar con menos humanos reales.
        int totalRacers = GetActivePlayerCount() + botsCount;

        _joinedRoomStartButton.interactable =
            PhotonNetwork.IsMasterClient &&
            totalRacers >= MatchmakingManager.Instance.MinPlayers;
    }

    private static int GetActivePlayerCount()
    {
        if (PhotonNetwork.CurrentRoom == null) return 0;

        return PhotonNetwork.CurrentRoom.Players.Values.Count(player => !player.IsInactive);
    }

    private void OnStartClicked()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        LobbyHandlerCommands.RequestStartGame();
    }

    private void OnLeaveClicked()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        LobbyHandlerCommands.RequestLeaveRoom();
    }
}
