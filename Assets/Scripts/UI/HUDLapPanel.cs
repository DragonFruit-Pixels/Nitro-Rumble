using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HUDLapPanel : MonoBehaviour
{
    [Header("Referencias (reemplazar con assets finales)")]
    [SerializeField] private Image _lapIcon;
    [SerializeField] private Text  _lapText;

    private Racer _localRacer;
    private bool _subscribed;

    private void Update()
    {
        // También entra acá si el panel se reactivó: OnDisable saca los handlers.
        if (_localRacer == null || !_subscribed)
            TryFindRacer();
    }

    private void OnDisable()
    {
        if (_subscribed && RaceManager.Instance != null)
        {
            RaceManager.Instance.OnLapCompleted     -= HandleLapCompleted;
            RaceManager.Instance.OnPositionsUpdated -= Refresh;
            RaceManager.Instance.OnRaceStart        -= Refresh;
        }
        _subscribed = false;
    }

    private void TryFindRacer()
    {
        if (_localRacer == null)
        {
            foreach (Racer r in FindObjectsOfType<Racer>())
            {
                var pv = r.GetComponent<Photon.Pun.PhotonView>();
                if (PhotonViewAuthority.IsLocalHumanRacer(pv))
                {
                    _localRacer = r;
                    break;
                }
            }
        }

        if (_localRacer == null) return;

        if (!_subscribed && RaceManager.Instance != null)
        {
            // Handler con nombre en vez de lambda: a un lambda no se le puede hacer -=
            // (cada uno es una instancia distinta), así que quedaría enganchado para siempre.
            RaceManager.Instance.OnLapCompleted     += HandleLapCompleted;
            RaceManager.Instance.OnPositionsUpdated += Refresh;
            RaceManager.Instance.OnRaceStart        += Refresh;
            _subscribed = true;
        }

        Refresh();
    }

    private void HandleLapCompleted(Racer _) => Refresh();

    private void Refresh()
    {
        if (_lapText == null || _localRacer == null) return;

        int current = _localRacer.CurrentLap + 1;
        int total   = RaceManager.Instance != null ? RaceManager.Instance.TotalLaps : 0;
        int display = Mathf.Min(current, total);
        _lapText.text = display + " / " + total;
    }
}
