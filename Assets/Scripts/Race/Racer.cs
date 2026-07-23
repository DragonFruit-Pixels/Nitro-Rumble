using Photon.Pun;
using UnityEngine;

/// <summary>
/// Componente del auto que lleva la cuenta de checkpoints, vueltas y posición.
/// Se registra en RaceManager al iniciar.
/// </summary>
[RequireComponent(typeof(CarController))]
public class Racer : MonoBehaviourPun
{
    public int    CurrentLap     { get; private set; } = 0;
    public int    LastCheckpoint { get; private set; } = -1;
    public float  RaceTime       { get; private set; } = 0f;
    public int    Position       { get; set; }         = 0;
    public double FinishTime     { get; set; }         = 0.0;
    public bool   IsFinished     { get; private set; } = false;
    public bool   IsBot          { get; private set; } = false;
    public int    BotIndex      { get; private set; } = 0;

    // Nombres fijos para los bots (maximo 3, ver RoomConfigPanel.ChangeBots). El mismo indice
    // se usa en la sala (InRoomHandler) y en la carrera (aca), asi que el bot que se ve como
    // "Bot Rayo" en el lobby es el mismo que corre como "Bot Rayo" en pista.
    public static readonly string[] BotNames = { "Bot Rayo", "Bot Trueno", "Bot Centella" };

    // Skin fija por bot (indices de CarSkinCatalogueSO: 0=Yellow, 1=Green, 2=Red, 3=Purple).
    // Se deja Yellow libre para no pisar visualmente al humano que nunca cambió su skin default.
    public static readonly int[] BotSkinIDs = { 3, 2, 1 };

    public string PlayerName
    {
        get
        {
            // El bot no tiene Player propio: su Owner es quien lo maneja (el Master Client),
            // asi que photonView.Owner.NickName mostraria el nombre del humano, no del bot.
            if (IsBot) return BotNames[BotIndex % BotNames.Length];

            if (photonView != null && photonView.Owner != null && !string.IsNullOrEmpty(photonView.Owner.NickName))
                return photonView.Owner.NickName;
            return gameObject.name;
        }
    }

    // True si es el jugador local o si no hay Photon activo (offline/testing).
    private bool IsLocal => PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private CarController _controller;
    private bool _racing = false;

private void Awake()
    {
        _controller = GetComponent<CarController>();

        // Instantiation data: [0] = esBot (bool), [1] = indice del bot (int, para BotNames).
        // Viaja pegado a la creacion en red, asi que es identico en todos los clientes y no
        // depende de quien sea el dueno actual (sobrevive a un cambio de Master Client).
        object[] data = photonView != null ? photonView.InstantiationData : null;
        IsBot = data != null && data.Length > 0 && data[0] is bool isBot && isBot;
        if (IsBot && data.Length > 1 && data[1] is int botIndex)
            BotIndex = botIndex;
    }

    private void Start()
    {
        RaceManager.Instance.RegisterRacer(this);
    }

    private void Update()
    {
        if (_racing) RaceTime += Time.deltaTime;
    }

    public void SetCanMove(bool value)
    {
        // Nunca activar input/timer en autos remotos.
        if (!IsLocal && value) return;

        _controller.CanMove = value;
        _racing = value;
    }

    // Llamado via RPC en todos los clientes cuando este auto completa su última vuelta.
    // Deshabilita los triggers de colisión lateral para que no interfiera con los demás.
    [PunRPC]
    public void RPC_SetFinished()
    {
        IsFinished = true;

        // Deshabilitar sensores laterales de colisión.
        foreach (var side in GetComponentsInChildren<CarCollisionSide>())
        {
            var col = side.GetComponent<BoxCollider>();
            if (col != null) col.enabled = false;
        }

        // Freezar el sphere en todos los clientes (sin pasar por IsLocalAuthority)
        // y deshabilitar sus colliders para que no interactúe con ningún otro auto.
        var rb = GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            foreach (var col in rb.GetComponents<Collider>())
                col.enabled = false;
        }

        SetCanMove(false);
    }

    public void AdvanceCheckpoint(int index)
    {
        LastCheckpoint = index;
    }

    public void CompleteLap()
    {
        CurrentLap++;
        LastCheckpoint = -1; // resetear checkpoints para la siguiente vuelta
    }
}
