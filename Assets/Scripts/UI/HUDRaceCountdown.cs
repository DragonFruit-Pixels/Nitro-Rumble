using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HUDRaceCountdown : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Text       _messageText;
    [SerializeField] private Text       _timerText;

    private float _endTime;
    private bool  _running;
    private bool  _subscribed;

    private void Start()
    {
        if (_panel != null) _panel.SetActive(false);
        TrySubscribe();
    }

    private void Update()
    {
        if (!_subscribed) TrySubscribe();
        if (!_running) return;

        float remaining = _endTime - Time.time;
        if (remaining <= 0f)
        {
            remaining = 0f;
            _running  = false;
        }

        if (_timerText != null)
            _timerText.text = FormatTime(remaining);
    }

    private void TrySubscribe()
    {
        if (RaceManager.Instance == null) return;
        RaceManager.Instance.OnCountdownStarted          += ShowFinishCountdown;
        RaceManager.Instance.OnAloneGracePeriodStarted   += ShowAloneWarning;
        RaceManager.Instance.OnAloneGracePeriodCancelled += Hide;
        RaceManager.Instance.OnLocalRacerFinished        += Hide;
        RaceManager.Instance.OnRaceFinished              += OnRaceFinished;
        _subscribed = true;
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance == null) return;
        RaceManager.Instance.OnCountdownStarted          -= ShowFinishCountdown;
        RaceManager.Instance.OnAloneGracePeriodStarted   -= ShowAloneWarning;
        RaceManager.Instance.OnAloneGracePeriodCancelled -= Hide;
        RaceManager.Instance.OnLocalRacerFinished        -= Hide;
        RaceManager.Instance.OnRaceFinished              -= OnRaceFinished;
    }

    private void ShowFinishCountdown(string finisherName, float seconds)
    {
        if (_panel == null) return;

        if (_messageText != null)
            _messageText.text = string.Format(LocalizationManager.Get("race.finisherAnnounce"), finisherName);

        _endTime = Time.time + seconds;
        _running = true;
        _panel.SetActive(true);
    }

    private void ShowAloneWarning(float seconds)
    {
        if (_panel == null) return;

        if (_messageText != null)
            _messageText.text = LocalizationManager.Get("race.aloneWaiting");

        _endTime = Time.time + seconds;
        _running = true;
        _panel.SetActive(true);
    }

    private void OnRaceFinished(Racer _)
    {
        Hide();
    }

    private void Hide()
    {
        _running = false;
        if (_panel != null) _panel.SetActive(false);
    }

    private static string FormatTime(float t)
    {
        int   minutes = (int)(t / 60f);
        float secs    = t % 60f;
        int   ms      = Mathf.FloorToInt((secs - Mathf.Floor(secs)) * 1000f);
        return $"{minutes:D2}:{(int)secs:D2}.{ms:D3}";
    }
}
