using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SpeedEffectsOverlay : MonoBehaviour
{
    [Header("Speed Lines")]
    [SerializeField] private Image _speedLinesImage;
    [SerializeField] private float _linesStartNorm  = 0.3f;
    [SerializeField] private float _linesMaxAlpha   = 0.55f;
    [SerializeField] private float _linesMaxScale   = 1.08f;
    [SerializeField] private float _linesRiseSpeed  = 1.5f;  // slow rise — ignores collision spikes
    [SerializeField] private float _linesFallSpeed  = 8f;    // fast fall — desaparece al frenar/chocar

    [Header("Vignette")]
    [SerializeField] private Image _vignetteImage;
    [SerializeField] private float _vignetteMaxAlpha = 0.40f;

    [Header("Turbo Flash")]
    [SerializeField] private Image _turboFlashImage;
    [SerializeField] private float _flashInTime    = 0.08f;
    [SerializeField] private float _flashOutTime   = 0.40f;
    [SerializeField] private float _flashPeakAlpha = 0.45f;

    [Header("Smoothing")]
    [SerializeField] private float _fadeSpeed = 5f;

    private CarController _car;
    private float _smoothedSpeed;

    #region Unity

    private void Start()
    {
        FindLocalCar();
        SetAlpha(_speedLinesImage, 0f);
        SetAlpha(_vignetteImage,   0f);
        SetAlpha(_turboFlashImage, 0f);
    }

    private void Update()
    {
        if (_car == null) { FindLocalCar(); return; }

        float normSpeed = Mathf.Clamp01(Mathf.Abs(_car.Speed) / _car.MaxSpeed);

        // Asymmetric filter: sube lento, baja rápido → spikes de colisión no activan las lines
        float rate = normSpeed > _smoothedSpeed ? _linesRiseSpeed : _linesFallSpeed;
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, normSpeed, Time.deltaTime * rate);

        UpdateSpeedLines(_smoothedSpeed);
        UpdateVignette(normSpeed);
    }

    #endregion

    #region Effects

    private void UpdateSpeedLines(float smoothSpeed)
    {
        if (_speedLinesImage == null) return;

        float t = Mathf.Clamp01((smoothSpeed - _linesStartNorm) / (1f - _linesStartNorm));
        t = t * t;

        Color c = _speedLinesImage.color;
        c.a = t * _linesMaxAlpha;
        _speedLinesImage.color = c;

        float scale = Mathf.Lerp(1f, _linesMaxScale, smoothSpeed);
        _speedLinesImage.rectTransform.localScale = Vector3.one * scale;
    }

    private void UpdateVignette(float normSpeed)
    {
        if (_vignetteImage == null) return;

        float targetAlpha = normSpeed * normSpeed * _vignetteMaxAlpha;
        Color c = _vignetteImage.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * _fadeSpeed);
        _vignetteImage.color = c;
    }

    public void TurboFlash()
    {
        StopAllCoroutines();
        StartCoroutine(TurboFlashRoutine());
    }

    private IEnumerator TurboFlashRoutine()
    {
        if (_turboFlashImage == null) yield break;

        float t = 0f;
        Color c = _turboFlashImage.color;
        while (t < _flashInTime)
        {
            c.a = Mathf.Lerp(0f, _flashPeakAlpha, t / _flashInTime);
            _turboFlashImage.color = c;
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < _flashOutTime)
        {
            c.a = Mathf.Lerp(_flashPeakAlpha, 0f, t / _flashOutTime);
            _turboFlashImage.color = c;
            t += Time.deltaTime;
            yield return null;
        }

        c.a = 0f;
        _turboFlashImage.color = c;
    }

    #endregion

    #region Helpers

    private void FindLocalCar()
    {
        foreach (var car in FindObjectsOfType<CarController>())
        {
            var pv = car.GetComponent<PhotonView>() ?? car.GetComponentInParent<PhotonView>();
            if (PhotonViewAuthority.IsLocalHumanRacer(pv))
            {
                _car = car;
                return;
            }
        }
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    #endregion
}
