using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HUDLeaderboard : MonoBehaviour
{
    [System.Serializable]
    public struct Row
    {
        public GameObject root;
        public Text       positionText;
        public Text       nameText;
        public Text       timeText;
    }

    [Header("Root")]
    [SerializeField] private GameObject _panel;

    [Header("Filas (una por jugador, en orden 1°→4°)")]
    [SerializeField] private Row[] _rows;

    private bool _subscribed;

    private void Start()
    {
        if (_panel != null) _panel.SetActive(false);
        TrySubscribe();
    }

    private void Update()
    {
        if (!_subscribed) TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (RaceManager.Instance == null) return;
        RaceManager.Instance.OnRaceFinished += Show;
        _subscribed = true;
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance != null)
            RaceManager.Instance.OnRaceFinished -= Show;
    }

    private void Show(Racer _)
    {
        if (_panel != null) _panel.SetActive(true);

        IReadOnlyList<Racer> racers = RaceManager.Instance.GetRacers(); // ya ordenado por Position

        for (int i = 0; i < _rows.Length; i++)
        {
            ref Row row = ref _rows[i];
            if (row.root == null) continue;

            if (i < racers.Count)
            {
                row.root.SetActive(true);
                Racer racer = racers[i];

                if (row.positionText != null)
                    row.positionText.text = racer.Position.ToString();

                if (row.nameText != null)
                    row.nameText.text = racer.PlayerName;

                if (row.timeText != null)
                    row.timeText.text = racer.FinishTime > 0
                        ? FormatTime(racer.FinishTime)
                        : "DNF";
            }
            else
            {
                row.root.SetActive(false);
            }
        }
    }

    // 02:35.47
    private static string FormatTime(double totalSeconds)
    {
        int    minutes = (int)(totalSeconds / 60.0);
        double secs    = totalSeconds - minutes * 60.0;
        return $"{minutes:D2}:{secs:00.00}";
    }
}
