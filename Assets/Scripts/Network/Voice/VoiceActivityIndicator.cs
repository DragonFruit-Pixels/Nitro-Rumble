using Photon.Pun;
using Photon.Voice.Unity;
using UnityEngine;

/// <summary>
/// Indicador visual de "está hablando" (req. #19). Se coloca en el prefab del auto, junto al
/// <see cref="Recorder"/> y al <see cref="Speaker"/> de Photon Voice 2.
///
/// En el auto local prende el ícono según <see cref="Recorder.IsCurrentlyTransmitting"/> (el
/// jugador está transmitiendo su propia voz). En autos remotos prende el ícono según
/// <see cref="Speaker.IsPlaying"/> (se está reproduciendo voz de ese jugador).
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class VoiceActivityIndicator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Recorder local. Si está vacío se busca en hijos.")]
    [SerializeField] private Recorder _recorder;
    [Tooltip("Speaker que reproduce la voz remota. Si está vacío se busca en hijos.")]
    [SerializeField] private Speaker _speaker;
    [Tooltip("Ícono world-space que se prende/apaga. Ideal: un sprite billboard sobre el auto.")]
    [SerializeField] private GameObject _icon;

    private PhotonView _photonView;
    private Transform  _billboardTarget;

    private bool IsLocal => PhotonViewAuthority.HasLocalInputAuthority(_photonView);

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
        if (_recorder == null) _recorder = GetComponentInChildren<Recorder>(true);
        if (_speaker  == null) _speaker  = GetComponentInChildren<Speaker>(true);

        if (_icon != null) _icon.SetActive(false);
    }

    private void Update()
    {
        if (_icon == null) return;

        bool speaking = IsLocal
            ? _recorder != null && _recorder.IsCurrentlyTransmitting
            : _speaker  != null && _speaker.IsPlaying;

        if (_icon.activeSelf != speaking)
            _icon.SetActive(speaking);

        Billboard();
    }

    private void Billboard()
    {
        if (!_icon.activeSelf) return;

        if (_billboardTarget == null)
        {
            if (Camera.main == null) return;
            _billboardTarget = Camera.main.transform;
        }

        _icon.transform.forward = _icon.transform.position - _billboardTarget.position;
    }
}
