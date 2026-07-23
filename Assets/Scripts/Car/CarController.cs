using Photon.Pun;
using UnityEngine;

public class CarController : MonoBehaviourPun
{
    [Header("Physics References")]
    [SerializeField] private Rigidbody _sphere;
    [SerializeField] private Transform _container;
    [SerializeField] private float     _containerOffset = 0.65f;

    [Header("Tuning")]
    [Tooltip("Asset con todos los valores de manejo (y cámara). Si queda vacío se carga Assets/Resources/CarTuning.asset.")]
    [SerializeField] private CarTuningSO _tuning;

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
    public float MaxSpeed      => _tuning.MaxSpeed;
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
    private CarBotDriver _botDriver;

    private Vector3 _groundNormal = Vector3.up;
    private bool    _wasGrounded;
    private Vector3 _prevContainerPos;
    private const float DriftThreshold = 4f;

    private bool IsLocalAuthority =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    #region MonoBehaviour

    private void Awake()
    {
        EnsureTuning();

        if (_sphere != null)
        {
            _sphere.interpolation = RigidbodyInterpolation.Interpolate;
            ApplyMovementLock();
        }

        EnsureRuntimeComponents();
        _botDriver = GetComponent<CarBotDriver>();
    }

    // Garantiza que _tuning nunca sea null: usa el asignado, si no el de Resources, y como último
    // recurso una instancia con los valores por defecto del SO. Así el código puede leer _tuning.* sin chequear.
    private void EnsureTuning()
    {
        if (_tuning == null) _tuning = Resources.Load<CarTuningSO>("CarTuning");
        if (_tuning == null) _tuning = ScriptableObject.CreateInstance<CarTuningSO>();
    }

    private void Start()
    {
        _prevContainerPos = _container != null ? _container.position : transform.position;
    }

    private void Update()
    {
        if (!CanMove) return;
        if (!IsLocalAuthority) return;

        if (_botDriver != null && _botDriver.IsBot)
        {
            ThrottleInput = _botDriver.ThrottleInput;
            SteerInput    = _botDriver.SteerInput;
        }
        else
        {
            ThrottleInput = Input.GetAxis("Vertical");
            SteerInput    = Input.GetAxis("Horizontal");
        }
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
        // Sin control de motor en el aire: se conserva la inercia (no se empuja ni frena volando).
        if (!CanMove || !IsGrounded) return;

        float forwardSpeed = Vector3.Dot(_sphere.velocity, _container.forward);
        float throttle     = ThrottleInput;

        // ── Dirección ──────────────────────────────────────────────────────────────
        // El agarre de giro crece con la velocidad (mínimo 0.2 para poder maniobrar casi detenido).
        float normSpeed    = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / _tuning.MaxSpeed);
        float steeringGrip = Mathf.Clamp(normSpeed, 0.2f, 1f);
        float direction    = forwardSpeed < -0.1f ? -1f : 1f; // en reversa, el volante se invierte
        float targetAngular = SteerInput * steeringGrip * _tuning.MaxTurnRate * direction * _driftSteeringMultiplier;
        _angularSpeed = Mathf.Lerp(_angularSpeed, targetAngular, Time.fixedDeltaTime * _tuning.SteerResponse);

        // ── Acelerar / Frenar / Costear ──────────────────────────────────────────────
        if (throttle > 0.05f)
        {
            // Fuerza proporcional al acelerador, atenuada al acercarse a la velocidad máxima.
            // Así el tope es un equilibrio natural (fuerza→0) en vez de un corte brusco.
            float speedRatio = Mathf.Clamp01(forwardSpeed / _tuning.MaxSpeed);
            float accel = _tuning.AccelerationForce * throttle * (1f - speedRatio) * _driftAccelerationMultiplier;
            _sphere.AddForce(_container.forward * accel, ForceMode.Acceleration);
        }
        else if (throttle < -0.05f)
        {
            if (forwardSpeed > 0.5f)
            {
                // Freno activo mientras se avanza.
                _sphere.AddForce(-_container.forward * _tuning.BrakeForce, ForceMode.Acceleration);
            }
            else
            {
                // Marcha atrás, con su propio tope de velocidad.
                float reverseRatio = Mathf.Clamp01(-forwardSpeed / _tuning.ReverseMaxSpeed);
                float accel = _tuning.AccelerationForce * throttle * (1f - reverseRatio);
                _sphere.AddForce(_container.forward * accel, ForceMode.Acceleration);
            }
        }
        else if (Mathf.Abs(forwardSpeed) > 0.1f)
        {
            // Coast: resistencia de rodadura suave. El auto desacelera de a poco conservando inercia,
            // sin frenar en seco. Se limita para no invertir la velocidad en un solo frame.
            float decel = Mathf.Min(_tuning.CoastDecel, Mathf.Abs(forwardSpeed) / Time.fixedDeltaTime);
            _sphere.AddForce(-Mathf.Sign(forwardSpeed) * _container.forward * decel, ForceMode.Acceleration);
        }
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

        // La desaceleración al soltar el gas ya la maneja el "coast" (resistencia de rodadura) en HandleInput.
        // Acá solo cortamos el deslizamiento residual cuando el auto ya está casi detenido, para que no
        // quede reptando eternamente. NO frenamos activamente a velocidades normales (eso era el bug).
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(_sphere.velocity, _groundNormal);
        if (horizontalVelocity.magnitude < _tuning.IdleStopSpeed)
            _sphere.velocity -= horizontalVelocity;
    }

    private void UpdateSpeed()
    {
        Speed = Vector3.Dot(_sphere.velocity, _container.forward);

        // LinearSpeed normalizado (-1..1) ahora deriva de la velocidad REAL del auto, no de un
        // seguidor del acelerador. Lo consumen CarVisuals (ruedas/lean), CarStretchSquash y CarNetworkSync.
        _linearSpeed = Mathf.Clamp(Speed / _tuning.MaxSpeed, -1f, 1f);
        LinearSpeed  = _linearSpeed;

        _acceleration = Mathf.Lerp(_acceleration, _linearSpeed, Time.fixedDeltaTime * 3f);
        Acceleration  = _acceleration;
    }

    private void DetectDrift()
    {
        Vector3 lateral = _sphere.velocity - Vector3.Project(_sphere.velocity, _container.forward);
        IsDrifting = lateral.magnitude > DriftThreshold && IsGrounded;
    }

    #endregion
}
