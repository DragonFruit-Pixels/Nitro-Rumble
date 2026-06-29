using Photon.Pun;
using UnityEngine;

public class CarController : MonoBehaviourPun
{
    [Header("Physics References")]
    [SerializeField] private Rigidbody _sphere;
    [SerializeField] private Transform _container;
    [SerializeField] private float     _containerOffset = 0.65f;

    [Header("Movement")]
    [SerializeField] private float _accelerationForce = 35f;
    [SerializeField] private float _idleVelocityDamping = 8f;
    [SerializeField] private float _idleStopSpeed = 0.15f;

    [Header("Ground Detection")]
    [SerializeField] private float     _groundRayDistance = 0.7f;
    [SerializeField] private LayerMask _groundMask;

    public float Speed         { get; private set; }
    public float LinearSpeed   { get; private set; }  // normalizado -1 a 1
    public float Acceleration  { get; private set; }  // LinearSpeed suavizado
    public float ThrottleInput { get; private set; }
    public float SteerInput    { get; private set; }
    public bool  IsGrounded    { get; private set; }
    public bool  IsDrifting    { get; private set; }
    public float MaxSpeed      => 20f;
    public Transform VisualTransform => _container;
    public Rigidbody PhysicsBody => _sphere;
    public bool CanMove
    {
        get => _canMove;
        set => SetCanMove(value);
    }

    /// <summary>
    /// Inyecta estado de red en autos remotos para que CarVisuals los anime correctamente.
    /// Llamado por CarNetworkSync en jugadores no locales.
    /// </summary>
    public void SetRemoteState(float linearSpeed, float steerInput)
    {
        LinearSpeed  = linearSpeed;
        SteerInput   = steerInput;
        Acceleration = linearSpeed;
    }

    private float _linearSpeed;
    private float _angularSpeed;
    private float _acceleration;
    private float _driftSteeringMultiplier = 1f;
    private float _driftAccelerationMultiplier = 1f;
    private bool _canMove;

    private Vector3 _groundNormal = Vector3.up;
    private bool    _wasGrounded;
    private Vector3 _prevContainerPos;
    private const float DriftThreshold = 4f;

    private bool IsLocalAuthority =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    #region MonoBehaviour

    private void Awake()
    {
        if (_sphere != null)
        {
            _sphere.interpolation = RigidbodyInterpolation.Interpolate;
            ApplyMovementLock();
        }

        EnsureRuntimeComponents();
    }

    private void Start()
    {
        _prevContainerPos = _container != null ? _container.position : transform.position;
    }

    private void Update()
    {
        if (!CanMove) return;
        if (!IsLocalAuthority) return;
        ThrottleInput = Input.GetAxis("Vertical");
        SteerInput    = Input.GetAxis("Horizontal");
    }

    private void FixedUpdate()
    {
        _wasGrounded = IsGrounded;
        CheckGround();

        if (!CanMove && IsLocalAuthority)
        {
            ResetMovementState();
            UpdateSpeed();
            DetectDrift();
            return;
        }

        HandleInput();
        ApplySteering();
        AlignToGround();
        SyncPhysicsBodyRotation();
        ApplyIdleDamping();
        UpdateSpeed();
        DetectDrift();
    }

    private void LateUpdate()
    {
        if (_sphere == null || _container == null) return;
        _container.position = _sphere.transform.position - Vector3.up * _containerOffset;
    }

    #endregion

    #region Core Physics

