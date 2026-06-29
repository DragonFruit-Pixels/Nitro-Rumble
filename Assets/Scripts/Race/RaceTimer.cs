using Photon.Pun;
using TMPro;
using UnityEngine;

/// <summary>
/// Muestra el tiempo transcurrido desde que arrancó la carrera.
/// El script vive en un GO siempre activo; _visual es el hijo que se muestra/oculta.
/// </summary>
public class RaceTimer : MonoBehaviour
{
    [SerializeField] private TMP_Text   _label;
    [SerializeField] private GameObject _visual;

    private bool _subscribed;

    private void Update()
    {
        if (RaceManager.Instance == null) return;

        if (!_subscribed)
        {
            RaceManager.Instance.OnRaceStart += OnRaceStart;
            _subscribed = true;
        }

        // Ocultar al terminar la carrera (polling: no depende solo del evento)
        if (RaceManager.Instance.State == RaceState.Finished && _visual != null && _visual.activeSelf)
        {
            _visual.SetActive(false);
            return;
        }

        if (!_visual.activeSelf) return;

        double elapsed = PhotonNetwork.Time - RaceManager.Instance.ExactStartTime;
        if (elapsed < 0) elapsed = 0;

        int    minutes = (int)(elapsed / 60.0);
        double seconds = elapsed - minutes * 60.0;
        if (_label != null)
            _label.text = $"{minutes:00}:{seconds:00.00}";
    }

    private void OnDisable()
    {
        if (_subscribed && RaceManager.Instance != null)
            RaceManager.Instance.OnRaceStart -= OnRaceStart;
        _subscribed = false;
    }

    private void OnRaceStart()
    {
        if (_visual != null) _visual.SetActive(true);
    }
}
