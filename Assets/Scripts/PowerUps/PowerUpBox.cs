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

    private Collider _trigger;
    private Vector3 _startLocalPosition;
    private Quaternion _startLocalRotation;
    private bool _available = true;

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

        // Ángulo absoluto derivado del reloj de red compartido (PhotonNetwork.Time) en vez de
        // acumular rotación local por Time.deltaTime: así todos los clientes calculan exactamente
        // el mismo ángulo en el mismo instante, sin necesitar PhotonView ni RPCs.
        float t = (float)PhotonNetwork.Time;

        _visualRoot.localRotation = _startLocalRotation * Quaternion.Euler(0f, (t * _rotationSpeed) % 360f, 0f);
        _visualRoot.localPosition = _startLocalPosition + Vector3.up * (Mathf.Sin(t * _bobSpeed) * _bobAmplitude);
        UpdateColor(t);
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
