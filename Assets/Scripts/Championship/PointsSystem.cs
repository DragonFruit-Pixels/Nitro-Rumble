/// <summary>
/// Tabla de puntos estilo Mario Kart. Posiciones más allá de la tabla reciben 0.
/// </summary>
public static class PointsSystem
{
    // 1° = 10, 2° = 6, 3° = 3, 4° = 1
    private static readonly int[] Table = { 10, 6, 3, 1 };

    public static int GetPoints(int position)
    {
        int idx = position - 1;
        return (idx >= 0 && idx < Table.Length) ? Table[idx] : 0;
    }
}
