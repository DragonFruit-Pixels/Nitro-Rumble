/// <summary>
/// Nombres de las claves (settings) configuradas en el dashboard de Unity Remote Config.
/// Mantener sincronizado con lo creado en Unity Dashboard > Remote Config.
///
/// Las tres son de tipo JSON y contienen un ARRAY con el contenido DESHABILITADO:
///   disabledTrackIds   → array de int    ej: [2, 3]
///   disabledSkinIds    → array de int    ej: [1]
///   disabledPowerUps   → array de string ej: ["EMP", "Turbo"]  (nombres del enum PowerUpType)
///
/// Semántica fail-open: lo que NO aparece en la lista está disponible.
/// </summary>
public static class RemoteConfigKeys
{
    public const string DISABLED_TRACK_IDS = "disabledTrackIds";
    public const string DISABLED_SKIN_IDS  = "disabledSkinIds";
    public const string DISABLED_POWERUPS  = "disabledPowerUps";
}
