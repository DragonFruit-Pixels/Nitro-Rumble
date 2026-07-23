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
        if (_localRacer == null)
            TryFindRacer();
    }

    private void TryFindRacer()
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

        if (_localRacer == null) return;

        if (!_subscribed && RaceManager.Instance != null)
        {
            RaceManager.Instance.OnLapCompleted     += _ => Refresh();
            RaceManager.Instance.OnPositionsUpdated += Refresh;
            RaceManager.Instance.OnRaceStart        += Refresh;
            _subscribed = true;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_lapText == null || _localRacer == null) return;

        int current = _localRacer.CurrentLap + 1;
        int total   = RaceManager.Instance != null ? RaceManager.Instance.TotalLaps : 0;
        int display = Mathf.Min(current, total);
        _lapText.text = display + " / " + total;
    }
}
