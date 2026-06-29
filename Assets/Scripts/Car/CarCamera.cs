using System.Collections;
using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarController _target;

    [Header("Follow")]
    [SerializeField] private float _followSpeed  = 4f;
    [SerializeField] private float _heightOffset = 3f;

    [Header("Dynamic Zoom")]
    [SerializeField] private float _minDistance = 8f;
    [SerializeField] private float _maxDistance = 16f;
    [SerializeField] private float _zoomSpeed   = 2f;

    [Header("Look At")]
    [SerializeField] private float _lookAheadOffset = 1.5f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float     _clipPadding = 0.3f;
    [SerializeField] private LayerMask _clipMask    = ~0;

    [Header("Dynamic FOV")]
    [SerializeField] private float _baseFov  = 60f;
    [SerializeField] private float _maxFov   = 75f;
    [SerializeField] private float _fovSpeed = 3f;

    [Header("Camera Tilt")]
    [SerializeField] private float _maxTilt  = 5f;
    [SerializeField] private float _tiltSpeed = 4f;

    [Header("Screen Shake")]
    [SerializeField] private float _shakeDecay = 3.5f;

    private Camera _cam;
    private float  _currentDistance;
    private float  _currentTilt;
    private float  _shakeIntensity;
    private Vector3 _shakeOffset;
    private float  _turboBurst;

    #region MonoBehaviour

    private void Awake()
    {
        _cam = GetComponent<Camera>();

        int minimapLayer = LayerMask.NameToLayer("MinimapIcon");
        if (minimapLayer >= 0 && _cam != null)
            _cam.cullingMask &= ~(1 << minimapLayer);

        // _clipMask viene como ~0 (todas las layers), lo que incluye "Ignore Raycast" (layer 2).
        // La excluimos explícitamente para que el ceiling configurado en esa layer no afecte la cámara.
        _clipMask &= ~(1 << 2);
    }

    private void Start()
    {
        _currentDistance = _minDistance;
        if (_cam != null) _cam.fieldOfView = _baseFov;
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
        float targetDist = Mathf.Lerp(_minDistance, _maxDistance, normSpeed);
        _currentDistance = Mathf.Lerp(_currentDistance, targetDist, Time.deltaTime * _zoomSpeed);
    }

    private void UpdatePosition()
    {
        Transform car = _target.VisualTransform;

        Vector3 origin    = car.position + Vector3.up * _heightOffset;
        Vector3 direction = (-car.forward + Vector3.up * 0.15f).normalized;
        float   wantedDist = _currentDistance;

        if (Physics.SphereCast(origin, _clipPadding, direction, out RaycastHit hit,
                               wantedDist, _clipMask, QueryTriggerInteraction.Ignore))
            wantedDist = Mathf.Max(hit.distance - _clipPadding, _clipPadding);

        Vector3 targetPos = origin + direction * wantedDist + _shakeOffset;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _followSpeed);
        transform.LookAt(car.position + car.forward * _lookAheadOffset + Vector3.up);
    }

    private void UpdateFov()
    {
        if (_cam == null) return;
        float normSpeed = Mathf.Clamp01(Mathf.Abs(_target.Speed) / _target.MaxSpeed);
        float targetFov = Mathf.Lerp(_baseFov, _maxFov, normSpeed) + _turboBurst;
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * _fovSpeed);
        _turboBurst = Mathf.MoveTowards(_turboBurst, 0f, Time.deltaTime * 30f);
    }

    private void UpdateTilt()
    {
        float targetTilt = -_target.SteerInput * _maxTilt;
        _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, Time.deltaTime * _tiltSpeed);

        Vector3 euler = transform.eulerAngles;
        euler.z = _currentTilt;
        transform.eulerAngles = euler;
    }

    private void UpdateShake()
    {
        if (_shakeIntensity <= 0f) { _shakeOffset = Vector3.zero; return; }

        _shakeOffset = Random.insideUnitSphere * _shakeIntensity;
        _shakeIntensity = Mathf.MoveTowards(_shakeIntensity, 0f, Time.deltaTime * _shakeDecay);
    }

    private void SnapToTarget()
    {
        Transform car = _target.VisualTransform;
        transform.position = car.position - car.forward * _minDistance + Vector3.up * _heightOffset;
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
