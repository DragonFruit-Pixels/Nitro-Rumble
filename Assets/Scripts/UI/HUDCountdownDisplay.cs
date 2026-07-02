using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class HUDCountdownDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private AudioSource     _audioSource;

    [Header("Animation")]
    [SerializeField] private float _numberDuration = 0.75f;
    [SerializeField] private float _goDuration     = 1.1f;
    [SerializeField] private float _scaleStart     = 0.4f;
    [SerializeField] private float _scaleEnd       = 2.0f;

    private void Start()
    {
        SetLabelAlpha(0f);
        if (RaceManager.Instance != null)
            RaceManager.Instance.OnCountdown += OnCountdown;
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance != null)
            RaceManager.Instance.OnCountdown -= OnCountdown;
    }

    private void OnCountdown(int value)
    {
        StopAllCoroutines();
        var sfx = GameSFX.Instance;
        if (value == 0)
            StartCoroutine(Animate(LocalizationManager.Get("race.go"), sfx?.countdownGo ?? default, _goDuration));
        else if (value > 0)
            StartCoroutine(Animate(value.ToString(), sfx?.countdownBeep ?? default, _numberDuration));
    }

    private IEnumerator Animate(string text, SFXEntry sfxEntry, float duration)
    {
        _label.text = text;
        _label.transform.localScale = Vector3.one * _scaleStart;
        SetLabelAlpha(1f);

        sfxEntry.Play(_audioSource);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t         = elapsed / duration;
            float scaleLerp = 1f - Mathf.Pow(1f - t, 2f);
            _label.transform.localScale = Vector3.one * Mathf.Lerp(_scaleStart, _scaleEnd, scaleLerp);
            SetLabelAlpha(t < 0.3f ? 1f : Mathf.InverseLerp(1f, 0.3f, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetLabelAlpha(0f);
        _label.transform.localScale = Vector3.one;
    }

    private void SetLabelAlpha(float a)
    {
        if (_label == null) return;
        var col = _label.color;
        col.a = a;
        _label.color = col;
    }
}
