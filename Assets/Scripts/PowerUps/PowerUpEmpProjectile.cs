using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class PowerUpEmpProjectile : MonoBehaviour
{
    private PowerUpEffects _owner;
    private int _attackerActor;
    private int _attackerViewId;
    private float _speed;
    private float _lifeRemaining;
    private bool _hasHit;

    public void Initialize(PowerUpEffects owner, int attackerActor, int attackerViewId, Vector3 position, Vector3 direction, float speed, float radius, float lifetime)
    {
        _owner = owner;
        _attackerActor = attackerActor;
        _attackerViewId = attackerViewId;
        _speed = speed;
        _lifeRemaining = lifetime;

        transform.position = position;
        transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward, Vector3.up);
        transform.localScale = new Vector3(radius * 2f, radius * 2f, 0.12f);
    }

    private void Update()
    {
        transform.position += transform.forward * (_speed * Time.deltaTime);
        _lifeRemaining -= Time.deltaTime;

        if (_lifeRemaining <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        PowerUpEffects target = other.GetComponentInParent<PowerUpEffects>();
        if (target == null || target == _owner)
            return;

        if (_owner == null && _attackerViewId != 0)
        {
            PhotonView attackerView = PhotonView.Find(_attackerViewId);
            if (attackerView != null)
                _owner = attackerView.GetComponent<PowerUpEffects>();
        }

        if (_owner == null)
            return;

        _hasHit = true;
        _owner.ResolveEmpHit(target, _attackerActor, _attackerViewId);
        Destroy(gameObject);
    }
}
