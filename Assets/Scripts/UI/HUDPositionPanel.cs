using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HUDPositionPanel : MonoBehaviour
{
    [Header("Referencias (reemplazar con assets finales)")]
    [SerializeField] private Text  _positionNumber;
    [SerializeField] private Text  _positionSuffix;
    [SerializeField] private Image _positionBackground;

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
            RaceManager.Instance.OnPositionsUpdated += Refresh;
            RaceManager.Instance.OnRaceStart        += Refresh;
            _subscribed = true;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_positionNumber == null || _localRacer == null) return;

        int pos = _localRacer.Position;
        _positionNumber.text = pos > 0 ? pos.ToString() : "1";
        if (_positionSuffix != null)
            _positionSuffix.text = GetSuffix(pos > 0 ? pos : 1);
    }

    private static string GetSuffix(int n)
    {
        if (LocalizationManager.CurrentLanguage == Language.Spanish)
            return "°"; // 1°, 2°, 3°... el español no usa sufijos ordinales tipo st/nd/rd

        int mod100 = n % 100;
        if (mod100 >= 11 && mod100 <= 13) return "th";
        switch (n % 10)
        {
            case 1:  return "st";
            case 2:  return "nd";
            case 3:  return "rd";
            default: return "th";
        }
    }
}
