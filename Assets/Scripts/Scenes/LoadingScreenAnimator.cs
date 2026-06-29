using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenAnimator : MonoBehaviour
{
    [SerializeField] private Text _loadingText;
    [SerializeField] private string _baseText = "LOADING";
    [SerializeField] private float _dotStepSeconds = 0.35f;

    private string[] _loadingFrames;

    private void Awake()
    {
        _loadingFrames = new[]
        {
            _baseText,
            _baseText + ".",
            _baseText + "..",
            _baseText + "..."
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
