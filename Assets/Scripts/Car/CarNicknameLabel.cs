using Photon.Pun;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class CarNicknameLabel : MonoBehaviour
{
    [SerializeField] private TextMeshPro _label;
    [SerializeField] private bool _hideOnLocalPlayer = true;

    private Racer _racer;
    private Camera _cam;
    private bool _initialized;

    private void Start()
    {
        _racer = GetComponentInParent<Racer>();
        _cam = Camera.main;

        if (_hideOnLocalPlayer && ShouldHide())
        {
            gameObject.SetActive(false);
            return;
        }

        TrySetName();
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;

        // Billboard: siempre mira a la cámara
        if (_cam != null)
            transform.rotation = Quaternion.LookRotation(
                transform.position - _cam.transform.position
            );

        // El NickName de Photon puede llegar unos frames tarde — reintentar hasta obtenerlo
        if (!_initialized) TrySetName();
    }

    private bool ShouldHide()
    {
        // Offline: ocultar (un solo jugador, no necesita verse a sí mismo)
        if (!PhotonNetwork.InRoom) return true;

        var pv = GetComponentInParent<PhotonView>();
        return PhotonViewAuthority.HasLocalInputAuthority(pv);
    }

    private void TrySetName()
    {
        if (_label == null || _racer == null) return;

        string name = _racer.PlayerName;

        // PlayerName devuelve el nombre del GameObject como fallback;
        // esperamos hasta tener un NickName real de Photon antes de marcar como inicializado.
        var pv = _racer.photonView;
        bool hasRealName = pv != null
            && pv.Owner != null
            && !string.IsNullOrEmpty(pv.Owner.NickName);

        _label.text = name;
        _initialized = hasRealName || !PhotonNetwork.InRoom;
    }
}
