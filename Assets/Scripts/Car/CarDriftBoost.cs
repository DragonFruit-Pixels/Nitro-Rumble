using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class CarDriftBoost : MonoBehaviourPun
{
    [Header("References")]
    [SerializeField] private CarController _controller;

    [Header("Drift Input")]
    [SerializeField] private KeyCode _driftKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode _alternateDriftKey = KeyCode.RightShift;
    [SerializeField] private float _minDriftSpeed = 4f;
    [SerializeField] private float _minSteerInput = 0.15f;

    [Header("Drift Handling")]
    [SerializeField] private float _driftSteeringMultiplier = 1.45f;
    [SerializeField, Range(0f, 1f)] private float _driftAccelerationMultiplier = 0.6f;
    [SerializeField] private float _driftForwardDrag = 0.45f;
    [SerializeField] private float _driftLateralDrag = 0.15f;
    [SerializeField] private float _chargeTimeForMaxBoost = 2.2f;
    [SerializeField] private float _levelOneThreshold = 0.32f;
    [SerializeField] private float _levelTwoThreshold = 0.66f;
    [SerializeField] private float _levelThreeThreshold = 0.95f;

    [Header("Boost")]
    [SerializeField] private float _levelOneBoostDuration = 0.45f;
    [SerializeField] private float _levelTwoBoostDuration = 0.8f;
    [SerializeField] private float _levelThreeBoostDuration = 1.15f;
    [SerializeField] private float _levelOneBoostAcceleration = 25f;
    [SerializeField] private float _levelTwoBoostAcceleration = 38f;
    [SerializeField] private float _levelThreeBoostAcceleration = 52f;

    [Header("VFX Hooks")]
    [SerializeField] private ParticleSystem[] _rearSmokeParticles;
    [SerializeField] private Color _idleSmokeColor = new Color(0.75f, 0.75f, 0.75f, 0.55f);
    [SerializeField] private Color _levelOneSmokeColor = new Color(0.25f, 0.65f, 1f, 0.85f);
    [SerializeField] private Color _levelTwoSmokeColor = new Color(1f, 0.58f, 0.08f, 0.9f);
    [SerializeField] private Color _levelThreeSmokeColor = new Color(1f, 0.18f, 0.08f, 1f);
    [SerializeField] private float _minSmokeRate = 8f;
    [SerializeField] private float _maxSmokeRate = 42f;
    [SerializeField] private float _boostSmokeRate = 70f;

    [Header("Debug")]
    [SerializeField] private bool _debugBoostLogs = true;

    private bool _isDriftButtonHeld;
    private float _driftCharge01;
    private int _driftLevel;
    private bool _isBoosting;
    private int _boostLevel;
    private float _boostTimeRemaining;

    public bool IsDrifting => _isDriftButtonHeld;
    public float DriftCharge01 => _driftCharge01;
    public int DriftLevel => _driftLevel;
    public bool IsBoosting => _isBoosting;
    public int BoostLevel => _boostLevel;

    private bool IsLocalAuthority =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<CarController>();
    }

    private void Update()
    {
        if (IsLocalAuthority)
            UpdateLocalDriftState();

        UpdateSmokeVfx();
    }

    private void FixedUpdate()
    {
        if (!IsLocalAuthority || _controller == null)
            return;

        if (_isDriftButtonHeld)
            _controller.ApplyDriftVelocityDrag(_driftForwardDrag, _driftLateralDrag);

        if (_isBoosting)
            ApplyBoostForce();
    }

    public void SetRemoteState(bool isDrifting, float driftCharge01, int driftLevel, bool isBoosting, int boostLevel)
    {
        if (IsLocalAuthority)
            return;

        bool wasBoosting = _isBoosting;
        _isDriftButtonHeld = isDrifting;
        _driftCharge01 = Mathf.Clamp01(driftCharge01);
        _driftLevel = Mathf.Clamp(driftLevel, 0, 3);
        _isBoosting = isBoosting;
        _boostLevel = Mathf.Clamp(boostLevel, 0, 3);

        if (!wasBoosting && _isBoosting)
            LogBoostActivated("remote");
    }

    private void UpdateLocalDriftState()
    {
        if (_controller == null || !_controller.CanMove)
        {
            ResetDriftCharge();
            return;
        }

        bool driftPressed = Input.GetKey(_driftKey) || Input.GetKey(_alternateDriftKey);
        bool wasDriftButtonHeld = _isDriftButtonHeld;
        _isDriftButtonHeld = driftPressed;

        bool canCharge = driftPressed
            && _controller.IsGrounded
            && Mathf.Abs(_controller.Speed) >= _minDriftSpeed
            && Mathf.Abs(_controller.SteerInput) >= _minSteerInput;

        if (canCharge)
        {
            _isDriftButtonHeld = true;
            _driftCharge01 = Mathf.Clamp01(_driftCharge01 + Time.deltaTime / _chargeTimeForMaxBoost);
            _driftLevel = GetLevelFromCharge(_driftCharge01);
            _controller.SetDriftHandling(_driftSteeringMultiplier, _driftAccelerationMultiplier);
        }
        else
        {
            if (wasDriftButtonHeld && !driftPressed)
                TryStartBoost();

            _controller.ResetDriftHandling();

            if (!driftPressed && !_isBoosting)
                ResetDriftCharge();
        }

        TickBoostTimer();
    }

    private void TryStartBoost()
    {
        if (_driftLevel <= 0)
            return;

        _isBoosting = true;
        _boostLevel = _driftLevel;
        _boostTimeRemaining = GetBoostDuration(_boostLevel);
        LogBoostActivated("local");
        ResetDriftCharge();
    }

    private void TickBoostTimer()
    {
        if (!_isBoosting)
            return;

        _boostTimeRemaining -= Time.deltaTime;
        if (_boostTimeRemaining > 0f)
            return;

        _isBoosting = false;
        _boostLevel = 0;
        _boostTimeRemaining = 0f;
    }

    private void ApplyBoostForce()
    {
        Rigidbody body = _controller.PhysicsBody;
        Transform basis = _controller.VisualTransform;
        if (body == null || basis == null)
            return;

        body.AddForce(basis.forward * GetBoostAcceleration(_boostLevel), ForceMode.Acceleration);
    }

    private int GetLevelFromCharge(float charge)
    {
        if (charge >= _levelThreeThreshold) return 3;
        if (charge >= _levelTwoThreshold) return 2;
        if (charge >= _levelOneThreshold) return 1;
        return 0;
    }

    private float GetBoostDuration(int level)
    {
        switch (level)
        {
            case 3: return _levelThreeBoostDuration;
            case 2: return _levelTwoBoostDuration;
            case 1: return _levelOneBoostDuration;
            default: return 0f;
        }
    }

    private float GetBoostAcceleration(int level)
    {
        switch (level)
        {
            case 3: return _levelThreeBoostAcceleration;
            case 2: return _levelTwoBoostAcceleration;
            case 1: return _levelOneBoostAcceleration;
            default: return 0f;
        }
    }

    private void ResetDriftCharge()
    {
        _isDriftButtonHeld = false;
        _driftCharge01 = 0f;
        _driftLevel = 0;

        if (_controller != null)
            _controller.ResetDriftHandling();
    }

    private void UpdateSmokeVfx()
    {
        if (_rearSmokeParticles == null)
            return;

        bool shouldEmit = IsDrifting || _isBoosting;
        Color color = GetSmokeColor();
        float rate = _isBoosting
            ? _boostSmokeRate
            : Mathf.Lerp(_minSmokeRate, _maxSmokeRate, _driftCharge01);

        for (int i = 0; i < _rearSmokeParticles.Length; i++)
        {
            ParticleSystem particles = _rearSmokeParticles[i];
            if (particles == null)
                continue;

            ParticleSystem.MainModule main = particles.main;
            ParticleSystem.EmissionModule emission = particles.emission;
            main.startColor = color;
            emission.rateOverTime = shouldEmit ? rate : 0f;

            if (shouldEmit && !particles.isPlaying)
                particles.Play();
            else if (!shouldEmit && particles.isPlaying)
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private Color GetSmokeColor()
    {
        if (_isBoosting)
            return GetLevelColor(_boostLevel);

        if (_driftLevel == 0)
            return _idleSmokeColor;

        return GetLevelColor(_driftLevel);
    }

    private Color GetLevelColor(int level)
    {
        switch (level)
        {
            case 3: return _levelThreeSmokeColor;
            case 2: return _levelTwoSmokeColor;
            case 1: return _levelOneSmokeColor;
            default: return _idleSmokeColor;
        }
    }

    private void LogBoostActivated(string source)
    {
        if (!_debugBoostLogs)
            return;

        string nick = photonView != null && photonView.Owner != null
            ? photonView.Owner.NickName
            : "offline";

        int actor = photonView != null && photonView.Owner != null
            ? photonView.OwnerActorNr
            : -1;

        int viewId = photonView != null ? photonView.ViewID : 0;

        Debug.Log(
            $"[CarDriftBoost] turbo activated source={source} car={name} nick={nick} actor={actor} viewId={viewId} tier={_boostLevel} duration={_boostTimeRemaining:0.00}s acceleration={GetBoostAcceleration(_boostLevel):0.00}",
            this
        );
    }
}
