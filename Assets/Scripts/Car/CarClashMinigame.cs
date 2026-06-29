using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class CarClashMinigame : MonoBehaviourPun
{
    [Header("References")]
    [SerializeField] private CarController _controller;
    [SerializeField] private CarDriftBoost _driftBoost;

    [Header("Clash Settings")]
    [SerializeField] private float _duration       = 3f;
    [SerializeField] private float _sineAmplitude  = 1.8f;
    [SerializeField] private float _sineFrequency  = 1.2f;
    [SerializeField] private float _pressIncrement = 0.10f;

    public bool  IsInClash { get; private set; }
    public float Progress  { get; private set; }
    public float Duration  => _duration;

    private CarClashMinigame _opponent;
    private Vector3          _startPos;
    private Vector3          _sineAxis;
    private float            _elapsed;
    private bool             _isInitiator;
    private bool             _resolved;
    private ClashHUD         _hud;

    private bool IsLocal =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private void Awake()
    {
        if (_controller == null) _controller = GetComponent<CarController>();
        if (_driftBoost  == null) _driftBoost  = GetComponent<CarDriftBoost>();
    }

    // ── Called only by CarArcadeCollision on local authority ──────────────

    public void TriggerClash(CarClashMinigame opponent)
    {
        if (IsInClash || opponent == null || opponent.IsInClash) return;
        if (_controller?.PhysicsBody == null) return;

        var myRacer  = GetComponent<Racer>();
        var oppRacer = opponent.GetComponent<Racer>();
        if (myRacer  != null && myRacer.IsFinished)  return;
        if (oppRacer != null && oppRacer.IsFinished) return;

        // Cache ViewIDs before sending any RPC (synchronous delivery may alter state)
        int myViewId  = photonView.ViewID;
        int oppViewId = opponent.photonView.ViewID;

        Transform visual = _controller.VisualTransform;
        Vector3 sineAxis = Vector3.ProjectOnPlane(
            visual != null ? visual.forward : transform.forward,
            Vector3.up
        ).normalized;

        photonView.RPC(nameof(RPC_StartClash), RpcTarget.All, oppViewId, sineAxis, true);
        opponent.photonView.RPC(nameof(RPC_StartClash), RpcTarget.All, myViewId, sineAxis, false);
    }

    // ── RPCs ──────────────────────────────────────────────────────────────

    [PunRPC]
    public void RPC_StartClash(int opponentViewId, Vector3 sineAxis, bool isInitiator)
    {
        // Ignorar si este auto ya terminó la carrera (RPC en vuelo cruzado con el finish).
        var myRacer = GetComponent<Racer>();
        if (myRacer != null && myRacer.IsFinished) return;

        PhotonView opponentPV = PhotonView.Find(opponentViewId);
        if (opponentPV == null) return;

        _opponent = opponentPV.GetComponent<CarClashMinigame>();
        if (_opponent == null) return;

        IsInClash    = true;
        Progress     = 0f;
        _elapsed     = 0f;
        _sineAxis    = sineAxis;
        _isInitiator = isInitiator;
        _resolved    = false;

        if (_controller != null)
        {
            Rigidbody body = _controller.PhysicsBody;
            if (body != null) _startPos = body.position;
            _controller.CanMove = false;
        }

        if (IsLocal)
        {
            _hud = FindObjectOfType<ClashHUD>(true);
            _hud?.Show(this, _opponent);
        }
    }

    [PunRPC]
    public void RPC_SyncProgress(float progress)
    {
        Progress = progress;
    }

    [PunRPC]
    public void RPC_EndClash()
    {
        IsInClash = false;
        _opponent = null;
        _resolved = false;

        if (_controller != null)
            _controller.CanMove = true;

        if (IsLocal)
            _hud?.Hide();
    }

    // ── Physics / Update ──────────────────────────────────────────────────

    private void FixedUpdate()
    {
        if (!IsInClash || !IsLocal || _controller == null) return;

        Rigidbody body = _controller.PhysicsBody;
        if (body == null || !body.isKinematic) return;

        float s = Mathf.Sin(_elapsed * _sineFrequency * Mathf.PI * 2f) * _sineAmplitude;
        body.MovePosition(_startPos + _sineAxis * s);
    }

    private void Update()
    {
        if (!IsInClash) return;
        _elapsed += Time.deltaTime;

        // Input: solo el jugador local presiona SPACE para su propio auto
        if (IsLocal && Input.GetKeyDown(KeyCode.Space))
        {
            Progress = Mathf.Clamp01(Progress + _pressIncrement);
            photonView.RPC(nameof(RPC_SyncProgress), RpcTarget.All, Progress);
            _hud?.PunchLocalBar();
        }

        // Resolución: componente del iniciador, ejecutado únicamente en el MasterClient.
        // El MC tiene todos los Progress sincronizados via RPC_SyncProgress y es la
        // autoridad canónica sobre quién muere. Si el MC se desconecta durante el clash,
        // Photon elige un nuevo MC cuyo _elapsed ya está corriendo → resuelve con los
        // datos sincronizados en ese momento.
        if (!_isInitiator || !PhotonNetwork.IsMasterClient) return;

        bool filledBar = Progress >= 1f || (_opponent != null && _opponent.Progress >= 1f);
        bool timerDone = _elapsed >= _duration;

        if (filledBar || timerDone)
            Resolve();
    }

    private void Resolve()
    {
        if (_resolved) return;
        _resolved = true;

        // Save opponent BEFORE any RPC — PUN may deliver local RPCs synchronously,
        // which would clear _opponent inside RPC_EndClash before we can use it.
        var opp = _opponent;

        bool localWins = opp == null || Progress >= opp.Progress;
        int loserViewId = localWins
            ? (opp != null ? opp.photonView.ViewID : -1)
            : photonView.ViewID;

        // End clash on both cars
        photonView.RPC(nameof(RPC_EndClash), RpcTarget.All);
        if (opp != null)
            opp.photonView.RPC(nameof(RPC_EndClash), RpcTarget.All);

        // Destroy loser — o consumir su escudo si lo tiene activo.
        // Resolve() ya corre en el MasterClient, así que este chequeo es canónico.
        if (loserViewId > 0)
        {
            PhotonView loserPV = PhotonView.Find(loserViewId);
            if (loserPV != null)
            {
                CarRamDestroy ramDestroy = loserPV.GetComponent<CarRamDestroy>();
                if (ramDestroy != null && !ramDestroy.IsInvincible)
                {
                    var loserEffects = loserPV.GetComponent<PowerUpEffects>();
                    if (loserEffects != null && loserEffects.ShieldActive)
                    {
                        // Escudo bloquea la muerte del clash
                        int winnerViewId = localWins
                            ? photonView.ViewID
                            : (opp != null ? opp.photonView.ViewID : -1);
                        int winnerActor = winnerViewId > 0
                            ? (PhotonView.Find(winnerViewId)?.Owner?.ActorNumber ?? -1)
                            : -1;
                        loserPV.RPC("RPC_ConsumeShield", RpcTarget.All, winnerActor, winnerViewId);
                    }
                    else
                    {
                        loserPV.RPC(nameof(CarRamDestroy.RPC_RamDestroyed), RpcTarget.All);
                    }
                }
            }
        }
    }
}
