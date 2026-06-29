using System;
using System.Collections.Generic;

/// <summary>
/// Datos del jugador que se persisten localmente con encriptación XOR.
/// Toda la información es por-jugador (no compartida en red).
/// </summary>
[Serializable]
public class PlayerProfile
{
    public string nickname = "";
    
    // Skin de auto seleccionada
    public int selectedSkin = 0;

    // Estadísticas globales
    public int totalRaces    = 0;
    public int totalWins     = 0;  // 1° lugar
    public int totalSilvers  = 0;  // 2° lugar
    public int totalBronzes  = 0;  // 3° lugar
    public int totalTrophies = 0;  // campeonatos ganados

    public int TotalPodiums => totalWins + totalSilvers + totalBronzes;
    public float WinRate    => totalRaces > 0 ? (float)totalWins / totalRaces : 0f;

    // Récords por track (clave = nombre de escena, ej. "Track01")
    public Dictionary<string, TrackRecord> trackRecords = new Dictionary<string, TrackRecord>();

    public TrackRecord GetOrCreateRecord(string trackId)
    {
        if (!trackRecords.ContainsKey(trackId))
            trackRecords[trackId] = new TrackRecord();
        return trackRecords[trackId];
    }
}

[Serializable]
public class TrackRecord
{
    public double bestRaceTime   = double.MaxValue; // segundos (PhotonNetwork.Time)
    public float  bestLapTime    = float.MaxValue;  // segundos (TODO: trackear por vuelta)
    public int    bestPosition   = int.MaxValue;    // menor es mejor
    public int    racesCompleted = 0;

    /// <summary>Devuelve true si este tiempo mejora el récord actual.</summary>
    public bool IsBetterTime(double raceTime) => raceTime < bestRaceTime;

    /// <summary>Devuelve true si esta posición mejora el récord actual.</summary>
    public bool IsBetterPosition(int position) => position < bestPosition;
}
