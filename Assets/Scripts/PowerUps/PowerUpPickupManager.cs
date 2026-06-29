using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[DisallowMultipleComponent]
public class PowerUpPickupManager : MonoBehaviourPunCallbacks
{
    // Clave de Room Custom Property por caja. Valor: -1 = disponible, actorNumber = tomada.
    private const string BOX_PROP_PREFIX = "pu_box_";
    private const int    AVAILABLE       = -1;

    [Header("Pickup")]
    [SerializeField] private float _respawnDelay = 6f;
    [SerializeField] private PowerUpType[] _powerUpPool =
    {
        PowerUpType.EMP,
        PowerUpType.Shield,
        PowerUpType.Turbo
    };

    private readonly Dictionary<int, PowerUpBox>       _boxes            = new Dictionary<int, PowerUpBox>();
    private readonly Dictionary<int, Coroutine>        _respawnRoutines  = new Dictionary<int, Coroutine>();
    // Inventario en espera de confirmación CAS, clave = boxId
    private readonly Dictionary<int, PowerUpInventory> _pendingInventory = new Dictionary<int, PowerUpInventory>();

    public static PowerUpPickupManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PowerUpPickupManager] duplicate manager found; keeping first instance.", this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        RegisterSceneBoxes();

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            InitializeBoxProperties();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterBox(PowerUpBox box)
    {
        if (box == null) return;

        int boxId = box.BoxId;
        if (boxId < 0)
        {
            Debug.LogWarning($"[PowerUpPickupManager] PowerUpBox '{box.name}' has invalid boxId={boxId}. Assign a stable scene id.", box);
            return;
        }

        if (_boxes.TryGetValue(boxId, out PowerUpBox existing) && existing != box)
            Debug.LogWarning($"[PowerUpPickupManager] duplicate boxId={boxId}; latest registration replaced previous box.", box);

        _boxes[boxId] = box;
    }

    public void UnregisterBox(PowerUpBox box)
    {
        if (box == null) return;

        int boxId = box.BoxId;
        if (_boxes.TryGetValue(boxId, out PowerUpBox existing) && existing == box)
            _boxes.Remove(boxId);
    }

    public void RequestPickup(PowerUpBox box, PowerUpInventory inventory)
    {
        if (box == null || inventory == null) return;

        var racer = inventory.GetComponent<Racer>();
        if (racer != null && racer.IsFinished) return;

        if (!PhotonNetwork.InRoom)
        {
            GrantPickupLocal(box, inventory);
            return;
        }

        string key     = BoxKey(box.BoxId);
        int    myActor = PhotonNetwork.LocalPlayer.ActorNumber;

        // Guardar inventario local para asignarlo cuando el CAS sea confirmado.
        _pendingInventory[box.BoxId] = inventory;

        // CAS atómico en el servidor de Photon: el segundo parámetro de SetCustomProperties
        // es el expectedValues — la operación solo se aplica si el valor actual coincide.
        // Si dos jugadores llaman simultáneamente, el servidor aplica el primero que llega
        // y descarta el segundo — garantizando que una sola caja se asigne a un solo jugador,
        // sin buffers de tiempo arbitrarios ni dependencia del MC como árbitro local.
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { key, myActor } },
            new Hashtable { { key, AVAILABLE } }
        );
    }

    // Disparado en TODOS los clientes cuando una Room Custom Property cambia.
    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        foreach (DictionaryEntry entry in changedProps)
        {
            string key = entry.Key as string;
            if (key == null || !key.StartsWith(BOX_PROP_PREFIX)) continue;

            if (!int.TryParse(key.Substring(BOX_PROP_PREFIX.Length), out int boxId)) continue;

            int value = (int)entry.Value;

            // Actualizar visual para todos los clientes
            if (_boxes.TryGetValue(boxId, out PowerUpBox box))
                box.SetAvailable(value == AVAILABLE);

            if (value == AVAILABLE)
                continue; // caja respawneó — nada más que hacer

            // Resolver si el jugador local ganó el CAS
            if (_pendingInventory.TryGetValue(boxId, out PowerUpInventory inv))
            {
                _pendingInventory.Remove(boxId);
                if (value == PhotonNetwork.LocalPlayer.ActorNumber && inv != null)
                    inv.ReceivePowerUp(PickPowerUp(), value);
            }

            // Solo el MC programa el respawn
            if (PhotonNetwork.IsMasterClient)
                ScheduleRespawn(boxId);
        }
    }

    private void InitializeBoxProperties()
    {
        var props = new Hashtable();
        foreach (int id in _boxes.Keys)
            props[BoxKey(id)] = AVAILABLE;

        if (props.Count > 0)
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    private void ScheduleRespawn(int boxId)
    {
        if (_respawnRoutines.TryGetValue(boxId, out Coroutine existing) && existing != null)
            StopCoroutine(existing);
        _respawnRoutines[boxId] = StartCoroutine(RespawnRoutine(boxId));
    }

    private IEnumerator RespawnRoutine(int boxId)
    {
        yield return new WaitForSeconds(_respawnDelay);
        _respawnRoutines.Remove(boxId);
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { BoxKey(boxId), AVAILABLE } });
    }

    private void RegisterSceneBoxes()
    {
        foreach (PowerUpBox box in FindObjectsOfType<PowerUpBox>(true))
            RegisterBox(box);
    }

    private void GrantPickupLocal(PowerUpBox box, PowerUpInventory inventory)
    {
        box.SetAvailable(false);
        inventory.ReceivePowerUp(PickPowerUp(), -1);
        StartCoroutine(LocalRespawnRoutine(box));
    }

    private IEnumerator LocalRespawnRoutine(PowerUpBox box)
    {
        yield return new WaitForSeconds(_respawnDelay);
        if (box != null) box.SetAvailable(true);
    }

    private readonly List<PowerUpType> _availablePool = new List<PowerUpType>();

    private PowerUpType PickPowerUp()
    {
        if (_powerUpPool == null || _powerUpPool.Length == 0)
            return PowerUpType.EMP;

        // LiveOps: descartar power-ups deshabilitados desde Remote Config.
        // Se evalúa en el MasterClient (autoridad), así que un cliente con config
        // vieja no puede recibir un power-up bloqueado.
        _availablePool.Clear();
        for (int i = 0; i < _powerUpPool.Length; i++)
        {
            PowerUpType type = _powerUpPool[i];
            if (LiveOpsConfig.Instance == null || LiveOpsConfig.Instance.IsPowerUpAvailable(type))
                _availablePool.Add(type);
        }

        // Caso improbable: LiveOps deshabilitó TODO el pool. Para no romper las cajas,
        // ignoramos el filtro y devolvemos uno cualquiera del pool original.
        if (_availablePool.Count == 0)
            return _powerUpPool[Random.Range(0, _powerUpPool.Length)];

        return _availablePool[Random.Range(0, _availablePool.Count)];
    }

    private static string BoxKey(int boxId) => BOX_PROP_PREFIX + boxId;
}
