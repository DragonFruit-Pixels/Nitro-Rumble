using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Cliente REST de la tabla de puntuaciones. Sube tiempos de carrera (POST) y lee
/// el top-N ordenado por tiempo ascendente (GET), comunicándose con un backend HTTP
/// vía <see cref="UnityWebRequest"/> + corrutinas y serializando con Newtonsoft.Json.
///
/// CONTRATO REST (el backend debe implementar):
///   POST {_baseUrl}            body = JSON de ScoreDto (o string XOR si _encryptPayload)
///                              Content-Type: application/json (o text/plain si cifrado)
///   GET  {_baseUrl}?limit={n}  → array JSON de ScoreDto ordenado por "time" asc.
///                              (el cliente igual re-ordena y corta a n por seguridad)
///
/// ACTIVACIÓN:
///   Setear _baseUrl en el inspector. Si queda vacío, el servicio es no-op silencioso
///   (permite correr el juego sin backend). Para anti-cheat básico, activar
///   _encryptPayload y compartir _xorKey con el servidor (ver <see cref="XorCipher"/>).
/// </summary>
public class LeaderboardService : Singleton<LeaderboardService>
{
    [Header("Backend")]
    [Tooltip("URL base del endpoint REST del leaderboard. Vacío = servicio deshabilitado (no-op).")]
    [SerializeField] private string _baseUrl = "";

    [Header("Anti-cheat (XOR)")]
    [Tooltip("Si está activo, el body se cifra con XOR antes de enviar y se descifra al recibir. El backend debe usar la misma clave.")]
    [SerializeField] private bool _encryptPayload = false;
    [Tooltip("Clave secreta compartida con el backend para el cifrado XOR.")]
    [SerializeField] private string _xorKey = "ChangeMeSecretKey";

    /// <summary>True si hay una URL configurada (el servicio puede operar).</summary>
    public bool IsReady => !string.IsNullOrWhiteSpace(_baseUrl);

    public override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicado destruido por el Singleton

        transform.SetParent(null);    // DontDestroyOnLoad solo funciona en GameObjects raíz
        DontDestroyOnLoad(gameObject);

        if (!IsReady)
            Logger.LogWarning("[Leaderboard] _baseUrl vacío — leaderboard deshabilitado (no-op). Configurá la URL en el inspector.");
    }

    // ── Submit (POST) ────────────────────────────────────────────────────────

    /// <summary>
    /// Sube un resultado de carrera. No-op silencioso si no hay URL configurada.
    /// </summary>
    public void SubmitScore(string playerName, double timeSeconds, int position = 0)
    {
        if (!IsReady)
        {
            Logger.LogWarning("[Leaderboard] SubmitScore ignorado — _baseUrl no configurada.");
            return;
        }

        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "Player";

        var dto = new ScoreDto(
            playerName,
            timeSeconds,
            position,
            SceneManager.GetActiveScene().name,
            DateTime.UtcNow.ToString("o")
        );

        StartCoroutine(PostScoreRoutine(dto));
    }

    private IEnumerator PostScoreRoutine(ScoreDto dto)
    {
        string json = JsonConvert.SerializeObject(dto);
        bool   encrypted = _encryptPayload;
        string body = encrypted ? XorCipher.Encrypt(json, _xorKey) : json;

        byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

        using (var req = new UnityWebRequest(_baseUrl, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", encrypted ? "text/plain" : "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.ProtocolError)
            {
                Logger.LogError($"[Leaderboard] Error al subir score: {req.error} (HTTP {req.responseCode})");
            }
            else
            {
                Logger.Log($"[Leaderboard] Score subido: {dto.Name} — {dto.Time:F2}s");
            }
        }
    }

    // ── Query (GET) ───────────────────────────────────────────────────────────

    /// <summary>
    /// Trae los mejores N tiempos globales (orden ascendente) y los entrega por callback.
    /// Entrega lista vacía ante error o si no hay URL configurada.
    /// </summary>
    public void GetTopScores(int n, Action<List<ScoreEntry>> callback)
    {
        if (callback == null) return;

        if (!IsReady)
        {
            Logger.LogWarning("[Leaderboard] GetTopScores ignorado — _baseUrl no configurada.");
            callback(new List<ScoreEntry>());
            return;
        }

        StartCoroutine(GetScoresRoutine(n, null, callback));
    }

    /// <summary>
    /// Trae los mejores N tiempos para un track específico (filtra por nombre de escena).
    /// Entrega lista vacía ante error o si no hay URL configurada.
    /// </summary>
    public void GetTopScoresByTrack(string trackSceneName, int n, Action<List<ScoreEntry>> callback)
    {
        if (callback == null) return;

        if (!IsReady)
        {
            Logger.LogWarning("[Leaderboard] GetTopScoresByTrack ignorado — _baseUrl no configurada.");
            callback(new List<ScoreEntry>());
            return;
        }

        StartCoroutine(GetScoresRoutine(n, trackSceneName, callback));
    }

    private IEnumerator GetScoresRoutine(int n, string trackFilter, Action<List<ScoreEntry>> callback)
    {
        // Pedimos el nodo completo y recortamos a N en el cliente. No mandamos ?limit
        // porque Firebase RTDB rechaza query params desconocidos; con un backend propio
        // igual funciona (devuelve todo y nosotros recortamos).
        string url = _baseUrl;

        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.ProtocolError)
            {
                Logger.LogError($"[Leaderboard] Error al leer scores: {req.error} (HTTP {req.responseCode})");
                callback(new List<ScoreEntry>());
                yield break;
            }

            callback(ParseScores(req.downloadHandler.text, n, trackFilter));
        }
    }

    private List<ScoreEntry> ParseScores(string raw, int n, string trackFilter = null)
    {
        var entries = new List<ScoreEntry>();
        if (string.IsNullOrWhiteSpace(raw)) return entries;

        string json = _encryptPayload ? XorCipher.Decrypt(raw, _xorKey) : raw;

        try
        {
            // El backend puede devolver un ARRAY [ {...}, {...} ] (REST propio / mock)
            // o un OBJETO/mapa { "id": {...}, ... } (Firebase RTDB). Soportamos ambos.
            var dtos = new List<ScoreDto>();
            JToken root = JToken.Parse(json);

            if (root.Type == JTokenType.Array)
            {
                dtos = root.ToObject<List<ScoreDto>>();
            }
            else if (root.Type == JTokenType.Object)
            {
                foreach (JProperty prop in ((JObject)root).Properties())
                {
                    var dto = prop.Value.ToObject<ScoreDto>();
                    if (dto != null) dtos.Add(dto);
                }
            }

            if (dtos == null || dtos.Count == 0) return entries;

            // Filtrar por track si se especificó uno.
            if (!string.IsNullOrEmpty(trackFilter))
                dtos = dtos.FindAll(d => string.Equals(d.Track, trackFilter, System.StringComparison.OrdinalIgnoreCase));

            // Orden ascendente por tiempo y recorte al top-N.
            dtos.Sort((a, b) => a.Time.CompareTo(b.Time));
            int count = Mathf.Min(n, dtos.Count);
            for (int i = 0; i < count; i++)
                entries.Add(dtos[i].ToEntry());
        }
        catch (Exception e)
        {
            Logger.LogError($"[Leaderboard] Error al deserializar scores: {e.Message}");
        }

        return entries;
    }
}
