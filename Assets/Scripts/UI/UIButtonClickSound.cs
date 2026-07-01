using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class UIButtonClickSound : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Toggle _toggle;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;

        if (_button == null) _button = GetComponent<Button>();
        if (_toggle == null) _toggle = GetComponent<Toggle>();
    }

    private void OnEnable()
    {
        if (_button != null) _button.onClick.AddListener(PlayClick);
        if (_toggle != null) _toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(PlayClick);
        if (_toggle != null) _toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool _) => PlayClick();

    private void PlayClick() => GameSFX.Instance?.uiClick.Play(_audioSource);
}
