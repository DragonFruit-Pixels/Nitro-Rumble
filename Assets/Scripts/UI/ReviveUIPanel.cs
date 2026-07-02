using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ReviveUIPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _countdownText;

    public void Show(int seconds)
    {
        gameObject.SetActive(true);
        SetSeconds(seconds);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetSeconds(int seconds)
    {
        if (_countdownText != null)
            _countdownText.text = string.Format(LocalizationManager.Get("race.reviving"), seconds);
    }
}
