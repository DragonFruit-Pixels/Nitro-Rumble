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
    [SerializeField] private TextMeshProUGUI _joinedRoomPlayers;

    public override void OnEnable()
    {
        base.OnEnable();
        _joinedRoomStartButton.onClick.AddListener(OnStartClicked);
        _joinedRoomLeaveButton.onClick.AddListener(OnLeaveClicked);

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

    public override void OnLeftRoom()
    {
        LobbyHandlerCommands.LeftRoom();
    }

    private void LoadRoomInfo()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        _joinedRoomName.SetText($"{PhotonNetwork.CurrentRoom.Name}");
    }

    private void ReloadPlayerInfo()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        List<Player> players = PhotonNetwork.CurrentRoom.Players.Values
            .Where(player => !player.IsInactive)
            .OrderBy(player => player.ActorNumber)
            .ToList();
        int          count     = players.Count;
        string       text      = string.Empty;

        for (int i = 0; i < PhotonNetwork.CurrentRoom.MaxPlayers; i++)
        {
            text += i < count
                ? $"{i + 1}. {GetDisplayName(players[i])}\n\n"
                : $"{i + 1}. -\n\n";
        }

        _joinedRoomPlayerCount.SetText($"Players: {count}/{PhotonNetwork.CurrentRoom.MaxPlayers}");
        _joinedRoomPlayers.SetText(text);
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

    private void ReloadStartButton()
    {
        if (PhotonNetwork.CurrentRoom == null || !MatchmakingManager.Instance) return;

        _joinedRoomStartButton.interactable =
            PhotonNetwork.IsMasterClient &&
            GetActivePlayerCount() >= MatchmakingManager.Instance.MinPlayers;
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
