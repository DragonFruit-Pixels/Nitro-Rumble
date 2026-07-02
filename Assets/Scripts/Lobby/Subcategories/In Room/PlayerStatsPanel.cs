using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

/// <summary>
/// Panel dentro de la sala que muestra las estadísticas de todos los jugadores.
/// Publica las stats locales como Player Custom Properties al entrar,
/// y refresca cuando alguien entra/sale o actualiza sus propiedades.
/// </summary>
public class PlayerStatsPanel : MonoBehaviourPunCallbacks
{
    // Player Custom Property keys (Photon)
    public const string PROP_TROPHIES = "trophies";
    public const string PROP_RACES    = "races";
    public const string PROP_WINS     = "wins";
    public const string PROP_SILVERS  = "silvers";
    public const string PROP_BRONZES  = "bronzes";

    [SerializeField] private TMP_Text _titleLabel;
    [SerializeField] private TMP_Text _statsLabel;

    public override void OnEnable()
    {
        base.OnEnable();
        PublishLocalStats();
        Refresh();
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(Language _) => Refresh();

    // ── Photon callbacks ─────────────────────────────────────────────────────

    public override void OnJoinedRoom()                => Refresh();
    public override void OnPlayerEnteredRoom(Player _) => Refresh();
    public override void OnPlayerLeftRoom(Player _)    => Refresh();

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(PROP_TROPHIES) ||
            changedProps.ContainsKey(PROP_RACES)    ||
            changedProps.ContainsKey(PROP_WINS)     ||
            changedProps.ContainsKey(PROP_SILVERS)  ||
            changedProps.ContainsKey(PROP_BRONZES))
            Refresh();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void PublishLocalStats()
    {
        if (!PhotonNetwork.InRoom) return;

        var profile = LocalSaveManager.Instance?.Profile;
        int trophies = profile?.totalTrophies ?? 0;
        int races    = profile?.totalRaces    ?? 0;
        int wins     = profile?.totalWins     ?? 0;
        int silvers  = profile?.totalSilvers  ?? 0;
        int bronzes  = profile?.totalBronzes  ?? 0;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PROP_TROPHIES, trophies },
            { PROP_RACES,    races    },
            { PROP_WINS,     wins     },
            { PROP_SILVERS,  silvers  },
            { PROP_BRONZES,  bronzes  },
        });
    }

    public void Refresh()
    {
        if (_titleLabel != null)
            _titleLabel.text = LocalizationManager.Get("playerStats.title");

        if (_statsLabel == null) return;

        if (!PhotonNetwork.InRoom)
        {
            var p = LocalSaveManager.Instance?.Profile;
            _statsLabel.text = FormatRow(PhotonNetwork.NickName,
                p?.totalRaces ?? 0, p?.totalWins ?? 0,
                p?.totalSilvers ?? 0, p?.totalBronzes ?? 0, p?.totalTrophies ?? 0);
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var kv in PhotonNetwork.CurrentRoom.Players)
        {
            var player = kv.Value;
            int races, wins, silvers, bronzes, trophies;

            if (player.IsLocal && LocalSaveManager.Instance != null)
            {
                // Leer directo del perfil local — sin latencia de round-trip a Photon.
                var prof = LocalSaveManager.Instance.Profile;
                races    = prof.totalRaces;
                wins     = prof.totalWins;
                silvers  = prof.totalSilvers;
                bronzes  = prof.totalBronzes;
                trophies = prof.totalTrophies;
            }
            else
            {
                var props = player.CustomProperties;
                races    = ReadInt(props, PROP_RACES);
                wins     = ReadInt(props, PROP_WINS);
                silvers  = ReadInt(props, PROP_SILVERS);
                bronzes  = ReadInt(props, PROP_BRONZES);
                trophies = ReadInt(props, PROP_TROPHIES);
            }

            sb.AppendLine(FormatRow(player.NickName, races, wins, silvers, bronzes, trophies));
        }
        _statsLabel.text = sb.ToString().TrimEnd();
    }

    private static string FormatRow(string name, int races, int wins, int silvers, int bronzes, int trophies)
    {
        float wr = races > 0 ? wins * 100f / races : 0f;
        return $"{name,-16} | W:{wins} S:{silvers} B:{bronzes} | WR:{wr:F0}% | T:{trophies}";
    }

    private static int ReadInt(ExitGames.Client.Photon.Hashtable props, string key)
        => props.TryGetValue(key, out object val) && val is int i ? i : 0;

    // ── Static helpers ───────────────────────────────────────────────────────

    public static void IncrementTrophies()
    {
        if (LocalSaveManager.Instance == null) return;
        LocalSaveManager.Instance.Profile.totalTrophies++;
        LocalSaveManager.Instance.Save();
    }
}
