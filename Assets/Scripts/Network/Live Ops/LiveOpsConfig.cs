using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;

/// <summary>
/// Servicio central de LiveOps basado en Unity Remote Config.
///
/// Controla la disponibilidad de contenido (mapas, skins de auto y power-ups)
/// desde el dashboard de Unity, sin publicar una nueva build.
///
/// NOTA DE NOMBRE: se llama <c>LiveOpsConfig</c> y NO "RemoteConfigService" para no
/// chocar con el tipo del SDK (<c>Unity.Services.RemoteConfig.RemoteConfigService</c>),
/// que se usa internamente acá.
///
/// ── MODELO DE DATOS (3 claves JSON en Remote Config) ───────────────────────────
///   disabledTrackIds   → array de int    ej: [2, 3]
///   disabledSkinIds    → array de int    ej: [1]
///   disabledPowerUps   → array de string ej: ["EMP", "Turbo"]  (nombres del enum PowerUpType)
///
/// ── SEMÁNTICA "fail-open" ───────────────────────────────────────────────────────
/// Lo que NO está en la lista de deshabilitados está disponible.
///   - Clave ausente, vacía o mal formada → no se deshabilita nada → todo disponible.
///   - Sin conexión / fetch fallido        → todo disponible (el juego siempre es jugable).
/// Esto permite lanzar contenido nuevo encendido por defecto y apagarlo puntualmente
/// (ej. "coming soon") agregando su ID a la lista hasta el día del lanzamiento.
public class LiveOpsConfig : Singleton<LiveOpsConfig>
{
    [Header("LiveOps")]
    [Tooltip("Si está activo, vuelve a pedir la config al volver del background (Remote Config lo considera una nueva sesión).")]
    [SerializeField] private bool _refetchOnAppResume = true;

    [Tooltip("Intervalo en segundos para re-pedir la config durante el runtime y detectar cambios del dashboard. 0 = desactivado.")]
    [SerializeField] private float _pollingIntervalSeconds = 60f;

    [Tooltip("Logs detallados del contenido habilitado/deshabilitado al aplicar la config.")]
    [SerializeField] private bool _verboseLogging = true;

    // Atributos opcionales para Game Overrides (segmentación). Vacíos = sin segmentación.
    private struct UserAttributes { }
    private struct AppAttributes { }

    // Listas de contenido DESHABILITADO. Vacías = todo disponible (fail-open).
    private readonly HashSet<int>         _disabledTracks   = new HashSet<int>();
    private readonly HashSet<int>         _disabledSkins    = new HashSet<int>();
    private readonly HashSet<PowerUpType> _disabledPowerUps = new HashSet<PowerUpType>();

    /// <summary>True una vez que se aplicó al menos una respuesta (remota, cacheada o default).</summary>
    public bool HasFetched { get; private set; }

    /// <summary>Origen de la última config aplicada (Remote / Cached / Default).</summary>
    public ConfigOrigin LastOrigin { get; private set; } = ConfigOrigin.Default;

    /// <summary>Se dispara cada vez que se aplica una config nueva. La UI escucha esto para refrescar.</summary>
    public event Action OnConfigApplied;

    public override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicado destruido por el Singleton

        transform.SetParent(null);    // DontDestroyOnLoad solo funciona en GameObjects raíz
        DontDestroyOnLoad(gameObject);

