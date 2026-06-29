using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PowerUpVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private GameObject _shieldPrefab;
    [SerializeField] private ParticleSystem _turboParticles;

    [Header("Shield")]
    [SerializeField] private float _shieldRotationSpeed = 180f;

    [Header("Turbo")]
    [SerializeField] private float _turboParticleRate = 85f;

    private Transform _shieldInstance;
    private Coroutine _turboRoutine;

    private void Update()
    {
        if (_shieldInstance != null && _shieldInstance.gameObject.activeSelf)
            _shieldInstance.Rotate(Vector3.up, _shieldRotationSpeed * Time.deltaTime, Space.World);
    }

    public void SetShieldVisible(bool visible)
    {
        if (_shieldInstance == null)
        {
            if (_shieldPrefab == null)
            {
                Debug.LogWarning("[PowerUpVisuals] _shieldPrefab not assigned.", this);
                return;
            }

            Transform parent = _visualRoot != null ? _visualRoot : transform;
            _shieldInstance = Instantiate(_shieldPrefab, parent).transform;
            _shieldInstance.localPosition = Vector3.zero;
            _shieldInstance.gameObject.SetActive(false);
        }

        _shieldInstance.gameObject.SetActive(visible);
    }

    public void PlayTurbo(float duration)
    {
        if (_turboParticles == null)
        {
            Debug.LogWarning("[PowerUpVisuals] _turboParticles not assigned.", this);
            return;
        }

        if (_turboRoutine != null)
            StopCoroutine(_turboRoutine);

        _turboRoutine = StartCoroutine(TurboRoutine(duration));
    }

    private IEnumerator TurboRoutine(float duration)
    {
        ParticleSystem.EmissionModule emission = _turboParticles.emission;
        emission.rateOverTime = _turboParticleRate;

        if (!_turboParticles.isPlaying)
            _turboParticles.Play();

        yield return new WaitForSeconds(duration);

        emission.rateOverTime = 0f;
        _turboParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        _turboRoutine = null;
    }
}
