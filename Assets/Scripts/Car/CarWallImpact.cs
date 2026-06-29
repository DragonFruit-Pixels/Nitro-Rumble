using UnityEngine;

// Attached to the physics Sphere child of the Car prefab.
// Detects wall/obstacle collisions (not car-vs-car) and triggers shake + sparks.
[DisallowMultipleComponent]
public class CarWallImpact : MonoBehaviour
{
    [SerializeField] private float _minImpactSpeed = 3f;
    [SerializeField] private float _maxShake       = 0.8f;
    [SerializeField] private float _speedToShake   = 0.06f;

    private CarCamera        _carCamera;
    private CarImpactSparks  _sparks;

    private void Start()
    {
        _carCamera = FindObjectOfType<CarCamera>();
        _sparks    = GetComponentInParent<CarImpactSparks>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_carCamera == null) _carCamera = FindObjectOfType<CarCamera>();
        if (_sparks    == null) _sparks    = GetComponentInParent<CarImpactSparks>();

        if (collision.collider.GetComponentInParent<CarArcadeCollision>() != null) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < _minImpactSpeed) return;

        float intensity = Mathf.Clamp(speed * _speedToShake, 0f, _maxShake);
        _carCamera?.Shake(intensity);

        ContactPoint contact = collision.GetContact(0);
        _sparks?.Spawn(contact.point, contact.normal);
    }
}
