using System.Collections;
using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarController _target;

    [Header("Tuning")]
    [Tooltip("Asset con todos los valores de cámara (y manejo). Si queda vacío se carga Assets/Resources/CarTuning.asset.")]
    [SerializeField] private CarTuningSO _tuning;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask _clipMask = ~0;

    private Camera _cam;
    private float  _currentDistance;
    private float  _currentTilt;
    private float  _shakeIntensity;
    private Vector3 _shakeOffset;
    private float  _turboBurst;

    #region MonoBehaviour

    private void Awake()
    {
        EnsureTuning();

        _cam = GetComponent<Camera>();

        int minimapLayer = LayerMask.NameToLayer("MinimapIcon");
        if (minimapLayer >= 0 && _cam != null)
            _cam.cullingMask &= ~(1 << minimapLayer);

        // _clipMask viene como ~0 (todas las layers), lo que incluye "Ignore Raycast" (layer 2).
        // La excluimos explícitamente para que el ceiling configurado en esa layer no afecte la cámara.
        _clipMask &= ~(1 << 2);
    }

    // Garantiza que _tuning nunca sea null (asignado → Resources → instancia por defecto).
    private void EnsureTuning()
    {
        if (_tuning == null) _tuning = Resources.Load<CarTuningSO>("CarTuning");
        if (_tuning == null) _tuning = ScriptableObject.CreateInstance<CarTuningSO>();
    }

    private void Start()
    {
        _currentDistance = _tuning.MinDistance;
        if (_cam != null) _cam.fieldOfView = _tuning.BaseFov;
        if (_target != null) SnapToTarget();
    }

    private void LateUpdate()
    {
        if (_target == null) return;
        UpdateDistance();
        UpdatePosition();
        UpdateFov();
        UpdateTilt();
        UpdateShake();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.K)) Shake(1.2f);
#endif
    }

    #endregion

    #region Camera Logic

    private void UpdateDistance()
    {
        float normSpeed  = Mathf.Clamp01(Mathf.Abs(_target.Speed) / _target.MaxSpeed);
        float targetDist = Mathf.Lerp(_tuning.MinDistance, _tuning.MaxDistance, normSpeed);
        _currentDistance = Mathf.Lerp(_currentDistance, targetDist, Time.deltaTime * _tuning.ZoomSpeed);
    }

    private void UpdatePosition()
    {
        Transform car = _target.VisualTransform;

        Vector3 origin    = car.position + Vector3.up * _tuning.HeightOffset;
        Vector3 direction = (-car.forward + Vector3.up * 0.15f).normalized;
        float   wantedDist = _currentDistance;

        if (Physics.SphereCast(origin, _tuning.ClipPadding, direction, out RaycastHit hit,
                               wantedDist, _clipMask, QueryTriggerInteraction.Ignore))
            wantedDist = Mathf.Max(hit.distance - _tuning.ClipPadding, _tuning.ClipPadding);

        Vector3 targetPos = origin + direction * wantedDist + _shakeOffset;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _tuning.FollowSpeed);

        Vector3 lookTarget = car.position + car.forward * _tuning.LookAheadOffset + Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _tuning.RotationSpeed);
    }

    private void UpdateFov()
    {
        if (_cam == null) return;
        float normSpeed = Mathf.Clamp01(Mathf.Abs(_target.Speed) / _target.MaxSpeed);
        float targetFov = Mathf.Lerp(_tuning.BaseFov, _tuning.MaxFov, normSpeed) + _turboBurst;
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * _tuning.FovSpeed);
        _turboBurst = Mathf.MoveTowards(_turboBurst, 0f, Time.deltaTime * 30f);
    }

    private void UpdateTilt()
    {
        float targetTilt = -_target.SteerInput * _tuning.MaxTilt;
        _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, Time.deltaTime * _tuning.TiltSpeed);

        Vector3 euler = transform.eulerAngles;
        euler.z = _currentTilt;
        transform.eulerAngles = euler;
    }

    private void UpdateShake()
    {
        if (_shakeIntensity <= 0f) { _shakeOffset = Vector3.zero; return; }

        _shakeOffset = Random.insideUnitSphere * _shakeIntensity;
        _shakeIntensity = Mathf.MoveTowards(_shakeIntensity, 0f, Time.deltaTime * _tuning.ShakeDecay);
    }

    private void SnapToTarget()
    {
        Transform car = _target.VisualTransform;
        transform.position = car.position - car.forward * _tuning.MinDistance + Vector3.up * _tuning.HeightOffset;
        transform.LookAt(car.position + Vector3.up);
    }

    #endregion

    public void SetTarget(CarController target)
    {
        _target = target;
        SnapToTarget();
    }

    public void Shake(float intensity)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
    }

    // Instant FOV spike that decays back — call on turbo activation
    public void TurboFovPunch(float extra = 15f)
    {
        _turboBurst = Mathf.Max(_turboBurst, extra);
    }
}
