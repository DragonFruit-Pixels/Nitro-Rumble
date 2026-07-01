using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una fila reutilizable para:
///   - Leaderboard online (tiempos globales)
///   - Resultados de carrera (tiempo + puntos ganados)
///   - Standings del campeonato (puntos acumulados)
/// </summary>
public class LeaderboardRow : MonoBehaviour
{
    [SerializeField] private TMP_Text _rank;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _time;

    [Header("Medalla (top 3)")]
    [SerializeField] private Image _medalIcon;
    [Tooltip("Índice 0 = 1° (oro), 1 = 2° (plata), 2 = 3° (bronce).")]
    [SerializeField] private Sprite[] _medalSprites;

    // ── Leaderboard online (ScoreEntry) ──────────────────────────────────────

    public void Set(int rank, ScoreEntry entry)
    {
        SetRankVisual(rank);
        if (_name != null) _name.text = entry.Name;
        if (_time != null) _time.text = FormatTime(entry.Time);
    }

    // ── Resultado de carrera individual ──────────────────────────────────────

    public void SetRaceResult(int rank, string playerName, double time, int pointsEarned)
    {
        SetRankVisual(rank);
        if (_name != null) _name.text = playerName;
        if (_time != null)
            _time.text = time > 0.0
                ? $"{FormatTime((float)time)} (+{pointsEarned})"
                : $"DNF (+{pointsEarned})";
    }

    // ── Standings del campeonato ──────────────────────────────────────────────

    public void SetChampionship(int rank, string playerName, int points)
    {
        SetRankVisual(rank);
        if (_name != null) _name.text = playerName;
        if (_time != null) _time.text = $"{points} pts";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetRankVisual(int rank)
    {
        if (_rank != null) _rank.text = $"{rank}";

        if (_medalIcon == null) return;

        int idx = rank - 1;
        bool hasMedal = idx >= 0 && _medalSprites != null && idx < _medalSprites.Length;
        _medalIcon.gameObject.SetActive(hasMedal);
        if (hasMedal) _medalIcon.sprite = _medalSprites[idx];
    }

    private static string FormatTime(float seconds)
    {
        int   minutes = (int)(seconds / 60f);
        float rest    = seconds - minutes * 60f;
        return $"{minutes:00}:{rest:00.000}";
    }
}