    public void SetDriftSteeringMultiplier(float multiplier)
    {
        _driftSteeringMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void SetDriftHandling(float steeringMultiplier, float accelerationMultiplier)
    {
        _driftSteeringMultiplier = Mathf.Max(0.1f, steeringMultiplier);
        _driftAccelerationMultiplier = Mathf.Clamp01(accelerationMultiplier);
    }

    public void ResetDriftHandling()
    {
        _driftSteeringMultiplier = 1f;
        _driftAccelerationMultiplier = 1f;
    }

    public void ApplyDriftVelocityDrag(float forwardDrag, float lateralDrag)
    {
        if (_sphere == null || _container == null || !IsGrounded)
            return;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(_sphere.velocity, _groundNormal);
        Vector3 forwardVelocity = Vector3.Project(planarVelocity, _container.forward);
        Vector3 lateralVelocity = planarVelocity - forwardVelocity;

        forwardVelocity = Vector3.Lerp(forwardVelocity, Vector3.zero, Time.fixedDeltaTime * forwardDrag);
        lateralVelocity = Vector3.Lerp(lateralVelocity, Vector3.zero, Time.fixedDeltaTime * lateralDrag);

        Vector3 verticalVelocity = _sphere.velocity - planarVelocity;
        _sphere.velocity = verticalVelocity + forwardVelocity + lateralVelocity;
    }

    private void EnsureRuntimeComponents() { }

    private void SetCanMove(bool value)
    {
        if (_canMove == value)
            return;

        _canMove = value;
        ResetMovementState();
        ApplyMovementLock();
    }

    private void ApplyMovementLock()
    {
        if (_sphere == null || !IsLocalAuthority)
            return;

        _sphere.isKinematic = !CanMove;
    }

    private void ResetMovementState()
    {
        ThrottleInput = 0f;
        SteerInput = 0f;
        _linearSpeed = 0f;
        _angularSpeed = 0f;
        _acceleration = 0f;
        ResetDriftHandling();
        ResetPhysicsVelocity();
    }

    private void ResetPhysicsVelocity()
    {
        if (_sphere == null)
            return;

        _sphere.velocity = Vector3.zero;
        _sphere.angularVelocity = Vector3.zero;
    }

    private void CheckGround()
    {
        IsGrounded = Physics.Raycast(
            _sphere.position,
            Vector3.down,
            out RaycastHit hit,
            _groundRayDistance,
            _groundMask
        );

        if (IsGrounded)
            _groundNormal = Vector3.Slerp(_groundNormal, hit.normal, 10f * Time.fixedDeltaTime);

        if (IsGrounded && !_wasGrounded)
            ThrottleInput = 0f;
    }

    private void HandleInput()
    {
        if (!CanMove) return;

        _sphere.AddForce(_container.forward * _linearSpeed * _accelerationForce * _driftAccelerationMultiplier, ForceMode.Acceleration);

        if (!IsGrounded) return;

        float direction = Mathf.Sign(_linearSpeed);
        if (direction == 0f)
            direction = Mathf.Abs(ThrottleInput) > 0.1f ? Mathf.Sign(ThrottleInput) : 1f;

        float steeringGrip = Mathf.Clamp(Mathf.Abs(_linearSpeed), 0.2f, 1.0f);
        float targetAngular = SteerInput * steeringGrip * 4f * direction * _driftSteeringMultiplier;
        _angularSpeed = Mathf.Lerp(_angularSpeed, targetAngular, Time.fixedDeltaTime * 4f);

        float targetSpeed = ThrottleInput;
        if (targetSpeed < 0f && _linearSpeed > 0.01f)
            _linearSpeed = Mathf.Lerp(_linearSpeed, 0f,               Time.fixedDeltaTime * 8f);
        else if (targetSpeed < 0f)
            _linearSpeed = Mathf.Lerp(_linearSpeed, targetSpeed * 0.5f, Time.fixedDeltaTime * 2f);
        else
            _linearSpeed = Mathf.Lerp(_linearSpeed, targetSpeed,       Time.fixedDeltaTime * 6f);
    }

    private void ApplySteering()
    {
        _container.Rotate(Vector3.up, _angularSpeed * Mathf.Rad2Deg * Time.fixedDeltaTime);
    }

    private void AlignToGround()
    {
        if (!IsGrounded) return;
        if (Vector3.Dot(_groundNormal, _container.up) > 0.5f)
        {
            Quaternion target = Quaternion.FromToRotation(_container.up, _groundNormal) * _container.rotation;
            _container.rotation = Quaternion.Lerp(_container.rotation, target, 0.2f);
        }
    }

    private void SyncPhysicsBodyRotation()
    {
        if (_sphere == null || _container == null)
            return;

        _sphere.MoveRotation(_container.rotation);
    }

    private void ApplyIdleDamping()
    {
        if (_sphere == null || !IsGrounded || !CanMove)
            return;

        if (Mathf.Abs(ThrottleInput) > 0.05f)
            return;

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(_sphere.velocity, _groundNormal);
        Vector3 dampedHorizontal = Vector3.Lerp(
            horizontalVelocity,
            Vector3.zero,
            Time.fixedDeltaTime * _idleVelocityDamping
        );

        Vector3 verticalVelocity = _sphere.velocity - horizontalVelocity;
        _sphere.velocity = verticalVelocity + dampedHorizontal;

        if (dampedHorizontal.magnitude < _idleStopSpeed)
            _sphere.velocity = verticalVelocity;
    }

    private void UpdateSpeed()
    {
        Speed = Vector3.Dot(_sphere.velocity, _container.forward);
        LinearSpeed = _linearSpeed;
        float angularBonus = Mathf.Abs(_sphere.angularVelocity.magnitude * _linearSpeed) / 100f;
        _acceleration = Mathf.Lerp(_acceleration, _linearSpeed + angularBonus, Time.fixedDeltaTime * 1f);
        Acceleration  = _acceleration;
    }

    private void DetectDrift()
    {
        Vector3 lateral = _sphere.velocity - Vector3.Project(_sphere.velocity, _container.forward);
        IsDrifting = lateral.magnitude > DriftThreshold && IsGrounded;
    }

    #endregion
}
