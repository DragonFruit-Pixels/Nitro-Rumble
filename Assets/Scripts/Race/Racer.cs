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

    public string PlayerName
    {
        get
        {
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
