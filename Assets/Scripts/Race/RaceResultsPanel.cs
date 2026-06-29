using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay que aparece al final de cada carrera mostrando:
///   - Resultados de esta carrera (posición, nombre, tiempo, puntos ganados)
///   - Standings del campeonato acumulado
///   - Countdown para la siguiente carrera (solo el host puede saltarlo)
///
/// Colocar en la escena de carrera. Se activa automaticamente vía OnRaceFinished.
/// </summary>
public class RaceResultsPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private CanvasGroup _panel;

    [Header("Header")]
    [SerializeField] private TMP_Text _headerLabel;
    [SerializeField] private TMP_Text _countdownLabel;

    [Header("Resultados de esta carrera")]
    [SerializeField] private Transform  _raceResultsContainer;
    [SerializeField] private GameObject _raceRowPrefab;

    [Header("Standings del campeonato")]
    [SerializeField] private GameObject _standingsSection;
    [SerializeField] private Transform  _standingsContainer;
    [SerializeField] private GameObject _standingsRowPrefab;

    [Header("Botón Skip")]
    [SerializeField] private Button   _skipButton;
    [SerializeField] private TMP_Text _skipLabel;

    [Header("Dependencias de escena")]
    [SerializeField] private GameObject _leaderboardPanel;

    private const float ShowDelay         = 2f;
    private const float CountdownDuration = 10f;

    private bool      _subscribed;
    private bool      _recorded;
    private int[]     _earnedThisRace;
    private Coroutine _countdownRoutine;

    private void Update()
    {
        if (_subscribed || RaceManager.Instance == null) return;
        RaceManager.Instance.OnRaceFinished += OnRaceFinished;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (_subscribed && RaceManager.Instance != null)
            RaceManager.Instance.OnRaceFinished -= OnRaceFinished;
        _subscribed = false;
    }

    private void Start()
    {
        HidePanel();
        if (_skipButton != null)
            _skipButton.onClick.AddListener(OnSkipClicked);
    }

    private void OnRaceFinished(Racer winner)
    {
        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(ShowDelay);

        var  cm              = ChampionshipManager.Instance;
        bool hasChampionship = cm != null && cm.IsActive;

        // Registrar puntos (determinístico en todos los clientes; solo MC escribe Room Props)
        if (!_recorded && hasChampionship)
        {
            _earnedThisRace = cm.RecordRaceResults(RaceManager.Instance.GetRacers());
            _recorded = true;
        }

        PopulateRaceResults();

        if (hasChampionship)
        {
            if (_standingsSection != null) _standingsSection.SetActive(true);
            PopulateStandings();

            int  displayRace = cm.CurrentRace;   // 0-based (aún no avanzado)
            int  total       = cm.TotalRaces;
            bool isLast      = (displayRace + 1) >= total;

            if (_headerLabel != null) _headerLabel.text = $"CARRERA {displayRace + 1} / {total} FINALIZADA";
            if (_skipLabel   != null) _skipLabel.text   = isLast ? "IR AL PODIO" : "SIGUIENTE CARRERA";
        }
        else
        {
            if (_standingsSection != null) _standingsSection.SetActive(false);
            if (_headerLabel      != null) _headerLabel.text = "CARRERA FINALIZADA";
        }

        // Botón skip: solo host en multiplayer, siempre visible en offline
        bool showSkip = !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
        if (_skipButton != null)
            _skipButton.gameObject.SetActive(showSkip);

        ShowPanel();

        _countdownRoutine = StartCoroutine(Countdown());
    }

    private IEnumerator Countdown()
    {
        float remaining = CountdownDuration;
        while (remaining > 0f)
        {
            if (_countdownLabel != null)
                _countdownLabel.text = $"Continuando en {Mathf.CeilToInt(remaining)}s...";
            remaining -= Time.deltaTime;
            yield return null;
        }

        // En multiplayer solo el MC avanza; en offline siempre avanza
        if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            Advance();
    }

    private void OnSkipClicked() => Advance();

    private void Advance()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }

        // Solo el MC controla la navegación en multiplayer
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        var cm = ChampionshipManager.Instance;
        if (cm != null && cm.IsActive)
        {
            // AdvanceChampionship() usa RaiseEvent → todos los clientes cargan la escena
            cm.AdvanceChampionship();
        }
        else
        {
            // Carrera única: volver a la sala (Lobby).
            // Track_1 → Lobby es escena diferente, LoadLevel funciona para todos.
            if (PhotonNetwork.InRoom)
                PhotonNetwork.LoadLevel("Lobby");
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
        }
    }

    // ── Populate ───────────────────────────────────────────────────────────────

    private void PopulateRaceResults()
    {
        if (_raceResultsContainer == null || _raceRowPrefab == null) return;
        foreach (Transform t in _raceResultsContainer) Destroy(t.gameObject);
        if (RaceManager.Instance == null) return;

        var racers = RaceManager.Instance.GetRacers();
        for (int i = 0; i < racers.Count; i++)
        {
            var go = Instantiate(_raceRowPrefab, _raceResultsContainer);
            if (!go.TryGetComponent(out LeaderboardRow row)) continue;

            int actorNum = racers[i].photonView?.Owner?.ActorNumber ?? 0;
            int pts      = (_earnedThisRace != null && actorNum >= 1 && actorNum <= _earnedThisRace.Length)
                ? _earnedThisRace[actorNum - 1]
                : PointsSystem.GetPoints(racers[i].Position);

            row.SetRaceResult(racers[i].Position, racers[i].PlayerName, racers[i].FinishTime, pts);
        }
    }

    private void PopulateStandings()
    {
        if (_standingsContainer == null || _standingsRowPrefab == null) return;
        foreach (Transform t in _standingsContainer) Destroy(t.gameObject);
        if (ChampionshipManager.Instance == null) return;

        var standings = ChampionshipManager.Instance.GetStandings();
        for (int i = 0; i < standings.Count; i++)
        {
            var go = Instantiate(_standingsRowPrefab, _standingsContainer);
            if (go.TryGetComponent(out LeaderboardRow row))
                row.SetChampionship(i + 1, standings[i].player.NickName, standings[i].points);
        }
    }

    // ── Panel visibility ────────────────────────────────────────────────────────

    private void ShowPanel()
    {
        if (_panel == null) return;
        _panel.alpha          = 1f;
        _panel.interactable   = true;
        _panel.blocksRaycasts = true;
        if (_leaderboardPanel != null) _leaderboardPanel.SetActive(false);
    }

    private void HidePanel()
    {
        if (_panel == null) return;
        _panel.alpha          = 0f;
        _panel.interactable   = false;
        _panel.blocksRaycasts = false;
        // No restauramos _leaderboardPanel aquí: HidePanel se llama en Start() y el
        // LeaderboardPanel arranca inactivo (su propio script controla cuándo mostrarse).
        // Solo lo escondemos en ShowPanel() cuando los resultados tapan la pantalla.
    }
}
