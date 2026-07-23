using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Voice.Unity;
using UnityEngine;

/// <summary>
/// Spawnea el auto del jugador local al cargar la escena de juego.
/// Requiere que el prefab del auto esté en Assets/Resources/.
/// </summary>
public class PlayerSpawner : MonoBehaviourPunCallbacks
{
    [Header("Spawn Settings")]
    [SerializeField] private string _carPrefabName = "Car";
    [SerializeField] private Transform[] _spawnPoints;

    [Header("References")]
    [SerializeField] private CarCamera _camera;

    private void Start()
    {
        SpawnLocalCar();
        SpawnBotsIfMaster();
    }

    private void SpawnLocalCar()
    {
        Transform spawnPoint = GetSpawnPoint();

        GameObject carGO;

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            carGO = PhotonNetwork.Instantiate(
                _carPrefabName,
                spawnPoint.position,
                spawnPoint.rotation
            );
        }
        else
        {
            // Fallback offline (testing en editor sin conexión).
            GameObject prefab = Resources.Load<GameObject>(_carPrefabName);
            carGO = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        }

        if (_camera != null && carGO.TryGetComponent(out CarController controller))
            _camera.SetTarget(controller);

        if (SettingsManager.Instance != null && carGO.TryGetComponent(out Recorder recorder))
            SettingsManager.Instance.ApplyVoice(recorder);
    }

// Solo lo ejecuta el Master Client (una vez, en su propia copia local de la escena) para
    // no duplicar bots. Usa los spawn points que no ocupan los jugadores reales (el grid de
    // jugadores ya fue armado en Keys.GRID_ACTORS_KEY antes de cargar esta escena).
// Solo lo ejecuta el Master Client (una vez, en su propia copia local de la escena) para
    // no duplicar bots. Usa los spawn points que no ocupan los jugadores reales (el grid de
    // jugadores ya fue armado en Keys.GRID_ACTORS_KEY antes de cargar esta escena).
    private void SpawnBotsIfMaster()
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        if (_spawnPoints == null || _spawnPoints.Length == 0) return;

        var roomProps = PhotonNetwork.CurrentRoom.CustomProperties;

        if (!roomProps.TryGetValue(Keys.BOTS_COUNT_KEY, out object botsObj) || !(botsObj is int botsCount) || botsCount <= 0)
            return;

        int realPlayers = roomProps.TryGetValue(Keys.GRID_ACTORS_KEY, out object gridObj) && gridObj is int[] gridActors
            ? gridActors.Length
            : PhotonNetwork.CurrentRoom.PlayerCount;

        // Nunca mas bots de los que hay spawn points libres — si la sala esta llena de
        // jugadores reales, no hay lugar fisico y no se spawnea ninguno (mejor eso que
        // superponer un bot arriba de un jugador real).
        int availableSlots = Mathf.Max(0, _spawnPoints.Length - realPlayers);
        if (botsCount > availableSlots)
        {
            Logger.LogWarning($"[PlayerSpawner] Pedidos {botsCount} bots pero solo hay {availableSlots} spawn points libres ({realPlayers} jugadores reales de {_spawnPoints.Length}). Se acota.");
            botsCount = availableSlots;
        }

        for (int i = 0; i < botsCount; i++)
        {
            int spawnIndex = (realPlayers + i) % _spawnPoints.Length;
            Transform sp = _spawnPoints[spawnIndex];
            int skinID = Racer.BotSkinIDs[i % Racer.BotSkinIDs.Length];
            PhotonNetwork.Instantiate(_carPrefabName, sp.position, sp.rotation, 0, new object[] { true, i, skinID });
        }
    }


    private Transform GetSpawnPoint()
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
            return transform;

        // El Master Client define la grilla oficial en Room Custom Properties.
        int index = RaceGridManager.TryGetLocalSpawnIndex(_spawnPoints.Length, out int gridIndex)
            ? gridIndex
            : 0;

        return _spawnPoints[index];
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_spawnPoints == null) return;

        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            Transform sp = _spawnPoints[i];
            if (sp == null) continue;

            Gizmos.color = i == 0 ? Color.yellow : Color.cyan;
            Gizmos.DrawSphere(sp.position + Vector3.up * 0.5f, 0.4f);
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawWireCube(sp.position + Vector3.up * 0.5f,
                new Vector3(1.8f, 1f, 3.5f));

            // Flecha de dirección
            Gizmos.color = Color.green;
            Gizmos.DrawRay(sp.position + Vector3.up * 0.5f, sp.forward * 2f);

            UnityEditor.Handles.Label(
                sp.position + Vector3.up * 1.5f,
                $"P{i + 1}",
                new GUIStyle { normal = { textColor = i == 0 ? Color.yellow : Color.cyan },
                               fontStyle = FontStyle.Bold, fontSize = 14 });
        }
    }
#endif
}

public static class RaceGridManager
{
    public static bool AssignGridFromCurrentRoom()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return false;

        int[] gridActors = PhotonNetwork.CurrentRoom.Players.Values
            .Where(player => !player.IsInactive)
            .OrderBy(player => player.ActorNumber)
            .Select(player => player.ActorNumber)
            .ToArray();

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { Keys.GRID_ACTORS_KEY, gridActors }
        });

        Logger.Log($"[RaceGridManager] Grid assigned: {string.Join(", ", gridActors)}");
        return true;
    }

    public static bool TryGetLocalSpawnIndex(int spawnCount, out int index)
    {
        index = 0;

        if (spawnCount <= 0 || !PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return false;

        if (TryGetGridActors(out int[] gridActors))
        {
            int gridIndex = System.Array.IndexOf(gridActors, PhotonNetwork.LocalPlayer.ActorNumber);
            if (gridIndex >= 0)
            {
                index = gridIndex % spawnCount;
                return true;
            }
        }

        return TryGetCompactLocalSpawnIndex(spawnCount, out index);
    }

    private static bool TryGetGridActors(out int[] gridActors)
    {
        gridActors = null;

        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Keys.GRID_ACTORS_KEY, out object value))
            return false;

        gridActors = value as int[];
        return gridActors != null && gridActors.Length > 0;
    }

    private static bool TryGetCompactLocalSpawnIndex(int spawnCount, out int index)
    {
        index = 0;

        var players = PhotonNetwork.CurrentRoom.Players.Values
            .Where(player => !player.IsInactive)
            .OrderBy(player => player.ActorNumber)
            .ToList();

        int localIndex = players.FindIndex(player => player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber);
        if (localIndex < 0)
            return false;

        index = localIndex % spawnCount;
        return true;
    }
}
