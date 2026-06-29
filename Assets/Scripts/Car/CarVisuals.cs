using UnityEngine;

public class CarVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarController _controller;

    [Header("Front Wheels")]
    [SerializeField] private Transform _wheelFrontLeft;
    [SerializeField] private Transform _wheelFrontRight;

    [Header("Rear Wheels")]
    [SerializeField] private Transform _wheelRearLeft;
    [SerializeField] private Transform _wheelRearRight;

    [Header("Wheel Settings")]
    [SerializeField] private float _wheelRotationSpeed = 200f;
    [SerializeField] private float _maxSteerAngle      = 30f;
    [SerializeField] private float _steerLerpSpeed     = 8f;

    [Header("Body Lean")]
    [SerializeField] private Transform _bodyMesh;
    [SerializeField] private float _maxLeanAngle  = 5f;
    [SerializeField] private float _leanLerpSpeed = 5f;

    private float      _wheelRollAngle    = 0f;
    private float      _currentSteerAngle = 0f;
    private float      _calculatedLean    = 0f;
    private float      _currentPitch      = 0f;
    private Quaternion _bodyBaseRotation;

    // Ángulo X base de cada rueda (baked ~-90° en el import FBX).
    private float _wheelFLBaseX, _wheelFRBaseX, _wheelRLBaseX, _wheelRRBaseX;

    #region MonoBehaviour

    private void Awake()
    {
        if (_bodyMesh)        _bodyBaseRotation = _bodyMesh.localRotation;
        if (_wheelFrontLeft)  _wheelFLBaseX = _wheelFrontLeft.localEulerAngles.x;
        if (_wheelFrontRight) _wheelFRBaseX = _wheelFrontRight.localEulerAngles.x;
        if (_wheelRearLeft)   _wheelRLBaseX = _wheelRearLeft.localEulerAngles.x;
        if (_wheelRearRight)  _wheelRRBaseX = _wheelRearRight.localEulerAngles.x;
    }

    private void LateUpdate()
    {
        UpdateWheelRoll();
        UpdateSteerAngle();
        ApplyWheelRotations();
        ApplyBodyLean();
    }

    #endregion

    #region Wheels

    private void UpdateWheelRoll()
    {
        _wheelRollAngle += _controller.Acceleration * _wheelRotationSpeed * Time.deltaTime;
    }

    private void UpdateSteerAngle()
    {
        float target = _controller.SteerInput * _maxSteerAngle;
        _currentSteerAngle = Mathf.Lerp(_currentSteerAngle, target, _steerLerpSpeed * Time.deltaTime);
    }

    private void ApplyWheelRotations()
    {
        if (_wheelFrontLeft)  _wheelFrontLeft.localRotation  = Quaternion.Euler(_wheelFLBaseX + _wheelRollAngle, _currentSteerAngle, 0f);
        if (_wheelFrontRight) _wheelFrontRight.localRotation = Quaternion.Euler(_wheelFRBaseX + _wheelRollAngle, _currentSteerAngle, 0f);
        if (_wheelRearLeft)   _wheelRearLeft.localRotation   = Quaternion.Euler(_wheelRLBaseX + _wheelRollAngle, 0f, 0f);
        if (_wheelRearRight)  _wheelRearRight.localRotation  = Quaternion.Euler(_wheelRRBaseX + _wheelRollAngle, 0f, 0f);
    }

    #endregion

    #region Body Lean

    private void ApplyBodyLean()
    {
        if (_bodyMesh == null) return;

        float normSpeed = _controller.LinearSpeed;

        float leanTarget = -_controller.SteerInput * (_maxLeanAngle * Mathf.Deg2Rad) * normSpeed;
        _calculatedLean  = Mathf.LerpAngle(_calculatedLean, leanTarget, _leanLerpSpeed * Time.deltaTime);

        float pitchTarget = -(normSpeed - _controller.Acceleration) / 6f;
        _currentPitch     = Mathf.LerpAngle(_currentPitch, pitchTarget, Time.deltaTime * 10f);

        // lean * pitch * base: los ejes se aplican en espacio del padre para no quedar
        // rotados por el -90° baked del FBX.
        Quaternion lean  = Quaternion.AngleAxis(_calculatedLean * Mathf.Rad2Deg, Vector3.forward);
        Quaternion pitch = Quaternion.AngleAxis(_currentPitch   * Mathf.Rad2Deg, Vector3.right);
        _bodyMesh.localRotation = lean * pitch * _bodyBaseRotation;
    }

    #endregion
}
