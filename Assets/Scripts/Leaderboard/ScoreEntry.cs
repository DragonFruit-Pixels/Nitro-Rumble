/// <summary>
/// Una entrada de la tabla de puntuaciones (struct que consume la UI).
/// Se mapea desde <see cref="ScoreDto"/> al leer la API REST.
/// </summary>
public readonly struct ScoreEntry
{
    public readonly string Name;
    public readonly float  Time;      // tiempo de carrera en segundos (menor = mejor)
    public readonly int    Position;  // posición final en la carrera (dato secundario)

    public ScoreEntry(string name, double time, int position)
    {
        Name     = name;
        Time     = (float)time;
        Position = position;
    }
}
