using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;

/// <summary>
/// Runtime debug overlay for Photon's built-in network lag simulation.
/// Toggle simulation and tweak latency / jitter / packet-loss live while the
/// game runs. Press the toggle key (default F9) to show/hide the window.
///
/// Drop on any GameObject. Works with PUN2 / Photon Realtime (anything exposing
/// LoadBalancingPeer). Fusion uses a different system.
/// </summary>
public class PhotonLagSimulatorGUI : Singleton<PhotonLagSimulatorGUI>
{
    [Header("Window")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F9;
    [SerializeField] private bool startVisible = true;

    [Header("Slider Ranges")]
    [SerializeField] private int maxLagMs = 500;
    [SerializeField] private int maxJitterMs = 200;

    private bool _visible;
    private Rect _windowRect = new Rect(20, 20, 340, 0);

    private PhotonPeer Peer =>
        PhotonNetwork.NetworkingClient != null
            ? PhotonNetwork.NetworkingClient.LoadBalancingPeer
            : null;

    private void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(this.gameObject);
        
        _visible = startVisible;
    } 

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!_visible) return;
        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "Photon Lag Simulator");
    }

    private void DrawWindow(int id)
    {
        PhotonPeer peer = Peer;

        if (peer == null)
        {
            GUILayout.Label("Photon client not initialized.");
            GUI.DragWindow();
            return;
        }

        var sim = peer.NetworkSimulationSettings;

        // Master toggle
        bool enabled = GUILayout.Toggle(peer.IsSimulationEnabled, "  Simulation Enabled");
        if (enabled != peer.IsSimulationEnabled)
            peer.IsSimulationEnabled = enabled;

        GUILayout.Space(6);
        GUILayout.Label($"State: {PhotonNetwork.NetworkClientState}    Real Ping: {PhotonNetwork.GetPing()} ms");
        GUILayout.Space(6);

        GUI.enabled = enabled; // grey out sliders when sim is off

        GUILayout.Label("— Outgoing —");
        sim.OutgoingLag            = IntSlider("Lag",  sim.OutgoingLag, 0, maxLagMs, "ms");
        sim.OutgoingJitter         = IntSlider("Jitter", sim.OutgoingJitter, 0, maxJitterMs, "ms");
        sim.OutgoingLossPercentage = IntSlider("Loss", sim.OutgoingLossPercentage, 0, 100, "%");

        GUILayout.Space(4);
        GUILayout.Label("— Incoming —");
        sim.IncomingLag            = IntSlider("Lag",  sim.IncomingLag, 0, maxLagMs, "ms");
        sim.IncomingJitter         = IntSlider("Jitter", sim.IncomingJitter, 0, maxJitterMs, "ms");
        sim.IncomingLossPercentage = IntSlider("Loss", sim.IncomingLossPercentage, 0, 100, "%");

        GUI.enabled = true;

        GUILayout.Space(8);
        GUILayout.Label("Presets");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Perfect")) ApplyPreset(peer, 0, 0, 0);
        if (GUILayout.Button("Good"))    ApplyPreset(peer, 40, 10, 0);
        if (GUILayout.Button("3G"))      ApplyPreset(peer, 120, 40, 1);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Bad WiFi")) ApplyPreset(peer, 200, 80, 3);
        if (GUILayout.Button("Awful"))    ApplyPreset(peer, 400, 150, 8);
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }

    private void ApplyPreset(PhotonPeer peer, int lag, int jitter, int loss)
    {
        peer.IsSimulationEnabled = true;
        var sim = peer.NetworkSimulationSettings;
        sim.OutgoingLag = sim.IncomingLag = lag;
        sim.OutgoingJitter = sim.IncomingJitter = jitter;
        sim.OutgoingLossPercentage = sim.IncomingLossPercentage = loss;
    }

    private int IntSlider(string label, int value, int min, int max, string suffix)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {value}{suffix}", GUILayout.Width(150));
        int result = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
        GUILayout.EndHorizontal();
        return result;
    }
}