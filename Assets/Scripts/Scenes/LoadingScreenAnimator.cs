using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenAnimator : MonoBehaviour
{
    [SerializeField] private Text _loadingText;
    [SerializeField] private float _dotStepSeconds = 0.35f;

    private string[] _loadingFrames;

    private void Awake()
    {
        RebuildFrames();
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(Language _) => RebuildFrames();

    private void RebuildFrames()
    {
        string baseText = LocalizationManager.Get("loading.text");
        _loadingFrames = new[]
        {
            baseText,
            baseText + ".",
            baseText + "..",
            baseText + "..."
        };
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        UpdateLoadingText(time);
    }

    private void UpdateLoadingText(float time)
    {
        if (_loadingText == null)
            return;

        int frame = Mathf.FloorToInt(time / _dotStepSeconds) % _loadingFrames.Length;
        _loadingText.text = _loadingFrames[frame];
    }
}
