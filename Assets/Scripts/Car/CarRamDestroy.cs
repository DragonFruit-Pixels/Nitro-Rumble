using System.Collections;
using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class CarRamDestroy : MonoBehaviourPun
{
    [Header("References")]
    [SerializeField] private CarController   _controller;
    [SerializeField] private Racer           _racer;

    [Header("VFX")]
    [SerializeField] private GameObject _explosionPrefab;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;

    private ReviveUIPanel _revivePanel;

    [Header("Timing")]
    [SerializeField] private float _reviveTime  = 3f;
    [SerializeField] private float _graceTime   = 3f;

    [Header("Flicker")]
    [SerializeField] private float _flickerSpeed = 8f;

    public bool IsInvincible   { get; private set; }

    private bool       _isDestroyed;
    private Vector3    _spawnPosition;
    private Quaternion _spawnRotation;

    private bool IsLocal =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    #region Unity

    private void Awake()
    {
        if (_controller == null) _controller = GetComponent<CarController>();
        if (_racer      == null) _racer      = GetComponent<Racer>();
    }

    private void Start()
    {
        var body = _controller?.PhysicsBody;
        if (body != null)
        {
            _spawnPosition = body.transform.position;
            _spawnRotation = body.transform.rotation;
        }
    }

    #endregion

    #region RPC

    [PunRPC]
    public void RPC_RamDestroyed()
    {
        if (_isDestroyed || IsInvincible) return;
        StartCoroutine(ReviveSequence());
    }

    #endregion

    #region Revive Sequence

    private IEnumerator ReviveSequence()
    {
        _isDestroyed = true;

        // 1. Explosión — VFX + sonido
        Vector3 fxPos = _controller?.VisualTransform != null
            ? _controller.VisualTransform.position
            : transform.position;

        if (_explosionPrefab != null)
            Destroy(Instantiate(_explosionPrefab, fxPos, Quaternion.identity), 4f);

        GameSFX.Instance?.carExplosion.Play(_audioSource);

        // 2. Deshabilitar visual, movimiento y colliders de la sphere
        var body = _controller?.PhysicsBody;
        if (body != null)
        {
            body.velocity        = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic     = true;
            foreach (var col in body.GetComponents<Collider>())
                col.enabled = false;
        }
        if (_controller != null) _controller.CanMove = false;
        if (_controller?.VisualTransform != null)
            _controller.VisualTransform.gameObject.SetActive(false);

        // 3. Revive UI (solo cliente local)
        if (IsLocal)
        {
            if (_revivePanel == null)
                _revivePanel = FindObjectOfType<ReviveUIPanel>(true);
            _revivePanel?.Show(Mathf.CeilToInt(_reviveTime));
        }

        // 4. Countdown
        float remaining = _reviveTime;
        while (remaining > 0f)
        {
            if (IsLocal)
            {
                if (_revivePanel == null)
                    _revivePanel = FindObjectOfType<ReviveUIPanel>(true);
                _revivePanel?.SetSeconds(Mathf.CeilToInt(remaining));
            }
            remaining -= Time.deltaTime;
            yield return null;
        }

        // 5. Re-habilitar physics en TODOS los clientes
        if (body != null)
        {
            body.isKinematic = false;
            foreach (var col in body.GetComponents<Collider>())
                col.enabled = true;
        }

        // Teleport solo en el cliente local — CarNetworkSync sincroniza la posición al resto
        if (IsLocal)
        {
            Vector3    respawnPos = GetRespawnPosition();
            Quaternion respawnRot = GetRespawnRotation();

            if (body != null)
            {
                body.transform.SetPositionAndRotation(respawnPos, respawnRot);
                body.velocity        = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (_controller?.VisualTransform != null)
                _controller.VisualTransform.rotation = respawnRot;

            if (_controller != null) _controller.CanMove = true;
        }

        // 6. Re-habilitar visual
        if (_controller?.VisualTransform != null)
            _controller.VisualTransform.gameObject.SetActive(true);

        // 7. Ocultar UI
        if (IsLocal)
            _revivePanel?.Hide();

        _isDestroyed = false;

        // 8. Período de gracia con titilación
        IsInvincible = true;
        var renderers = _controller?.VisualTransform != null
            ? _controller.VisualTransform.GetComponentsInChildren<MeshRenderer>()
            : null;

        float grace = _graceTime;
        while (grace > 0f)
        {
            if (renderers != null)
            {
                bool visible = Mathf.Sin(grace * _flickerSpeed) > 0f;
                foreach (var r in renderers) r.enabled = visible;
            }
            grace -= Time.deltaTime;
            yield return null;
        }

        // 9. Restaurar completamente
        if (renderers != null)
            foreach (var r in renderers) r.enabled = true;

        IsInvincible = false;
    }

    #endregion

    #region Helpers

    private Vector3 GetRespawnPosition()
    {
        if (_racer != null && _racer.LastCheckpoint >= 0 && RaceManager.Instance != null)
        {
            Transform cp = RaceManager.Instance.GetCheckpointTransform(_racer.LastCheckpoint);
            if (cp != null) return cp.position + Vector3.up * 0.5f;
        }
        return _spawnPosition;
    }

    private Quaternion GetRespawnRotation()
    {
        if (_racer != null && _racer.LastCheckpoint >= 0 && RaceManager.Instance != null)
        {
            Transform cp = RaceManager.Instance.GetCheckpointTransform(_racer.LastCheckpoint);
            if (cp != null)
            {
                // Proyectar forward del checkpoint al plano horizontal para que el auto
                // quede siempre derecho (sin inclinación), mirando en dirección de la pista
                Vector3 flat = Vector3.ProjectOnPlane(cp.forward, Vector3.up);
                if (flat.sqrMagnitude > 0.01f)
                    return Quaternion.LookRotation(flat, Vector3.up);
            }
        }
        // Fallback: misma rotación que al hacer spawn (horizontal, sin roll/pitch)
        Vector3 spawnFlat = Vector3.ProjectOnPlane(_spawnRotation * Vector3.forward, Vector3.up);
        return spawnFlat.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(spawnFlat, Vector3.up)
            : Quaternion.identity;
    }

    #endregion
}
