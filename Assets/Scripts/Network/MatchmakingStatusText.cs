using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class MatchmakingStatusText : MonoBehaviourPunCallbacks
{
    [Header("Matchmaking Status Texts")]
    [SerializeField] private string _inLobbyText = "Status: In Lobby - Join or Create a Room";
    [SerializeField] private string _joiningRoomText = "Status: Joining Room...";
    [SerializeField] private string _joiningRandomRoomText = "Status: Searching for a Room...";
    [SerializeField] private string _creatingRoomText = "Status: Creating Room...";
    [SerializeField] private string _inRoomText = "Status: In Room";
    [SerializeField] private string _leavingRoomText = "Status: Leaving Room...";

    [Header("Failure")]
    [Tooltip("{0} is replaced with the failure message from Photon.")]
    [SerializeField] private string _failedText = "Status: Failed - {0}";

    private TextMeshProUGUI _matchmakingStatusText;

    private void Awake()
    {
        _matchmakingStatusText = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        TrySubscribeToEvents();
        UpdateStatusText(MatchmakingManager.Instance.MatchmakingStatus);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        TrySubscribeToEvents();
    }

    public override void OnDisable()
    {
        base.OnDisable();

        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.OnStatusChanged -= UpdateStatusText;
            MatchmakingManager.Instance.OnFailed -= ShowFailure;
        }
    }

    private void TrySubscribeToEvents()
    {
        // Guard against the manager itself, not NetworkManager.
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.OnStatusChanged -= UpdateStatusText;
            MatchmakingManager.Instance.OnStatusChanged += UpdateStatusText;

            MatchmakingManager.Instance.OnFailed -= ShowFailure;
            MatchmakingManager.Instance.OnFailed += ShowFailure;
        }
    }

    private void UpdateStatusText(MatchmakingStatus newStatus)
    {
        switch (newStatus)
        {
            case MatchmakingStatus.InLobby:
                _matchmakingStatusText.text = _inLobbyText;
                break;
            case MatchmakingStatus.JoiningRoom:
                _matchmakingStatusText.text = _joiningRoomText;
                break;
            case MatchmakingStatus.JoiningRandomRoom:
                _matchmakingStatusText.text = _joiningRandomRoomText;
                break;
            case MatchmakingStatus.CreatingRoom:
                _matchmakingStatusText.text = _creatingRoomText;
                break;
            case MatchmakingStatus.InRoom:
                _matchmakingStatusText.text = _inRoomText;
                break;
            case MatchmakingStatus.LeavingRoom:
                _matchmakingStatusText.text = _leavingRoomText;
                break;
            default:
                _matchmakingStatusText.text = _inLobbyText; // fallback, never leave stale text
                break;
        }
    }

    private void ShowFailure(string message)
    {
        _matchmakingStatusText.text = string.Format(_failedText, message);
    }
}