using Photon.Pun;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class NetworkStatusText : MonoBehaviourPunCallbacks
{
    private TextMeshProUGUI _networkStatusText;
    private NetworkStatus _lastStatus = NetworkStatus.Offline;

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
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        if (NetworkManager.Instance != null)
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

    private void OnLanguageChanged(Language _) => UpdateStatusText(_lastStatus);

    private void UpdateStatusText(NetworkStatus newStatus)
    {
        _lastStatus = newStatus;

        string key;
        switch (newStatus)
        {
            case NetworkStatus.Connecting: key = "menu.statusConnecting"; break;
            case NetworkStatus.Connected:  key = "menu.statusConnected"; break;
            default:                       key = "menu.statusPlayToConnect"; break;
        }

        _networkStatusText.text = LocalizationManager.Get(key);
    }
}
