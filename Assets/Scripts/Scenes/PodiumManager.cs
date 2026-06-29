using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Maneja la escena de podio final del campeonato.
/// Lee el estado del ChampionshipManager y muestra el ranking completo.
/// </summary>
public class PodiumManager : MonoBehaviourPunCallbacks
{
    [Header("Podio (1°, 2°, 3°)")]
    [SerializeField] private TMP_Text _firstPlaceName;
    [SerializeField] private TMP_Text _firstPlacePoints;
    [SerializeField] private TMP_Text _secondPlaceName;
    [SerializeField] private TMP_Text _secondPlacePoints;
    [SerializeField] private TMP_Text _thirdPlaceName;
    [SerializeField] private TMP_Text _thirdPlacePoints;

    [Header("Tabla completa")]
    [SerializeField] private Transform  _standingsContainer;
    [SerializeField] private GameObject _rowPrefab;

    [Header("Botones")]
    [SerializeField] private Button _returnButton;
    [SerializeField] private TMP_Text _returnButtonLabel;
    [SerializeField] private Button _leaveButton;
    [SerializeField] private TMP_Text _leaveButtonLabel;

    private void Start()
    {
        // Sincronizar por si el ChampionshipManager no recibió sceneLoaded aún
        if (ChampionshipManager.Instance != null)
            ChampionshipManager.Instance.SyncFromRoomProperties();

        SaveTrophyIfWon();
        SetupReturnButton();
        PopulatePodium();
        PopulateStandings();
    }

    private void OnDestroy()
    {
        if (_returnButton != null)
            _returnButton.onClick.RemoveListener(OnReturnClicked);

        if (_leaveButton != null)
            _leaveButton.onClick.RemoveListener(OnLeaveClicked);
    }

    private void SaveTrophyIfWon()
    {
        if (!PhotonNetwork.InRoom) return;
        var standings = ChampionshipManager.Instance?.GetStandings();
        if (standings == null || standings.Count == 0) return;
        if (standings[0].player == PhotonNetwork.LocalPlayer)
            PlayerStatsPanel.IncrementTrophies();
    }

    private void SetupReturnButton()
    {
        if (_returnButton == null) return;

        bool isHost = !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
        _returnButton.interactable = isHost;

        if (_returnButtonLabel != null)
            _returnButtonLabel.text = isHost ? "VOLVER AL LOBBY" : "Esperando al host...";

        _returnButton.onClick.AddListener(OnReturnClicked);

        if (_leaveButton == null) return;

        _leaveButton.interactable = PhotonNetwork.InRoom;

        if (_leaveButtonLabel != null)
            _leaveButtonLabel.text = "LEAVE";

        _leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    private void PopulatePodium()
    {
        if (ChampionshipManager.Instance == null) return;

        var standings = ChampionshipManager.Instance.GetStandings();

        FillSlot(0, standings, _firstPlaceName,  _firstPlacePoints);
        FillSlot(1, standings, _secondPlaceName, _secondPlacePoints);
        FillSlot(2, standings, _thirdPlaceName,  _thirdPlacePoints);
    }

    private static void FillSlot(int idx,
                                   List<(Player player, int points)> standings,
                                   TMP_Text nameText,
                                   TMP_Text pointsText)
    {
        if (idx >= standings.Count)
        {
            if (nameText   != null) nameText.text   = "-";
            if (pointsText != null) pointsText.text = "";
            return;
        }

        if (nameText   != null) nameText.text   = standings[idx].player.NickName;
        if (pointsText != null) pointsText.text = $"{standings[idx].points} pts";
    }

    private void PopulateStandings()
    {
        if (_standingsContainer == null || _rowPrefab == null) return;

        foreach (Transform t in _standingsContainer) Destroy(t.gameObject);

        if (ChampionshipManager.Instance == null) return;

        var standings = ChampionshipManager.Instance.GetStandings();
        for (int i = 0; i < standings.Count; i++)
        {
            var go = Instantiate(_rowPrefab, _standingsContainer);
            if (go.TryGetComponent(out LeaderboardRow row))
                row.SetChampionship(i + 1, standings[i].player.NickName, standings[i].points);
        }
    }

    private void OnReturnClicked()
    {
        if (!PhotonNetwork.InRoom)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
            return;
        }
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel("Lobby");
    }

    private void OnLeaveClicked()
    {
        if (!PhotonNetwork.InRoom)
        {
            SceneManager.LoadScene("Lobby");
            return;
        }

        if (_leaveButton != null)
            _leaveButton.interactable = false;

        if (_leaveButtonLabel != null)
            _leaveButtonLabel.text = "SALIENDO...";

        if (MatchmakingManager.Instance != null)
            MatchmakingManager.Instance.RequestLeaveRoom();
        else
            PhotonNetwork.LeaveRoom(false);
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("Lobby");
    }
}
