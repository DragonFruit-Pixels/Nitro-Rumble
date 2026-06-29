using UnityEngine;

public class CarAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarController _controller;

    [Header("Engine Sound")]
    [SerializeField] private AudioSource _engineSource;

    [Header("Screech Sound")]
    [SerializeField] private AudioSource _screechSource;

    [Header("Impact Sound")]
    [SerializeField] private AudioSource _impactSource;

    private float _smoothedSpeed;

    #region MonoBehaviour

    private void Start()
    {
        if (_engineSource  != null && !_engineSource.isPlaying)  _engineSource.Play();
        if (_screechSource != null && !_screechSource.isPlaying) _screechSource.Play();
    }

    private void Update()
    {
        // Reinicia sources que se cortaron cuando el visual se desactivó durante el revive
        if (_engineSource  != null && !_engineSource.isPlaying)  _engineSource.Play();
        if (_screechSource != null && !_screechSource.isPlaying) _screechSource.Play();

        float normSpeed      = Mathf.Clamp01(Mathf.Abs(_controller.LinearSpeed));
        float throttleFactor = Mathf.Clamp01(Mathf.Abs(_controller.ThrottleInput));
        UpdateEngine(normSpeed, throttleFactor);
        UpdateScreech(normSpeed);
    }

    private void OnCollisionEnter(Collision collision)
    {
        PlayImpact(collision.relativeVelocity.magnitude);
    }

    #endregion

    #region Engine

    private void UpdateEngine(float speedFactor, float throttleFactor)
    {
        var s = GameSFX.Instance?.engine ?? new EngineAudioSettings
            { minPitch = 0.5f, maxPitch = 3f, minVolume = 0.1f, maxVolume = 0.8f };

        float volumeInput  = Mathf.Clamp(speedFactor + throttleFactor * 0.5f, 0f, 1.5f);
        float targetVolume = Remap(volumeInput, 0f, 1.5f, s.minVolume, s.maxVolume);
        _engineSource.volume = Mathf.Lerp(_engineSource.volume, targetVolume, Time.deltaTime * 5f);

        float targetPitch = Remap(speedFactor, 0f, 1f, s.minPitch, s.maxPitch);
        if (throttleFactor > 0.1f) targetPitch += 0.2f;
        _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, targetPitch, Time.deltaTime * 2f);
    }

    #endregion

    #region Screech

    private void UpdateScreech(float normSpeed)
    {
        float threshold = GameSFX.Instance?.screech.driftThreshold ?? 0.25f;

        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, normSpeed, Time.deltaTime * 1f);
        float driftIntensity = Mathf.Abs(normSpeed - _smoothedSpeed)
                             + Mathf.Abs(_controller.SteerInput * normSpeed * 0.4f);

        float targetVolume = driftIntensity > threshold
            ? Remap(Mathf.Clamp(driftIntensity, threshold, 2f), threshold, 2f, 0f, 1f)
            : 0f;

        _screechSource.volume = Mathf.Lerp(_screechSource.volume, targetVolume, Time.deltaTime * 10f);
        _screechSource.pitch  = Mathf.Lerp(_screechSource.pitch, Mathf.Lerp(1f, 3f, normSpeed), 0.1f);
    }

    #endregion

    #region Impact

    private void PlayImpact(float impactSpeed)
    {
        if (_impactSource == null || _impactSource.isPlaying) return;

        var s = GameSFX.Instance?.impact ?? new ImpactAudioSettings { minSpeed = 1f, maxSpeed = 6f };
        if (impactSpeed < s.minSpeed) return;

        _impactSource.volume = Remap(
            Mathf.Clamp(impactSpeed, s.minSpeed, s.maxSpeed),
            s.minSpeed, s.maxSpeed, 0.05f, 1f);
        _impactSource.Play();
    }

    #endregion

    private float Remap(float value, float from1, float to1, float from2, float to2)
        => from2 + (value - from1) * (to2 - from2) / (to1 - from1);
}
