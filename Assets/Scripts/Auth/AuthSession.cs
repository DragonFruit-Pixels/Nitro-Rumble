using System;
using Photon.Pun;

/// <summary>
/// Sesión del usuario logueado (estática, sobrevive cambios de escena).
/// Al loguear, fija el nickname de Photon con el username → conecta con el req. 14
/// (el nombre que se ve sobre el auto es el del usuario autenticado).
///
/// Si no hay usuario logueado, el jugador es un INVITADO: juega anónimo (NickName vacío,
/// sin cartelito sobre el auto) y no sube al leaderboard.
/// </summary>
public static class AuthSession
{
    public static string Username { get; private set; }
    public static bool   IsLoggedIn => !string.IsNullOrEmpty(Username);

    /// <summary>Se dispara cuando cambia el estado de sesión (login o logout). La UI del menú lo escucha.</summary>
    public static event Action OnChanged;

    public static void SetUser(string username)
    {
        Username = username;
        if (!string.IsNullOrEmpty(username))
            PhotonNetwork.NickName = username;
        Logger.Log($"[AuthSession] Usuario logueado: {username}");
        OnChanged?.Invoke();
    }

    public static void Clear()
    {
        Username = null;
        // Invitado: sin nickname → auto anónimo.
        PhotonNetwork.NickName = string.Empty;
        Logger.Log("[AuthSession] Sesión cerrada.");
        OnChanged?.Invoke();
    }
}
