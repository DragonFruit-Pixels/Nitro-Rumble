using Photon.Pun;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class MatchmakingStatusText : MonoBehaviourPunCallbacks
{
    private TextMeshProUGUI _matchmakingStatusText;

    private MatchmakingStatus _lastStatus = MatchmakingStatus.InLobby;
    private string _lastFailureMessage;
    private bool _showingFailure;

    private void Awake()
    {
        _matchmakingStatusText = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        TrySubscribeToEvents();
        UpdateStatusText(MatchmakingManager.Instance != null
            ? MatchmakingManager.Instance.MatchmakingStatus
            : MatchmakingStatus.InLobby);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        TrySubscribeToEvents();
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    public override void OnDisable()
    {
        base.OnDisable();

        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;

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

    private void OnLanguageChanged(Language _)
    {
        if (_showingFailure)
            ShowFailure(_lastFailureMessage);
        else
            UpdateStatusText(_lastStatus);
    }

    private void UpdateStatusText(MatchmakingStatus newStatus)
    {
        _lastStatus = newStatus;
        _showingFailure = false;

        string key;
        switch (newStatus)
        {
            case MatchmakingStatus.JoiningRoom:       key = "matchmaking.joiningRoom"; break;
            case MatchmakingStatus.JoiningRandomRoom: key = "matchmaking.joiningRandomRoom"; break;
            case MatchmakingStatus.CreatingRoom:       key = "matchmaking.creatingRoom"; break;
            case MatchmakingStatus.InRoom:             key = "matchmaking.inRoom"; break;
            case MatchmakingStatus.LeavingRoom:        key = "matchmaking.leavingRoom"; break;
            default:                                   key = "matchmaking.inLobby"; break; // fallback, never leave stale text
        }

        _matchmakingStatusText.text = LocalizationManager.Get(key);
    }

    private void ShowFailure(string message)
    {
        _lastFailureMessage = message;
        _showingFailure = true;
        _matchmakingStatusText.text = string.Format(LocalizationManager.Get("matchmaking.failed"), message);
    }
}
