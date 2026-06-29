using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class CarArcadeCollision : MonoBehaviourPun
{
    [Header("References")]
    [SerializeField] private CarController _controller;
    [SerializeField] private Rigidbody _sphere;
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private CarCollisionTuning _tuning;

    [Header("Impact")]
    [SerializeField] private float _minImpactSpeed = 1.5f;
    [SerializeField] private float _impulseStrength = 7f;
    [SerializeField] private float _sideImpulseMultiplier = 0.75f;
    [SerializeField] private float _separationImpulse = 2f;
    [SerializeField] private float _stayImpulseMultiplier = 0.18f;
    [SerializeField] private float _maxImpulse = 12f;

    [Header("Camera Shake")]
    [SerializeField] private CarCamera _carCamera;
    [SerializeField] private float _shakeIntensity = 0.8f;

    private PowerUpEffects _effects;
    private CarDriftBoost  _driftBoost;
    private CarClashMinigame _clashMinigame;

    [Header("Impact Sparks")]
    [SerializeField] private CarImpactSparks _sparks;

    public Rigidbody PhysicsBody => _sphere;

    private bool IsLocalAuthority =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<CarController>();

        if (_sphere == null && _controller != null)
            _sphere = _controller.PhysicsBody;

        if (_visualRoot == null && _controller != null)
            _visualRoot = _controller.VisualTransform;

        if (_carCamera == null)
            _carCamera = FindObjectOfType<CarCamera>();

        if (_sparks == null)
            _sparks = GetComponent<CarImpactSparks>();

        if (_effects == null)
            _effects = GetComponent<PowerUpEffects>();

        if (_driftBoost == null)
            _driftBoost = GetComponent<CarDriftBoost>();

        if (_clashMinigame == null)
            _clashMinigame = GetComponent<CarClashMinigame>();

        if (_visualRoot == null)
            _visualRoot = transform;
    }

    public void HandleSideTrigger(CarCollisionSide ownSensor, Collider other, bool isStay)
    {
        if (ownSensor == null || other == null)
            return;

        CarArcadeCollision otherCar = other.GetComponentInParent<CarArcadeCollision>();

        if (otherCar == null)
            return;

        if (otherCar == this)
            return;

        if (!IsLocalAuthority)
            return;

        if (_sphere == null)
            return;

        // Autos que terminaron la carrera no interactúan con nadie.
        var otherRacer = otherCar.GetComponent<Racer>();
        if (otherRacer != null && otherRacer.IsFinished) return;
        var myRacer = GetComponent<Racer>();
        if (myRacer != null && myRacer.IsFinished) return;

        Rigidbody otherBody = otherCar.PhysicsBody;
        Vector3 ownVelocity = _sphere.velocity;
        Vector3 otherVelocity = GetOtherVelocity(otherCar, otherBody);
        Vector3 relativeVelocity = ownVelocity - otherVelocity;

        Vector3 sideNormal = GetSideNormal(ownSensor.Side);
        float closingSpeed = Vector3.Dot(relativeVelocity, sideNormal);
        float minImpactSpeed = _tuning != null ? _tuning.MinImpactSpeed : _minImpactSpeed;
        if (closingSpeed < minImpactSpeed && !isStay)
            return;

        float impulseMagnitude = GetImpulseMagnitude(closingSpeed, ownSensor.Side, isStay);
        Vector3 impulse = -sideNormal * impulseMagnitude;
        _sphere.AddForce(impulse, ForceMode.VelocityChange);

        float shakeAmount = _shakeIntensity * Mathf.Clamp01(impulseMagnitude / _maxImpulse);
        if (_carCamera != null)
            _carCamera.Shake(shakeAmount);

        // Sparks at contact point
        Vector3 contactPoint = other.ClosestPoint(ownSensor.transform.position);
        _sparks?.Spawn(contactPoint, sideNormal);

        // Nitro ram: mi FRONT intersecta el BACK sensor del otro.
        // El MasterClient valida escudo e invencibilidad y decide el outcome.
        if (ownSensor.Side == CarImpactSide.Front && _effects != null && _effects.IsTurboActive)
        {
            if (IsHittingBackSensor(ownSensor, otherCar))
            {
                var ramDestroy = otherCar.GetComponent<CarRamDestroy>();
                if (ramDestroy != null && !ramDestroy.IsInvincible)
                {
                    if (otherCar.photonView != null && otherCar.photonView.ViewID != 0)
                        photonView.RPC(nameof(RPC_RequestRamResolve), RpcTarget.MasterClient,
                            otherCar.photonView.ViewID, photonView.ViewID, PhotonNetwork.ServerTimestamp);
                    else
                        ramDestroy.RPC_RamDestroyed(); // offline / sin red
                }
            }
        }

        // Drift boost clash: tengo boost activo + golpeo el lateral del oponente.
        // Si el oponente tiene escudo, lo consume; si no, inicia el minijuego.
        if (!isStay
            && _driftBoost != null && _driftBoost.IsBoosting
            && _clashMinigame != null && !_clashMinigame.IsInClash
            && IsHittingLateralSensor(ownSensor, otherCar))
        {
            var opponentClash   = otherCar.GetComponent<CarClashMinigame>();
            var opponentEffects = otherCar.GetComponent<PowerUpEffects>();

            if (opponentClash != null && !opponentClash.IsInClash)
            {
                if (opponentEffects != null && opponentEffects.ShieldActive)
                    // Escudo bloquea el inicio del clash — consumirlo
                    otherCar.photonView.RPC("RPC_ConsumeShield", RpcTarget.All,
                        photonView.Owner?.ActorNumber ?? -1, photonView.ViewID);
                else
                    _clashMinigame.TriggerClash(opponentClash);
            }
        }
    }

    private Vector3 GetSideNormal(CarImpactSide side)
    {
        Transform basis = _visualRoot != null ? _visualRoot : transform;

        switch (side)
        {
            case CarImpactSide.Front:
                return basis.forward;
            case CarImpactSide.Back:
                return -basis.forward;
            case CarImpactSide.Left:
                return -basis.right;
            case CarImpactSide.Right:
                return basis.right;
            default:
                return basis.forward;
        }
    }

    private Vector3 GetOtherVelocity(CarArcadeCollision otherCar, Rigidbody otherBody)
    {
        if (otherBody == null)
            return Vector3.zero;

        if (!otherBody.isKinematic)
            return otherBody.velocity;

        CarNetworkSync sync = otherCar.GetComponent<CarNetworkSync>();
        return sync != null ? sync.RemoteVelocity : Vector3.zero;
    }

    // Devuelve true si mi sensor actual se solapa con el sensor Left o Right del otro auto.
    private bool IsHittingLateralSensor(CarCollisionSide mySensor, CarArcadeCollision other)
    {
        var myBox = mySensor.GetComponent<BoxCollider>();
        if (myBox == null) return false;

        foreach (var side in other.GetComponentsInChildren<CarCollisionSide>())
        {
            if (side.Side != CarImpactSide.Left && side.Side != CarImpactSide.Right) continue;
            var lateralBox = side.GetComponent<BoxCollider>();
            if (lateralBox != null && myBox.bounds.Intersects(lateralBox.bounds))
                return true;
        }
        return false;
    }

    // Devuelve true si mi sensor FRONT se solapa con el sensor BACK del otro auto.
    private bool IsHittingBackSensor(CarCollisionSide myFrontSensor, CarArcadeCollision other)
    {
        var frontBox = myFrontSensor.GetComponent<BoxCollider>();
        if (frontBox == null) return false;

        foreach (var side in other.GetComponentsInChildren<CarCollisionSide>())
        {
            if (side.Side != CarImpactSide.Back) continue;
            var backBox = side.GetComponent<BoxCollider>();
            if (backBox != null && frontBox.bounds.Intersects(backBox.bounds))
                return true;
        }
        return false;
    }

    private CarImpactSide GuessOtherSide(CarArcadeCollision otherCar, Vector3 worldNormalTowardOther)
    {
        Transform otherBasis = otherCar._visualRoot != null ? otherCar._visualRoot : otherCar.transform;

        float front = Vector3.Dot(worldNormalTowardOther, otherBasis.forward);
        float right = Vector3.Dot(worldNormalTowardOther, otherBasis.right);

        if (Mathf.Abs(front) > Mathf.Abs(right))
            return front > 0f ? CarImpactSide.Front : CarImpactSide.Back;

        return right > 0f ? CarImpactSide.Right : CarImpactSide.Left;
    }

    private static bool IsSideHit(CarImpactSide side)
    {
        return side == CarImpactSide.Left || side == CarImpactSide.Right;
    }

    // Recibido solo por el MasterClient. El hitTimestamp (ServerTimestamp del atacante)
    // se compara contra el momento en que el objetivo activó su escudo: si el escudo
    // fue activado ANTES del golpe, bloquea; si fue activado después, no cuenta.
    [PunRPC]
    public void RPC_RequestRamResolve(int targetViewId, int attackerViewId, int hitTimestamp)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        PhotonView targetPV   = PhotonView.Find(targetViewId);
        PhotonView attackerPV = PhotonView.Find(attackerViewId);
        if (targetPV == null) return;

        var ramDestroy = targetPV.GetComponent<CarRamDestroy>();
        if (ramDestroy == null || ramDestroy.IsInvincible) return;

        var targetEffects = targetPV.GetComponent<PowerUpEffects>();
        // El escudo bloquea solo si estaba activo Y fue activado antes del golpe.
        // La resta unchecked maneja el wrap-around del int de ServerTimestamp.
        bool shieldBlocksHit = targetEffects != null
            && targetEffects.ShieldActive
            && unchecked(hitTimestamp - targetEffects.ShieldTimestamp) >= 0;

        if (shieldBlocksHit)
            targetPV.RPC("RPC_ConsumeShield", RpcTarget.All,
                attackerPV?.Owner?.ActorNumber ?? -1, attackerViewId);
        else
            targetPV.RPC(nameof(CarRamDestroy.RPC_RamDestroyed), RpcTarget.All);
    }

    private float GetImpulseMagnitude(float closingSpeed, CarImpactSide side, bool isStay)
    {
        if (_tuning != null)
            return _tuning.GetImpulseMagnitude(closingSpeed, side, isStay);

        float impulseMagnitude = Mathf.Clamp(
            closingSpeed * _impulseStrength + _separationImpulse,
            _separationImpulse,
            _maxImpulse
        );

        if (IsSideHit(side))
            impulseMagnitude *= _sideImpulseMultiplier;

        if (isStay)
            impulseMagnitude *= _stayImpulseMultiplier;

        return impulseMagnitude;
    }

}
