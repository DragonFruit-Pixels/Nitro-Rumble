using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class PowerUpBox : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private int _boxId = -1;

    [Header("Visual")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private Material _boxMaterial;
    [SerializeField] private Renderer[] _renderers;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobAmplitude = 0.25f;
    [SerializeField] private float _bobSpeed = 2.5f;
    [SerializeField] private float _hueSpeed = 0.25f;
    [Tooltip("Amplitud del pulso de squash (fraccion de la escala base).")]
    [SerializeField] private float _squashAmount = 0.15f;
    [Tooltip("Velocidad del ciclo de squash.")]
    [SerializeField] private float _squashSpeed = 3f;

    private Collider _trigger;
    private Vector3 _startLocalPosition;
    private Vector3 _startLocalScale;
    private Quaternion _startLocalRotation;
    private bool _available = true;

    // Suaviza PhotonNetwork.Time antes de usarlo en la animacion: el reloj de red hace
    // pequenas correcciones (ping/drift) que a velocidades de rotacion/squash bajas eran
    // invisibles, pero con los valores exagerados quedan como un salto/lag perceptible.
    // El reloj local avanza solo (Time.deltaTime, siempre fluido) y se re-sincroniza de a
    // poco hacia el tiempo de red real, en vez de saltar directo a el cada frame.
    private const float TimeSyncSpeed = 3f;
    private static float _sSmoothedTime;
    private static int   _sSmoothedFrame = -1;

    public int BoxId => _boxId;
    public bool Available => _available;

    private void Awake()
    {
        _trigger = GetComponent<Collider>();
        _trigger.isTrigger = true;

        if (_visualRoot == null)
            _visualRoot = transform;

        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);

        _startLocalPosition = _visualRoot.localPosition;
        _startLocalRotation = _visualRoot.localRotation;
        _startLocalScale    = _visualRoot.localScale;

        if (_boxMaterial != null)
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _renderers[i].material = _boxMaterial;
    }

    private void OnEnable()
    {
        if (PowerUpPickupManager.Instance != null)
            PowerUpPickupManager.Instance.RegisterBox(this);
    }

    private void OnDisable()
    {
        if (PowerUpPickupManager.Instance != null)
            PowerUpPickupManager.Instance.UnregisterBox(this);
    }

    private void Update()
    {
        if (!_available || _visualRoot == null)
            return;

        // Angulo/posicion/squash absolutos derivados del reloj de red compartido (PhotonNetwork.Time)
        // en vez de acumular por Time.deltaTime: todos los clientes calculan exactamente el mismo
        // valor en el mismo instante, sin necesitar PhotonView ni RPCs.
        float t = GetSmoothedNetworkTime();

        _visualRoot.localRotation = _startLocalRotation * Quaternion.Euler(0f, (t * _rotationSpeed) % 360f, 0f);
        _visualRoot.localPosition = _startLocalPosition + Vector3.up * (Mathf.Sin(t * _bobSpeed) * _bobAmplitude);

        float squash = Mathf.Sin(t * _squashSpeed) * _squashAmount;
        Vector3 scale = _startLocalScale;
        scale.y *= 1f + squash;
        float lateral = 1f - squash * 0.5f; // pseudo-conservacion de volumen
        scale.x *= lateral;
        scale.z *= lateral;
        _visualRoot.localScale = scale;

        UpdateColor(t);
    }

    private static float GetSmoothedNetworkTime()
    {
        int frame = Time.frameCount;
        if (frame == _sSmoothedFrame)
            return _sSmoothedTime;

        float networkTime = (float)PhotonNetwork.Time;
        _sSmoothedTime = _sSmoothedFrame < 0
            ? networkTime
            : Mathf.Lerp(_sSmoothedTime + Time.deltaTime, networkTime, Time.deltaTime * TimeSyncSpeed);
        _sSmoothedFrame = frame;
        return _sSmoothedTime;
    }

    
private void OnTriggerEnter(Collider other)
    {
        if (!_available)
            return;

        PowerUpInventory inventory = other.GetComponentInParent<PowerUpInventory>();
        if (inventory == null || !inventory.CanReceivePowerUp())
            return;

        if (PowerUpPickupManager.Instance == null)
            return;

        _available = false;
        PowerUpPickupManager.Instance.RequestPickup(this, inventory);
    }

    public void SetAvailable(bool available)
    {
        _available = available;

        if (_trigger != null)
            _trigger.enabled = available;

        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null)
                _renderers[i].enabled = available;

    }

    private void UpdateColor(float t)
    {
        if (_renderers == null)
            return;

        Color color = Color.HSVToRGB(Mathf.Repeat(t * _hueSpeed + _boxId * 0.17f, 1f), 0.65f, 1f);
        color.a = 0.55f;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer rendererInstance = _renderers[i];
            if (rendererInstance == null)
                continue;

            rendererInstance.material.color = color;
        }
    }

}
