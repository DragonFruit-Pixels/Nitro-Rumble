using Photon.Pun;
using UnityEngine;

/// <summary>
/// Deformación visual stretch & squash del auto: estira hacia adelante al acelerar/turbo
/// y aplasta al frenar/impactar. Es puramente cosmético (escala un transform visual).
///
/// El dueño (local) calcula la deformación a partir de la aceleración y el estado de boost.
/// El valor resultante (<see cref="CurrentSquash"/>) lo envía CarNetworkSync por la red usando
/// el bitmask delta (bit DIRTY_SQUASH); los autos remotos lo aplican vía <see cref="SetRemoteSquash"/>.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class CarStretchSquash : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform de la malla visual a deformar. NO usar el sphere de físicas ni el container raíz.")]
    [SerializeField] private Transform     _squashTarget;
    [SerializeField] private CarController  _controller;
    [SerializeField] private CarDriftBoost  _driftBoost;

    [Header("Tuning")]
    [Tooltip("Estiramiento/aplastado máximo (fracción de la escala base).")]
    [SerializeField] private float _stretchAmount  = 0.15f;
    [Tooltip("Aceleración → factor de squash. Subir si la deformación es muy sutil.")]
    [SerializeField] private float _accelScale     = 0.04f;
    [Tooltip("Estiramiento fijo mientras hay turbo activo.")]
    [SerializeField] private float _boostStretch    = 0.5f;
    [Tooltip("Velocidad de suavizado hacia el valor objetivo.")]
    [SerializeField] private float _squashResponse  = 10f;

    private PhotonView _photonView;
    private Vector3    _baseScale;
    private float      _prevSpeed;
    private float      _localTarget;   // target de squash calculado en FixedUpdate (ver comentario ahi)
    private float      _currentSquash; // -1 (aplastado) .. +1 (estirado), suavizado
    private float      _remoteSquash;

    private bool IsLocal => PhotonViewAuthority.HasLocalInputAuthority(_photonView);

    /// <summary>Valor de deformación actual del dueño, leído por CarNetworkSync para enviarlo.</summary>
    public float CurrentSquash => _currentSquash;

    /// <summary>Valor recibido por red, aplicado en los autos remotos.</summary>
    public void SetRemoteSquash(float value) => _remoteSquash = value;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
        if (_controller == null) _controller = GetComponent<CarController>();
        if (_driftBoost == null) _driftBoost = GetComponent<CarDriftBoost>();
        if (_squashTarget != null) _baseScale = _squashTarget.localScale;
    }

    private void Update()
    {
        if (_squashTarget == null) return;

        float target = IsLocal ? _localTarget : _remoteSquash;

        _currentSquash = Mathf.Lerp(_currentSquash, target, Time.deltaTime * _squashResponse);
        ApplyScale(_currentSquash);
    }

    // LinearSpeed solo cambia en FixedUpdate (CarController.UpdateSpeed), por eso la derivada
    // de aceleracion tambien se calcula aca con Time.fixedDeltaTime. Calcularla en Update()
    // con Time.deltaTime daba lecturas erraticas: la mayoria de los frames ven la misma
    // velocidad (accel = 0), con un pico esporadico que el Lerp de suavizado casi anulaba.
    private void FixedUpdate()
    {
        if (!IsLocal) return;

        float speed = _controller != null ? _controller.LinearSpeed : 0f;
        float accel = (speed - _prevSpeed) / Time.fixedDeltaTime;
        _prevSpeed = speed;

        float target = Mathf.Clamp(accel * _accelScale, -1f, 1f);

        if (_driftBoost != null && _driftBoost.IsBoosting)
            target = Mathf.Max(target, _boostStretch);

        _localTarget = target;
    }

    // s > 0 → estira en Z y adelgaza lateral; s < 0 → aplasta (invierte).
    private void ApplyScale(float s)
    {
        Vector3 scale = _baseScale;
        scale.z *= 1f + s * _stretchAmount;
        float lateral = 1f - s * _stretchAmount * 0.5f; // pseudo-conservación de volumen
        scale.x *= lateral;
        scale.y *= lateral;
        _squashTarget.localScale = scale;
    }
}
