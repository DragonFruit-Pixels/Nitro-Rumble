using Newtonsoft.Json;

/// <summary>
/// DTO de transporte para una entrada del leaderboard que viaja como JSON
/// hacia/desde la API REST.
///
/// Los atributos [JsonProperty] emparejan los campos C# con los nombres exactos
/// que usa el backend. Si tu API usa otras keys, cambialas acá (no en el resto del código).
///
/// Se mapea a <see cref="ScoreEntry"/> (struct que consume la UI) al leer.
/// </summary>
public class ScoreDto
{
    [JsonProperty("name")]      public string Name;
    [JsonProperty("time")]      public double Time;       // tiempo de carrera en segundos (menor = mejor)
    [JsonProperty("position")]  public int    Position;   // posición final en la carrera (dato secundario)
    [JsonProperty("track")]     public string Track;       // nombre de la escena/pista
    [JsonProperty("timestamp")] public string Timestamp;   // DateTime.UtcNow en ISO-8601 ("o")

    public ScoreDto() { }

    public ScoreDto(string name, double time, int position, string track, string timestamp)
    {
        Name      = name;
        Time      = time;
        Position  = position;
        Track     = track;
        Timestamp = timestamp;
    }

    public ScoreEntry ToEntry() => new ScoreEntry(Name ?? "???", Time, Position);
}
