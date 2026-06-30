using System;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class CarNetworkSync : MonoBehaviourPun, IPunObservable
{
    [Header("References")]
    [SerializeField] private CarController   _controller;
    [SerializeField] private CarDriftBoost   _driftBoost;
    [SerializeField] private CarStretchSquash _stretchSquash;
    [SerializeField] private Rigidbody       _sphere;
    [SerializeField] private Transform       _container;

    [Header("Interpolation")]
    [SerializeField] private float _positionLerpSpeed  = 15f;
    [SerializeField] private float _rotationLerpSpeed  = 15f;
    [SerializeField] private float _maxExtrapolation   = 0.5f;

    // Estado recibido por red — variables continuas
    private Vector3    _netSpherePos;
    private Vector3    _netSphereVel;
    private Quaternion _netContainerRot;
    private float      _netLinearSpeed;
    private float      _netSteerInput;

    // Estado recibido por red — variables discretas (delta sync)
    private bool  _netIsDrifting;
    private float _netDriftCharge;
    private int   _netDriftLevel;
    private bool  _netIsBoosting;
    private int   _netBoostLevel;
    private float _netSquash;

    // Valores previos enviados — usados para detectar cambios (delta sync)
    private bool  _prevIsDrifting;
    private float _prevDriftCharge;
    private int   _prevDriftLevel;
    private bool  _prevIsBoosting;
    private int   _prevBoostLevel;
    private float _prevSquash;

    // Máscaras de bits para el dirty flag de estados discretos (byte = 8 bits, 3 libres para futuras variables)
    private const byte DIRTY_DRIFTING     = 1 << 0;
    private const byte DIRTY_BOOSTING     = 1 << 1;
    private const byte DIRTY_DRIFT_LEVEL  = 1 << 2;
    private const byte DIRTY_BOOST_LEVEL  = 1 << 3;
    private const byte DIRTY_DRIFT_CHARGE = 1 << 4;
    private const byte DIRTY_SQUASH       = 1 << 5;

    // Umbral de cambio del squash para marcar dirty (cuantizado a ~1/255, evita enviar cada frame).
    private const float SQUASH_THRESHOLD = 0.04f;

    private double _lastReceiveTime;

    public Vector3 RemoteVelocity => _netSphereVel;

    private bool IsLocal => PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private void Awake()
    {
        if (_container != null)
        {
            _netSpherePos    = _sphere != null ? _sphere.position : transform.position;
            _netContainerRot = _container.rotation;
        }

        ResolveDriftBoost();
        if (_stretchSquash == null) _stretchSquash = GetComponent<CarStretchSquash>();

        if (IsLocal) return;

        _controller.CanMove = false;
        if (_sphere != null) _sphere.isKinematic = true;
    }

    // Posición del Rigidbody: aplicada en FixedUpdate con MovePosition
    // para mantenerse sincronizado con el ciclo de físicas de Unity.
    private void FixedUpdate()
    {
        if (IsLocal) return;

        float lag = Mathf.Clamp((float)(PhotonNetwork.Time - _lastReceiveTime), 0f, _maxExtrapolation);
        Vector3 extrapolated = _netSpherePos + _netSphereVel * lag;

        Vector3 targetPos = Vector3.Lerp(
            _sphere.position, extrapolated, Time.fixedDeltaTime * _positionLerpSpeed);
        _sphere.MovePosition(targetPos);
    }

    // Estado visual y de inputs: no involucra el Rigidbody directamente.
    private void Update()
    {
        if (IsLocal) return;

        _container.rotation = Quaternion.Lerp(
            _container.rotation, _netContainerRot, Time.deltaTime * _rotationLerpSpeed);

        _controller.SetRemoteState(_netLinearSpeed, _netSteerInput);
        ResolveDriftBoost();
        if (_driftBoost != null)
        {
            _driftBoost.SetRemoteState(
                _netIsDrifting,
                _netDriftCharge,
                _netDriftLevel,
                _netIsBoosting,
                _netBoostLevel
            );
        }

        // Stretch & squash recibido por red (el remoto no lo calcula, lo aplica).
        if (_stretchSquash != null)
            _stretchSquash.SetRemoteSquash(_netSquash);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            ResolveDriftBoost();

            // ── Variables continuas: siempre se envían ────────────────────────
            stream.SendNext(_sphere.position);
            stream.SendNext(_sphere.velocity);
            stream.SendNext(_container.rotation);
            stream.SendNext(_controller.LinearSpeed);
            stream.SendNext(_controller.SteerInput);

            // ── Variables discretas: delta sync con bitmask ───────────────────
            bool  isDrifting  = _driftBoost != null && _driftBoost.IsDrifting;
            bool  isBoosting  = _driftBoost != null && _driftBoost.IsBoosting;
            int   driftLevel  = _driftBoost != null ? _driftBoost.DriftLevel   : 0;
            int   boostLevel  = _driftBoost != null ? _driftBoost.BoostLevel   : 0;
            float driftCharge = _driftBoost != null ? _driftBoost.DriftCharge01 : 0f;
            float squash      = _stretchSquash != null ? _stretchSquash.CurrentSquash : 0f;

            byte dirty = 0;
            if (isDrifting  != _prevIsDrifting)                         dirty |= DIRTY_DRIFTING;
            if (isBoosting  != _prevIsBoosting)                         dirty |= DIRTY_BOOSTING;
            if (driftLevel  != _prevDriftLevel)                         dirty |= DIRTY_DRIFT_LEVEL;
            if (boostLevel  != _prevBoostLevel)                         dirty |= DIRTY_BOOST_LEVEL;
            if (Mathf.Abs(driftCharge - _prevDriftCharge) > 0.01f)     dirty |= DIRTY_DRIFT_CHARGE;
            if (Mathf.Abs(squash - _prevSquash) > SQUASH_THRESHOLD)    dirty |= DIRTY_SQUASH;

            stream.SendNext(dirty);
            if ((dirty & DIRTY_DRIFTING)     != 0) stream.SendNext(isDrifting);
            if ((dirty & DIRTY_BOOSTING)     != 0) stream.SendNext(isBoosting);
            if ((dirty & DIRTY_DRIFT_LEVEL)  != 0) stream.SendNext(driftLevel);
            if ((dirty & DIRTY_BOOST_LEVEL)  != 0) stream.SendNext(boostLevel);
            if ((dirty & DIRTY_DRIFT_CHARGE) != 0) stream.SendNext(driftCharge);
            if ((dirty & DIRTY_SQUASH)       != 0) stream.SendNext(QuantizeSquash(squash));

            _prevIsDrifting  = isDrifting;
            _prevIsBoosting  = isBoosting;
            _prevDriftLevel  = driftLevel;
            _prevBoostLevel  = boostLevel;
            _prevDriftCharge = driftCharge;
            _prevSquash      = squash;
        }
        else
        {
            // ── Variables continuas ───────────────────────────────────────────
            _netSpherePos    = (Vector3)    stream.ReceiveNext();
            _netSphereVel    = (Vector3)    stream.ReceiveNext();
            _netContainerRot = (Quaternion) stream.ReceiveNext();
            _netLinearSpeed  = (float)      stream.ReceiveNext();
            _netSteerInput   = (float)      stream.ReceiveNext();

            // ── Variables discretas: leer solo las marcadas en el bitmask ─────
            byte dirty = Convert.ToByte(stream.ReceiveNext());
            if ((dirty & DIRTY_DRIFTING)     != 0) _netIsDrifting  = (bool)  stream.ReceiveNext();
            if ((dirty & DIRTY_BOOSTING)     != 0) _netIsBoosting  = (bool)  stream.ReceiveNext();
            if ((dirty & DIRTY_DRIFT_LEVEL)  != 0) _netDriftLevel  = (int)   stream.ReceiveNext();
            if ((dirty & DIRTY_BOOST_LEVEL)  != 0) _netBoostLevel  = (int)   stream.ReceiveNext();
            if ((dirty & DIRTY_DRIFT_CHARGE) != 0) _netDriftCharge = (float) stream.ReceiveNext();
            if ((dirty & DIRTY_SQUASH)       != 0) _netSquash      = DequantizeSquash(Convert.ToByte(stream.ReceiveNext()));

            _lastReceiveTime = PhotonNetwork.Time;
        }
    }

    private void ResolveDriftBoost()
    {
        if (_driftBoost == null)
            _driftBoost = GetComponent<CarDriftBoost>();
    }

    // Squash en [-1, 1] → byte [0, 255] para enviar 1 solo byte en lugar de un float (4 bytes).
    private static byte QuantizeSquash(float squash)
    {
        float t = Mathf.Clamp01(squash * 0.5f + 0.5f); // [-1,1] → [0,1]
        return (byte)Mathf.RoundToInt(t * 255f);
    }

    private static float DequantizeSquash(byte q)
    {
        return (q / 255f) * 2f - 1f; // [0,255] → [-1,1]
    }
}
