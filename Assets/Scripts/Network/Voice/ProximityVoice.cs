using Photon.Pun;
using Photon.Voice.Unity;
using UnityEngine;

/// <summary>
/// Chat de voz por proximidad (req. #19). Se coloca en el prefab del auto, junto al
/// <see cref="PhotonVoiceView"/> y al <see cref="Speaker"/> de Photon Voice 2.
///
/// El <see cref="Speaker"/> reproduce la voz remota a través de su <see cref="AudioSource"/>.
/// Este componente, solo en los autos REMOTOS (los que reproducen voz ajena), ajusta el
/// volumen de ese AudioSource según la distancia al oyente local: cerca se escucha fuerte,
/// lejos se apaga. El auto local no se escucha a sí mismo (no tiene voz linkeada).
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class ProximityVoice : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Speaker de Photon Voice que reproduce la voz remota. Si está vacío se busca en hijos.")]
    [SerializeField] private Speaker _speaker;

    [Header("Distancias")]
    [Tooltip("A esta distancia (o menos) la voz se escucha al volumen máximo.")]
    [SerializeField] private float _minDistance = 4f;
    [Tooltip("A esta distancia (o más) la voz se silencia por completo.")]
    [SerializeField] private float _maxDistance = 35f;
    [Tooltip("Volumen máximo (cerca). 0..1")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxVolume = 1f;
    [Tooltip("Curva de caída: X = cercanía normalizada (0 lejos, 1 cerca), Y = factor de volumen.")]
    [SerializeField] private AnimationCurve _falloff = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private PhotonView  _photonView;
    private AudioSource _audioSource;
    private Transform   _listener;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
        if (_speaker == null) _speaker = GetComponentInChildren<Speaker>(true);
        if (_speaker != null) _audioSource = _speaker.GetComponent<AudioSource>();
    }

    private void Update()
    {
        // Solo los autos remotos aplican proximidad (el local no reproduce su propia voz).
        if (_audioSource == null || _photonView.IsMine) return;

        Transform listener = ResolveListener();
        if (listener == null) return;

        float dist = Vector3.Distance(transform.position, listener.position);

        // 0 cuando está a _maxDistance (o más), 1 cuando está a _minDistance (o menos).
        float nearness = Mathf.InverseLerp(_maxDistance, _minDistance, dist);
        _audioSource.volume = _falloff.Evaluate(nearness) * _maxVolume;
    }

    // El oyente local es la cámara principal (sigue al auto del jugador local).
    private Transform ResolveListener()
    {
        if (_listener != null) return _listener;
        if (Camera.main != null) _listener = Camera.main.transform;
        return _listener;
    }
}