        _ = InitializeAsync();
    }

    private void OnApplicationPause(bool paused)
    {
        // Al volver del background, Remote Config considera una "nueva sesión".
        if (!paused && _refetchOnAppResume && HasFetched)
            _ = RefreshAsync();
    }

    // ── Init + Fetch ────────────────────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            // Remote Config requiere un usuario autenticado (alcanza con anónimo).
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            // Suscribir una sola vez; el handler corre también en cada refresh posterior.
            RemoteConfigService.Instance.FetchCompleted += ApplyConfig;

            await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());

            // Iniciar polling periódico para detectar cambios hechos en el dashboard
            // mientras el juego está corriendo (el SDK no lo hace automáticamente).
            if (_pollingIntervalSeconds > 0f)
                StartCoroutine(PollingCoroutine());
        }
        catch (Exception e)
        {
            // Fail-open: si UGS no está configurado o no hay red, el juego sigue jugable
            // con todo el contenido disponible (los sets quedan vacíos).
            Logger.LogWarning($"[LiveOps] No se pudo inicializar Remote Config: {e.Message}. " +
                              "Se continúa con todo el contenido disponible (fail-open).");
        }
    }

    /// <summary>
    /// Re-pide la config al servidor en intervalos regulares.
    /// Necesario porque el SDK de Remote Config NO notifica cambios del dashboard en tiempo real:
    /// solo obtiene la config una vez por sesión salvo que se llame FetchConfigsAsync de nuevo.
    /// </summary>
    private IEnumerator PollingCoroutine()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(_pollingIntervalSeconds);
            _ = RefreshAsync();
        }
    }

    /// <summary>Vuelve a pedir la config al servicio (ej. al volver del background, o manual).</summary>
    public async Task RefreshAsync()
    {
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn) return;
            await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
        }
        catch (Exception e)
        {
            Logger.LogWarning($"[LiveOps] Refresh de Remote Config falló: {e.Message}");
        }
    }

    private void ApplyConfig(ConfigResponse response)
    {
        LastOrigin = response.requestOrigin;
        HasFetched = true;

        RuntimeConfig cfg = RemoteConfigService.Instance.appConfig;

        PopulateIntSet(cfg.GetJson(RemoteConfigKeys.DISABLED_TRACK_IDS, "[]"), _disabledTracks);
        PopulateIntSet(cfg.GetJson(RemoteConfigKeys.DISABLED_SKIN_IDS,  "[]"), _disabledSkins);
        PopulatePowerUpSet(cfg.GetJson(RemoteConfigKeys.DISABLED_POWERUPS, "[]"), _disabledPowerUps);

        if (_verboseLogging) LogState();

        OnConfigApplied?.Invoke();
    }

    // ── Consultas públicas (las usa la UI y el gameplay) ──────────────────────────

    /// <summary>True si el mapa está disponible para jugar/seleccionar.</summary>
    public bool IsTrackAvailable(int trackId) => !_disabledTracks.Contains(trackId);

    /// <summary>True si la skin de auto está disponible para seleccionar.</summary>
    public bool IsSkinAvailable(int skinId) => !_disabledSkins.Contains(skinId);

    /// <summary>True si el power-up puede aparecer/otorgarse. <c>None</c> nunca está disponible.</summary>
    public bool IsPowerUpAvailable(PowerUpType type) =>
        type != PowerUpType.None && !_disabledPowerUps.Contains(type);

    // ── Parsing (Newtonsoft, ya presente en el proyecto vía Leaderboard) ──────────

    private static void PopulateIntSet(string json, HashSet<int> target)
    {
        target.Clear();
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            JArray array = JArray.Parse(json);
            foreach (JToken token in array)
            {
                if (token.Type == JTokenType.Integer)
                    target.Add(token.Value<int>());
                else if (int.TryParse(token.ToString(), out int parsed))
                    target.Add(parsed);
            }
        }
        catch (Exception e)
        {
            // JSON mal formado → fail-open (set vacío).
            Logger.LogWarning($"[LiveOps] No se pudo parsear lista de IDs: {e.Message}. JSON='{json}'");
        }
    }

    private static void PopulatePowerUpSet(string json, HashSet<PowerUpType> target)
    {
        target.Clear();
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            JArray array = JArray.Parse(json);
            foreach (JToken token in array)
            {
                string name = token.ToString();
                // Acepta nombres del enum (case-insensitive). Ignora "None" y valores desconocidos.
                if (Enum.TryParse(name, true, out PowerUpType type) && type != PowerUpType.None)
                    target.Add(type);
                else
                    Logger.LogWarning($"[LiveOps] Power-up desconocido en disabledPowerUps: '{name}' (ignorado).");
            }
        }
        catch (Exception e)
        {
            Logger.LogWarning($"[LiveOps] No se pudo parsear disabledPowerUps: {e.Message}. JSON='{json}'");
        }
    }

    private void LogState()
    {
        string tracks   = _disabledTracks.Count   == 0 ? "ninguno" : string.Join(", ", _disabledTracks);
        string skins    = _disabledSkins.Count    == 0 ? "ninguna" : string.Join(", ", _disabledSkins);
        string powerUps = _disabledPowerUps.Count == 0 ? "ninguno" : string.Join(", ", _disabledPowerUps);

        Logger.Log($"[LiveOps] Config aplicada (origen: {LastOrigin}).\n" +
                   $"          Mapas deshabilitados: {tracks}\n" +
                   $"          Skins deshabilitadas: {skins}\n" +
                   $"          Power-ups deshabilitados: {powerUps}");
    }

    private void OnDestroy()
    {
        // Desuscribir el handler estático del SDK para no dejar referencias colgando.
        try { RemoteConfigService.Instance.FetchCompleted -= ApplyConfig; }
        catch { /* el SDK puede no estar inicializado; ignorar */ }
    }
}