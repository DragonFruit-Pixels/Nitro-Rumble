using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Autenticación usuario/contraseña contra una planilla de Google Sheets, vía un
/// Google Apps Script publicado como Web App (doPost). Reusa el patrón de
/// <see cref="LeaderboardService"/>: UnityWebRequest + corrutinas + Newtonsoft.Json,
/// y ofusca la contraseña con <see cref="XorCipher"/> antes de enviarla.
///
/// CONTRATO con el Apps Script (doPost recibe JSON, devuelve JSON):
///   REQUEST  { "action": "login"|"register", "username": "...", "password": "&lt;xor&gt;" }
///   RESPONSE { "success": true }  ó  { "success": false, "error": "mensaje" }
///   El Apps Script desencripta la password con LA MISMA _xorKey y la compara/guarda
///   (idealmente como hash) en la hoja: columnas [username, passwordXor/hash, salt].
///
/// ACTIVACIÓN: setear _baseUrl (la URL .../exec del Apps Script) en el inspector.
/// Si queda vacío, el servicio es no-op y los callbacks devuelven error explicativo.
/// </summary>
public class AuthService : Singleton<AuthService>
{
    [Header("Backend (Google Apps Script Web App)")]
    [Tooltip("URL .../exec del Apps Script publicado como Web App. Vacío = deshabilitado.")]
    [SerializeField] private string _baseUrl = "";

    [Header("Ofuscación (XOR)")]
    [Tooltip("Clave secreta compartida con el Apps Script para ofuscar la contraseña.")]
    [SerializeField] private string _xorKey = "ChangeMeSecretKey";

    public bool IsReady => !string.IsNullOrWhiteSpace(_baseUrl);

    public override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // duplicado destruido por el Singleton

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        if (!IsReady)
            Logger.LogWarning("[Auth] _baseUrl vacío — autenticación deshabilitada. Configurá la URL del Apps Script en el inspector.");
    }

    /// <summary>Registra un nuevo usuario. callback(éxito, mensajeDeError).</summary>
    public void Register(string username, string password, Action<bool, string> callback)
        => StartCoroutine(AuthRoutine("register", username, password, callback));

    /// <summary>Loguea un usuario existente. callback(éxito, mensajeDeError).</summary>
    public void Login(string username, string password, Action<bool, string> callback)
        => StartCoroutine(AuthRoutine("login", username, password, callback));

    private IEnumerator AuthRoutine(string action, string username, string password, Action<bool, string> callback)
    {
        if (!IsReady)
        {
            callback?.Invoke(false, "Autenticación no configurada (falta URL del Apps Script).");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            callback?.Invoke(false, "Usuario y contraseña no pueden estar vacíos.");
            yield break;
        }

        var dto = new AuthRequestDto
        {
            action   = action,
            username = username.Trim(),
            password = XorCipher.Encrypt(password, _xorKey) // ofuscada en tránsito
        };
        byte[] body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto));

        using (var req = new UnityWebRequest(_baseUrl, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler   = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.ProtocolError)
            {
                Logger.LogError($"[Auth] {action} falló: {req.error} (HTTP {req.responseCode})");
                callback?.Invoke(false, $"Error de red: {req.error}");
                yield break;
            }

            AuthResponseDto resp;
            try
            {
                resp = JsonConvert.DeserializeObject<AuthResponseDto>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                Logger.LogError($"[Auth] Respuesta no válida: {e.Message} | raw: {req.downloadHandler.text}");
                callback?.Invoke(false, "Respuesta del servidor no válida.");
                yield break;
            }

            if (resp != null && resp.success)
            {
                AuthSession.SetUser(dto.username);
                callback?.Invoke(true, null);
            }
            else
            {
                callback?.Invoke(false, resp?.error ?? "Credenciales inválidas.");
            }
        }
    }

    // DTOs del contrato REST. Nombres en minúscula para matchear el JSON del Apps Script.
    [Serializable]
    private class AuthRequestDto
    {
        public string action;
        public string username;
        public string password;
    }

    [Serializable]
    private class AuthResponseDto
    {
        public bool   success;
        public string error;
    }
}
