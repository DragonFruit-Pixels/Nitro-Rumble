using Photon.Pun;
using UnityEngine;

/// <summary>
/// Conduce el auto de forma automatica siguiendo los Checkpoints de la pista (mismo path
/// que ya usa RaceManager para validar vueltas — no hace falta pathfinding nuevo).
///
/// Solo actua si el Racer de este auto es un bot Y esta maquina tiene autoridad de red
/// sobre el (dueno actual = quien lo maneja). Si el Master Client cambia, la ownership del
/// PhotonView migra (ver RaceManager.OnMasterClientSwitched) y este componente empieza a
/// calcular solo en la nueva maquina, sin ningun cambio extra.
/// </summary>
[RequireComponent(typeof(Racer))]
[RequireComponent(typeof(CarController))]
public class CarBotDriver : MonoBehaviourPun
{
    [Header("Tuning")]
    [Tooltip("Sensibilidad del steer respecto al angulo (grados) hacia el proximo checkpoint.")]
    [SerializeField] private float _steerSensitivity = 1.5f;
    [Tooltip("Angulo (grados) a partir del cual empieza a frenar en curvas cerradas.")]
    [SerializeField] private float _slowdownAngle = 45f;
    [Tooltip("Throttle minimo incluso en la curva mas cerrada (nunca se detiene del todo).")]
    [SerializeField, Range(0f, 1f)] private float _minThrottle = 0.3f;

    [Header("Recuperacion (atascado)")]
    [Tooltip("Si la velocidad real cae debajo de esto mientras persigue el checkpoint, cuenta como atascado.")]
    [SerializeField] private float _stuckSpeedThreshold = 1.5f;
    [Tooltip("Cuanto tiempo (s) tolerando baja velocidad antes de considerarse atascado.")]
    [SerializeField] private float _stuckTimeThreshold = 1f;
    [Tooltip("Cuanto dura la maniobra de retroceso al detectar atasco.")]
    [SerializeField] private float _recoveryDuration = 1.2f;

    private float _stuckTimer;
    private float _recoveryTimer;
    private float _recoverySteerDir;

    // Si se vuelve a trabar enseguida despues de una recuperacion, escala: retrocede mas
    // tiempo y alterna el lado de giro en vez de repetir el mismo (evita el loop de reintentar
    // siempre para el mismo lado en una esquina cerrada donde ese lado nunca alcanza).
    private int   _consecutiveStuckCount;
    private float _unstuckGraceTimer;

    private Racer _racer;
    private CarController _controller;

    public float ThrottleInput { get; private set; }
    public float SteerInput    { get; private set; }
    public bool  IsBot => _racer != null && _racer.IsBot;

    private bool IsLocalAuthority => PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private void Awake()
    {
        _racer      = GetComponent<Racer>();
        _controller = GetComponent<CarController>();
    }

private void Update()
    {
        if (!IsBot || !IsLocalAuthority || RaceManager.Instance == null)
        {
            ThrottleInput = 0f;
            SteerInput    = 0f;
            _stuckTimer    = 0f;
            _recoveryTimer = 0f;
            return;
        }

        // Maniobra de retroceso: el seek simple hacia UN checkpoint puede empotrar el auto
        // contra una pared en curvas cerradas (no hay forma de "dar la vuelta" solo steereando).
        // Mientras dura, ignora el checkpoint y retrocede girando para el lado contrario al que
        // intentaba doblar cuando se atasco.
        if (_recoveryTimer > 0f)
        {
            _recoveryTimer -= Time.deltaTime;
            ThrottleInput = -1f;
            SteerInput    = _recoverySteerDir;
            return;
        }

        Transform target = RaceManager.Instance.GetCheckpointTransform(_racer.LastCheckpoint + 1);
        if (target == null || _controller.VisualTransform == null)
        {
            ThrottleInput = 0f;
            SteerInput    = 0f;
            return;
        }

        Vector3 toTarget = target.position - _controller.VisualTransform.position;
        toTarget.y = 0f;

        float angle = Vector3.SignedAngle(_controller.VisualTransform.forward, toTarget, Vector3.up);

        SteerInput = Mathf.Clamp((angle / 45f) * _steerSensitivity, -1f, 1f);

        // Cuanto mas cerrado el angulo hacia el proximo checkpoint, mas frena (sin llegar a 0).
        float slowdown = Mathf.Clamp01(1f - Mathf.Abs(angle) / _slowdownAngle);
        ThrottleInput = Mathf.Max(_minThrottle, slowdown);

        DetectStuck();
    }

// Si el auto acelera pero casi no avanza durante _stuckTimeThreshold segundos seguidos,
    // arranca la maniobra de retroceso (ver Update).
private void DetectStuck()
    {
        float speed = _controller.PhysicsBody != null ? _controller.PhysicsBody.velocity.magnitude : 0f;

        if (speed < _stuckSpeedThreshold)
        {
            _stuckTimer += Time.deltaTime;
            _unstuckGraceTimer = 0f;

            if (_stuckTimer >= _stuckTimeThreshold)
            {
                _consecutiveStuckCount++;

                // Intentos alternados: 1ro para el lado contrario al steer actual, 2do para el
                // mismo lado (si el contrario no alcanzo), y asi turnando. Cada intento nuevo
                // retrocede mas tiempo (tope x3) para tener mas margen de escape.
                float baseDir = -Mathf.Sign(SteerInput != 0f ? SteerInput : 1f);
                _recoverySteerDir = (_consecutiveStuckCount % 2 == 1) ? baseDir : -baseDir;
                _recoveryTimer    = _recoveryDuration * Mathf.Min(_consecutiveStuckCount, 3);
                _stuckTimer       = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;

            // Solo se considera "realmente destrabado" tras un tramo sostenido a buena velocidad,
            // no un pico de un frame (que podria ser solo el rebote del ultimo intento fallido).
            _unstuckGraceTimer += Time.deltaTime;
            if (_unstuckGraceTimer > 1.5f)
                _consecutiveStuckCount = 0;
        }
    }

}
