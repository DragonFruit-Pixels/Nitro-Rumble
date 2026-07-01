using Photon.Pun;
using UnityEngine;

/// <summary>
/// Escucha el fin de carrera y sube el resultado del jugador LOCAL a la tabla de
/// puntuaciones online (LeaderboardService / REST). Cada cliente reporta solo su
/// propio auto, así que tanto el ganador como los rezagados registran su tiempo.
///
/// Se engancha a <see cref="RaceManager.OnRaceFinished"/>, que dispara en todos los
/// clientes cuando llega el podio oficial; en ese momento cada Racer ya tiene su
/// <see cref="Racer.FinishTime"/> y <see cref="Racer.Position"/> seteados.
///
/// Colocar en la escena de juego (junto al RaceManager).
/// </summary>
public class RaceResultReporter : MonoBehaviour
{
    private bool _subscribed;
    private bool _reported;

    private void Update()
    {
        // RaceManager se instancia en la escena; suscribimos en cuanto exista.
        if (_subscribed || RaceManager.Instance == null) return;

        RaceManager.Instance.OnRaceFinished += HandleRaceFinished;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (_subscribed && RaceManager.Instance != null)
            RaceManager.Instance.OnRaceFinished -= HandleRaceFinished;
        _subscribed = false;
    }

    // El podio ya está resuelto: subimos el resultado del auto local, una sola vez.
    private void HandleRaceFinished(Racer winner)
    {
        if (_reported) return;

        // Solo los jugadores logueados suben al leaderboard. Los invitados juegan anónimos.
        if (!AuthSession.IsLoggedIn)
        {
            _reported = true;
            Logger.Log("[RaceResultReporter] Invitado (sin login): no se sube resultado al leaderboard.");
            return;
        }

        Racer local = FindLocalRacer();
        if (local == null)
        {
            Logger.LogWarning("[RaceResultReporter] No se encontró el racer local — no se sube resultado.");
            return;
        }
        _reported = true;

        // FinishTime se setea por el podio (online). Offline queda 0 → caemos a RaceTime.
        double time = local.FinishTime > 0.0 ? local.FinishTime : local.RaceTime;

        Logger.Log($"[RaceResultReporter] Subiendo resultado local: {local.PlayerName} — {time:F2}s (P{local.Position})");

        if (LeaderboardService.Instance != null)
            LeaderboardService.Instance.SubmitScore(local.PlayerName, time, local.Position);
        else
            Logger.LogWarning("[RaceResultReporter] LeaderboardService no presente en la escena.");
    }

    private static Racer FindLocalRacer()
    {
        if (RaceManager.Instance == null) return null;

        foreach (var racer in RaceManager.Instance.GetRacers())
        {
            bool isLocal = PhotonViewAuthority.HasLocalInputAuthority(racer.photonView);
            if (isLocal) return racer;
        }
        return null;
    }
}
