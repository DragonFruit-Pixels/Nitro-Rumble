using Photon.Pun;

/// <summary>
/// Sesión del usuario logueado (estática, sobrevive cambios de escena).
/// Al loguear, fija el nickname de Photon con el username → conecta con el req. 14
/// (el nombre que se ve sobre el auto es el del usuario autenticado).
/// </summary>
public static class AuthSession
{
    public static string Username { get; private set; }
    public static bool   IsLoggedIn => !string.IsNullOrEmpty(Username);

    public static void SetUser(string username)
    {
        Username = username;
        if (!string.IsNullOrEmpty(username))
            PhotonNetwork.NickName = username;
        Logger.Log($"[AuthSession] Usuario logueado: {username}");
    }

    public static void Clear()
    {
        Username = null;
        Logger.Log("[AuthSession] Sesión cerrada.");
    }
}
