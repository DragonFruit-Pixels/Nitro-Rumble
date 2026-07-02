using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel del lobby que muestra:
///   - Carrusel (foto grande + flechas prev/next) para elegir un track.
///   - Top-N del leaderboard global para ese track (reutiliza LeaderboardRow prefab).
///   - Récord personal del jugador local para ese track (LocalSaveManager).
///
/// Wiring en el Editor:
///   _trackCatalogue       → el mismo ScriptableObject de tracks que usa el create room.
///   _trackPhoto           → Image grande del carrusel.
///   _prevTrackButton / _nextTrackButton → flechas del carrusel.
///   _rowsContainer        → Transform con VerticalLayoutGroup para las filas del leaderboard.
///   _rowPrefab            → el mismo prefab LeaderboardRow que usa LeaderboardPanel.
///   _topN                 → cuántas filas mostrar (default 5).
///   _personalBestTime     → TMP_Text para el mejor tiempo personal.
///   _personalBestPosition → TMP_Text para la mejor posición personal.
///   _noDataLabel          → TMP_Text que se muestra cuando no hay datos (opcional).
/// </summary>
public class LobbyLeaderboardPanel : MonoBehaviour
{
    [Header("Track Carousel")]
    [SerializeField] private TrackCatalogueSO _trackCatalogue;
    [SerializeField] private Image  _trackPhoto;
    [SerializeField] private Button _prevTrackButton;
    [SerializeField] private Button _nextTrackButton;

    [Header("Global Leaderboard")]
    [SerializeField] private Transform  _rowsContainer;
    [SerializeField] private GameObject _rowPrefab;
    [SerializeField] private int        _topN = 5;
    [SerializeField] private TMP_Text   _noDataLabel;

    [Header("Personal Record")]
    [SerializeField] private TMP_Text _personalBestTime;
    [SerializeField] private TMP_Text _personalBestPosition;
    [SerializeField] private TMP_Text _selectedTrackName;

    private TrackSO _selectedTrack;
    private int _trackIndex;

    private void Start()
    {
        if (_prevTrackButton != null) _prevTrackButton.onClick.AddListener(() => ShowTrack(_trackIndex - 1));
        if (_nextTrackButton != null) _nextTrackButton.onClick.AddListener(() => ShowTrack(_trackIndex + 1));

        ShowTrack(0);
    }

    // ── Carrusel ─────────────────────────────────────────────────────────────

    private void ShowTrack(int index)
    {
        if (_trackCatalogue == null || _trackCatalogue.Tracks == null || _trackCatalogue.Tracks.Count == 0) return;

        int count = _trackCatalogue.Tracks.Count;
        _trackIndex = ((index % count) + count) % count;
        _selectedTrack = _trackCatalogue.Tracks[_trackIndex];

        if (_trackPhoto != null) _trackPhoto.sprite = _selectedTrack.TrackImage;
        if (_selectedTrackName != null) _selectedTrackName.text = _selectedTrack.TrackName;

        RefreshLeaderboard();
        RefreshPersonalRecord();
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
