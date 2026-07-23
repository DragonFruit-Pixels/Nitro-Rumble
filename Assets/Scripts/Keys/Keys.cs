public static class Keys
{
    public const string MAP_PROP_KEY     = "map";      // Legacy - mantenido por compatibilidad
    public const string LAPS_KEY         = "laps";
    public const string RACE_COUNT_KEY   = "rc";
    public const string MAP_POOL_KEY     = "pool";     // int[] IDs de tracks seleccionados
    public const string MAP_QUEUE_KEY    = "queue";    // string[] scene names en orden de juego
    public const string CURRENT_RACE_KEY = "curRace";  // int (0-based)
    public const string POINTS_KEY       = "pts";      // int[] indexed por actorNumber - 1
    public const string GRID_ACTORS_KEY  = "gridActors"; // int[] actorNumbers en orden oficial de grilla
    public const string BOTS_COUNT_KEY   = "bots";      // int, cantidad de bots elegida por el host
    public const string NEXT_SCENE_KEY   = "nextScene"; // string scene name que Loading debe cargar
}
