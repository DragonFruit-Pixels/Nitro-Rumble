using TMPro;
using UnityEngine;

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

    // ── Leaderboard online (ScoreEntry) ──────────────────────────────────────

    public void Set(int rank, ScoreEntry entry)
    {
        if (_rank != null) _rank.text = $"{rank}";
        if (_name != null) _name.text = entry.Name;
        if (_time != null) _time.text = FormatTime(entry.Time);
    }

    // ── Resultado de carrera individual ──────────────────────────────────────

    public void SetRaceResult(int rank, string playerName, double time, int pointsEarned)
    {
        if (_rank != null) _rank.text = $"{rank}";
        if (_name != null) _name.text = playerName;
        if (_time != null)
            _time.text = time > 0.0
                ? $"{FormatTime((float)time)} (+{pointsEarned})"
                : $"DNF (+{pointsEarned})";
    }

    // ── Standings del campeonato ──────────────────────────────────────────────

    public void SetChampionship(int rank, string playerName, int points)
    {
        if (_rank != null) _rank.text = $"{rank}";
        if (_name != null) _name.text = playerName;
        if (_time != null) _time.text = $"{points} pts";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatTime(float seconds)
    {
        int   minutes = (int)(seconds / 60f);
        float rest    = seconds - minutes * 60f;
        return $"{minutes:00}:{rest:00.000}";
    }
}
