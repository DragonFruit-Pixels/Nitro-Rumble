using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Panel del lobby que muestra:
///   - Botones para seleccionar un track (reutiliza TrackChooseButton).
///   - Top-N del leaderboard global para ese track (reutiliza LeaderboardRow prefab).
///   - Récord personal del jugador local para ese track (LocalSaveManager).
///
/// Wiring en el Editor:
///   _trackCatalogue       → el mismo ScriptableObject de tracks que usa el create room.
///   _trackButtonContainer → Transform padre donde se instancian los botones de track.
///   _trackButtonPrefab    → prefab TrackChooseButton.
///   _rowsContainer        → Transform con VerticalLayoutGroup para las filas del leaderboard.
///   _rowPrefab            → el mismo prefab LeaderboardRow que usa LeaderboardPanel.
///   _topN                 → cuántas filas mostrar (default 5).
///   _personalBestTime     → TMP_Text para el mejor tiempo personal.
///   _personalBestPosition → TMP_Text para la mejor posición personal.
///   _noDataLabel          → TMP_Text que se muestra cuando no hay datos (opcional).
/// </summary>
public class LobbyLeaderboardPanel : MonoBehaviour, ITrackChooseListener
{
    [Header("Track Selection")]
    [SerializeField] private TrackCatalogueSO _trackCatalogue;
    [SerializeField] private Transform        _trackButtonContainer;
    [SerializeField] private TrackChooseButton _trackButtonPrefab;

    [Header("Global Leaderboard")]
    [SerializeField] private Transform  _rowsContainer;
    [SerializeField] private GameObject _rowPrefab;
    [SerializeField] private int        _topN = 5;
    [SerializeField] private TMP_Text   _noDataLabel;

    [Header("Personal Record")]
    [SerializeField] private TMP_Text _personalBestTime;
    [SerializeField] private TMP_Text _personalBestPosition;
    [SerializeField] private TMP_Text _selectedTrackName;

    private readonly List<TrackChooseButton> _trackButtons = new();
    private TrackSO _selectedTrack;

    private void Start()
    {
        SpawnTrackButtons();
    }

    private void SpawnTrackButtons()
    {
        if (_trackCatalogue == null || _trackButtonPrefab == null || _trackButtonContainer == null) return;

        foreach (TrackSO track in _trackCatalogue.Tracks)
        {
            TrackChooseButton btn = Instantiate(_trackButtonPrefab, _trackButtonContainer);
            btn.Init(this, track);
            _trackButtons.Add(btn);
        }

        // Seleccionar el primer track por defecto.
        if (_trackButtons.Count > 0)
            _trackButtons[0].OnTrackChooseExternal();
    }

    // ── ITrackChooseListener ────────────────────────────────────────────────

    public void OnTrackChoose(TrackSO track)
    {
        _selectedTrack = track;

        if (_selectedTrackName != null)
            _selectedTrackName.text = track.TrackName;

        RefreshLeaderboard();
        RefreshPersonalRecord();
    }

    public void UnselectOthers(TrackChooseButton selectedButton)
    {
        foreach (var btn in _trackButtons)
        {
            if (btn != selectedButton)
                btn.Select(false);
        }
    }

    // ── Leaderboard global ─────────────────────────────────────────────────

    private void RefreshLeaderboard()
    {
        ClearRows();

        if (LeaderboardService.Instance == null || !LeaderboardService.Instance.IsReady)
        {
            ShowNoData("Leaderboard not available");
            return;
        }

        LeaderboardService.Instance.GetTopScoresByTrack(_selectedTrack.TrackSceneName, _topN, PopulateRows);
    }

    private void PopulateRows(List<ScoreEntry> scores)
    {
        ClearRows();

        if (scores == null || scores.Count == 0)
        {
            ShowNoData("No registries yet");
            return;
        }

        HideNoData();

        for (int i = 0; i < scores.Count; i++)
        {
            GameObject go = Instantiate(_rowPrefab, _rowsContainer);
            if (go.TryGetComponent(out LeaderboardRow row))
                row.Set(i + 1, scores[i]);
        }
    }

    private void ClearRows()
    {
        if (_rowsContainer == null) return;
        for (int i = _rowsContainer.childCount - 1; i >= 0; i--)
            Destroy(_rowsContainer.GetChild(i).gameObject);
    }

    private void ShowNoData(string message)
    {
        if (_noDataLabel == null) return;
        _noDataLabel.text = message;
        _noDataLabel.gameObject.SetActive(true);
    }

    private void HideNoData()
    {
        if (_noDataLabel != null)
            _noDataLabel.gameObject.SetActive(false);
    }

    // ── Récord personal ────────────────────────────────────────────────────

    private void RefreshPersonalRecord()
    {
        if (LocalSaveManager.Instance == null) return;

        string sceneId = _selectedTrack.TrackSceneName;
        double bestTime = LocalSaveManager.Instance.GetBestTime(sceneId);
        int bestPos  = LocalSaveManager.Instance.GetBestPosition(sceneId);

        if (_personalBestTime != null)
            _personalBestTime.text = bestTime > 0 ? FormatTime(bestTime) : "--:--.---";

        if (_personalBestPosition != null)
            _personalBestPosition.text = bestPos > 0 ? $"P{bestPos}" : "--";
    }

    private static string FormatTime(double seconds)
    {
        int mins   = (int)(seconds / 60.0);
        double rest = seconds - mins * 60.0;
        return $"{mins:00}:{rest:00.000}";
    }
}
