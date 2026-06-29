using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HUDFinishBanner : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Text       _label;
    [SerializeField] private float      _displayDuration = 3f;

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
        RaceManager.Instance.OnLocalRacerFinished += Show;
        _subscribed = true;
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance != null)
            RaceManager.Instance.OnLocalRacerFinished -= Show;
    }

    private void Show()
    {
        StopAllCoroutines();
        StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        if (_panel == null) yield break;
        _panel.SetActive(true);

        float elapsed = 0f;
        float punchDuration = 0.35f;

        // Punch de escala al aparecer
        while (elapsed < punchDuration)
        {
            float t = elapsed / punchDuration;
            float scale = Mathf.Lerp(0.4f, 1f, t * t);
            _panel.transform.localScale = Vector3.one * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }
        _panel.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(_displayDuration - punchDuration);

        // Fade out
        elapsed = 0f;
        float fadeDuration = 0.4f;
        CanvasGroup cg = _panel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            while (elapsed < fadeDuration)
            {
                cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            cg.alpha = 1f;
        }

        _panel.SetActive(false);
    }
}
