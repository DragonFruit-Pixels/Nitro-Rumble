using UnityEngine;

/// <summary>
/// Todos los valores de "feel" del auto y la cámara en un solo asset configurable.
/// Lo consumen <see cref="CarController"/> (manejo) y <see cref="CarCamera"/> (cámara).
///
/// Poné el asset en Assets/Resources/CarTuning.asset — así ambos componentes lo cargan
/// automáticamente por Resources aunque no esté cableado en la escena.
/// </summary>
[CreateAssetMenu(menuName = "Racing/Car Tuning")]
public class CarTuningSO : ScriptableObject
{
    // ── Movimiento ───────────────────────────────────────────────────────────────
    [Header("Movimiento")]
    [Tooltip("Fuerza de aceleración (m/s²) con el acelerador a fondo y auto detenido. Se atenúa al acercarse a la velocidad máxima.")]
    [SerializeField] private float _accelerationForce = 35f;
    [Tooltip("Velocidad máxima hacia adelante (m/s). El tope surge natural: la fuerza tiende a 0 al acercarse a este valor.")]
    [SerializeField] private float _maxSpeed = 20f;
    [Tooltip("Fuerza de frenado (m/s²) al apretar reversa mientras se avanza.")]
    [SerializeField] private float _brakeForce = 45f;
    [Tooltip("Velocidad máxima en reversa (m/s).")]
    [SerializeField] private float _reverseMaxSpeed = 8f;
    [Tooltip("Resistencia de rodadura al soltar el acelerador (m/s²). Bajo = costea y conserva inercia; alto = frena solo.")]
    [SerializeField] private float _coastDecel = 6f;
    [Tooltip("Por debajo de esta velocidad (m/s) sin acelerar, el auto se detiene del todo.")]
    [SerializeField] private float _idleStopSpeed = 0.3f;

    // ── Dirección ────────────────────────────────────────────────────────────────
    [Header("Dirección")]
    [Tooltip("Velocidad de giro máxima (rad/s) doblando a fondo y a máxima velocidad. 3.6 rad/s ≈ 206°/s.")]
    [SerializeField] private float _maxTurnRate = 3.6f;
    [Tooltip("Qué tan rápido el giro alcanza su objetivo. Más bajo = arranque de giro más suave.")]
    [SerializeField] private float _steerResponse = 4f;

    // ── Cámara: seguimiento ──────────────────────────────────────────────────────
    [Header("Cámara — Seguimiento")]
    [Tooltip("Velocidad de suavizado de la POSICIÓN de la cámara. Más alto = más pegada/tensa.")]
    [SerializeField] private float _followSpeed = 4f;
    [Tooltip("Velocidad de suavizado de la ROTACIÓN de la cámara. Más bajo = más inercia/trailing (menos brusco).")]
    [SerializeField] private float _rotationSpeed = 5f;
    [Tooltip("Altura de la cámara sobre el auto (m).")]
    [SerializeField] private float _heightOffset = 3f;
    [Tooltip("Distancia mínima de la cámara (a baja velocidad).")]
    [SerializeField] private float _minDistance = 7f;
    [Tooltip("Distancia máxima de la cámara (a alta velocidad). Subirlo aleja la cámara.")]
    [SerializeField] private float _maxDistance = 8.5f;
    [Tooltip("Velocidad de transición del zoom por velocidad.")]
    [SerializeField] private float _zoomSpeed = 2f;
    [Tooltip("Cuánto mira la cámara por delante del auto (m).")]
    [SerializeField] private float _lookAheadOffset = 1.5f;
    [Tooltip("Colchón de la cámara contra obstáculos (m).")]
    [SerializeField] private float _clipPadding = 0.3f;

    // ── Cámara: FOV / Tilt / Shake ───────────────────────────────────────────────
    [Header("Cámara — FOV / Tilt / Shake")]
    [Tooltip("FOV base (parado / baja velocidad).")]
    [SerializeField] private float _baseFov = 60f;
    [Tooltip("FOV a máxima velocidad. Un swing chico respecto al base evita marear.")]
    [SerializeField] private float _maxFov = 68f;
    [Tooltip("Velocidad de transición del FOV.")]
    [SerializeField] private float _fovSpeed = 3f;
    [Tooltip("Roll/inclinación máxima de la cámara al doblar (grados). Bajo = horizonte estable = menos mareo.")]
    [SerializeField] private float _maxTilt = 1.5f;
    [Tooltip("Velocidad de suavizado del tilt.")]
    [SerializeField] private float _tiltSpeed = 4f;
    [Tooltip("Velocidad de decaimiento del screen shake.")]
    [SerializeField] private float _shakeDecay = 3.5f;

    // ── Getters ──────────────────────────────────────────────────────────────────
    public float AccelerationForce => _accelerationForce;
    public float MaxSpeed          => _maxSpeed;
    public float BrakeForce        => _brakeForce;
    public float ReverseMaxSpeed   => _reverseMaxSpeed;
    public float CoastDecel        => _coastDecel;
    public float IdleStopSpeed     => _idleStopSpeed;

    public float MaxTurnRate       => _maxTurnRate;
    public float SteerResponse     => _steerResponse;

    public float FollowSpeed       => _followSpeed;
    public float RotationSpeed     => _rotationSpeed;
    public float HeightOffset      => _heightOffset;
    public float MinDistance       => _minDistance;
    public float MaxDistance       => _maxDistance;
    public float ZoomSpeed         => _zoomSpeed;
    public float LookAheadOffset   => _lookAheadOffset;
    public float ClipPadding       => _clipPadding;

    public float BaseFov           => _baseFov;
    public float MaxFov            => _maxFov;
    public float FovSpeed          => _fovSpeed;
    public float MaxTilt           => _maxTilt;
    public float TiltSpeed         => _tiltSpeed;
    public float ShakeDecay        => _shakeDecay;
}
