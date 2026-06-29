using UnityEngine;

[DisallowMultipleComponent]
public class CarImpactSparks : MonoBehaviour
{
    [SerializeField] private GameObject _sparkPrefab;
    [SerializeField] private float      _lifetime = 1.5f;

    public void Spawn(Vector3 position, Vector3 normal)
    {
        if (_sparkPrefab == null) return;

        Quaternion rot = normal != Vector3.zero
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        GameObject fx = Instantiate(_sparkPrefab, position, rot);
        Destroy(fx, _lifetime);
    }
}
