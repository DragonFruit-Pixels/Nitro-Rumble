using System.Collections;
using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class PowerUpEffects : MonoBehaviourPun
{
    [Header("References")]
    [SerializeField] private CarController _controller;
    [SerializeField] private Rigidbody _physicsBody;
    [SerializeField] private PowerUpVisuals _visuals;
    [SerializeField] private GameObject _empProjectilePrefab;
    [SerializeField] private CarCamera _carCamera;

    [Header("Camera Shake")]
    [SerializeField] private float _empShakeIntensity   = 1.4f;
    [SerializeField] private float _turboShakeIntensity = 0.2f;

    [Header("EMP")]
    [SerializeField] private float _empProjectileSpeed = 24f;
    [SerializeField] private float _empProjectileRadius = 0.65f;
    [SerializeField] private float _empProjectileLifetime = 1.35f;
    [SerializeField] private float _empSpawnForwardOffset = 1.55f;
    [SerializeField] private float _empSpawnUpOffset = 0.65f;
    [SerializeField] private float _empStunDuration = 1.25f;
    [SerializeField] private float _empVelocityDamping = 0.25f;

    [Header("Shield")]
    [SerializeField] private float _shieldDuration = 6f;

    [Header("Turbo")]
    [SerializeField] private float _turboDuration    = 1.1f;
    [SerializeField] private float _turboAcceleration = 45f;
    [SerializeField] private float _turboFovPunch    = 15f;

    private bool _shieldActive;
    private int  _shieldActivatedTimestamp;
    private Coroutine _shieldRoutine;
    private Coroutine _empRoutine;
    private Coroutine _turboRoutine;
    private PowerUpEmpProjectile _activeEmpProjectile;
    private SpeedEffectsOverlay  _speedEffects;

    public bool ShieldActive      => _shieldActive;
    public int  ShieldTimestamp   => _shieldActivatedTimestamp;
    public bool IsTurboActive     { get; private set; }

    private bool IsLocalAuthority =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (_controller == null)
            _controller = GetComponent<CarController>();

        if (_physicsBody == null && _controller != null)
            _physicsBody = _controller.PhysicsBody;

        if (_visuals == null)
        {
            _visuals = GetComponent<PowerUpVisuals>();
            if (_visuals == null)
                Debug.LogWarning("[PowerUpEffects] PowerUpVisuals not found. Assign it in the prefab.", this);
        }

        if (_carCamera == null)
            _carCamera = FindObjectOfType<CarCamera>();
    }

    public bool TryUsePowerUp(PowerUpType powerUp)
    {
        switch (powerUp)
        {
            case PowerUpType.EMP:
                return TryUseEmp();
            case PowerUpType.Shield:
                ActivateShield();
                return true;
            case PowerUpType.Turbo:
                ActivateTurbo();
                return true;
            default:
                return false;
        }
    }

    private bool TryUseEmp()
    {
        if (_empProjectilePrefab == null)
        {
            Debug.LogWarning("[PowerUpEffects] _empProjectilePrefab not assigned.", this);
            return false;
        }

        int attackerActor = OwnerActorNumber();
        int attackerViewId = photonView != null ? photonView.ViewID : 0;

        Transform origin = GetEffectOrigin();
        Vector3 spawnPosition = origin.position + origin.forward * _empSpawnForwardOffset + Vector3.up * _empSpawnUpOffset;
        Vector3 direction = origin.forward;

        if (photonView != null && photonView.ViewID != 0)
            photonView.RPC(nameof(RPC_SpawnEmpProjectile), RpcTarget.All, spawnPosition, direction, attackerActor, attackerViewId);
        else
            RPC_SpawnEmpProjectile(spawnPosition, direction, attackerActor, attackerViewId);

        return true;
    }

    public void ResolveEmpHit(PowerUpEffects target, int attackerActor, int attackerViewId)
    {
        if (target == null || target == this)
            return;

        if (target.ShieldActive)
        {
            if (target.photonView != null && target.photonView.ViewID != 0)
                target.photonView.RPC(nameof(RPC_ConsumeShield), RpcTarget.All, attackerActor, attackerViewId);
            else
                target.RPC_ConsumeShield(attackerActor, attackerViewId);

            return;
        }

        if (target.photonView != null && target.photonView.ViewID != 0)
            target.photonView.RPC(nameof(RPC_ApplyEmp), RpcTarget.All, attackerActor, attackerViewId, _empStunDuration);
        else
            target.RPC_ApplyEmp(attackerActor, attackerViewId, _empStunDuration);

        if (photonView != null && photonView.ViewID != 0)
            photonView.RPC(nameof(RPC_DestroyEmpProjectile), RpcTarget.All);
        else
            RPC_DestroyEmpProjectile();
    }

    private void ActivateShield()
    {
        if (photonView != null && photonView.ViewID != 0)
            photonView.RPC(nameof(RPC_SetShield), RpcTarget.All, true, _shieldDuration);
        else
            RPC_SetShield(true, _shieldDuration);
    }

    private void ActivateTurbo()
    {
        if (photonView != null && photonView.ViewID != 0)
            photonView.RPC(nameof(RPC_ActivateTurbo), RpcTarget.All, _turboDuration, _turboAcceleration);
        else
            RPC_ActivateTurbo(_turboDuration, _turboAcceleration);
    }

    [PunRPC]
    private void RPC_SetShield(bool active, float duration)
    {
        ResolveReferences();

        if (_shieldRoutine != null)
            StopCoroutine(_shieldRoutine);

        _shieldActive = active;
        if (_visuals != null)
            _visuals.SetShieldVisible(active);

        if (active)
        {
            _shieldActivatedTimestamp = PhotonNetwork.ServerTimestamp;
            _shieldRoutine = StartCoroutine(ShieldRoutine(duration));
        }
    }

    [PunRPC]
    private void RPC_ConsumeShield(int attackerActor, int attackerViewId)
    {
        if (_shieldRoutine != null)
        {
            StopCoroutine(_shieldRoutine);
            _shieldRoutine = null;
        }

        _shieldActive = false;
        if (_visuals != null)
            _visuals.SetShieldVisible(false);
    }

    [PunRPC]
    private void RPC_SpawnEmpProjectile(Vector3 spawnPosition, Vector3 direction, int attackerActor, int attackerViewId)
    {
        if (_empProjectilePrefab == null)
            return;

        PowerUpEffects owner = this;
        if (attackerViewId != 0)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewId);
            if (attackerView != null)
                owner = attackerView.GetComponent<PowerUpEffects>();
        }

        GameObject projectileObject = Instantiate(_empProjectilePrefab);
        projectileObject.name = "EMP_Projectile_" + attackerActor;

        PowerUpEmpProjectile projectile = projectileObject.GetComponent<PowerUpEmpProjectile>();
        projectile.Initialize(owner, attackerActor, attackerViewId, spawnPosition, direction, _empProjectileSpeed, _empProjectileRadius, _empProjectileLifetime);
        _activeEmpProjectile = projectile;
    }

    [PunRPC]
    private void RPC_DestroyEmpProjectile()
    {
        if (_activeEmpProjectile != null)
        {
            Destroy(_activeEmpProjectile.gameObject);
            _activeEmpProjectile = null;
        }
    }

    [PunRPC]
    private void RPC_ApplyEmp(int attackerActor, int attackerViewId, float duration)
    {
        if (!IsLocalAuthority)
            return;

        if (_empRoutine != null)
            StopCoroutine(_empRoutine);

        _empRoutine = StartCoroutine(EmpRoutine(duration));
    }

    [PunRPC]
    private void RPC_ActivateTurbo(float duration, float acceleration)
    {
        ResolveReferences();
        if (_visuals != null)
            _visuals.PlayTurbo(duration);

        if (!IsLocalAuthority)
            return;

        if (_turboRoutine != null)
            StopCoroutine(_turboRoutine);

        _turboRoutine = StartCoroutine(TurboRoutine(duration, acceleration));
    }

    private IEnumerator ShieldRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        _shieldActive = false;
        if (_visuals != null)
            _visuals.SetShieldVisible(false);

        _shieldRoutine = null;
    }

    private Transform GetEffectOrigin()
    {
        ResolveReferences();
        return _controller != null && _controller.VisualTransform != null
            ? _controller.VisualTransform
            : transform;
    }

    private IEnumerator EmpRoutine(float duration)
    {
        ResolveReferences();

        if (_carCamera != null)
            _carCamera.Shake(_empShakeIntensity);

        bool previousCanMove = _controller != null && _controller.CanMove;
        if (_controller != null)
            _controller.CanMove = false;

        if (_physicsBody != null)
            _physicsBody.velocity *= _empVelocityDamping;

        yield return new WaitForSeconds(duration);

        if (_controller != null)
            _controller.CanMove = previousCanMove;

        _empRoutine = null;
    }

    private IEnumerator TurboRoutine(float duration, float acceleration)
    {
        ResolveReferences();

        if (_carCamera != null)
        {
            _carCamera.Shake(_turboShakeIntensity);
            _carCamera.TurboFovPunch(_turboFovPunch);
        }

        if (_speedEffects == null) _speedEffects = FindObjectOfType<SpeedEffectsOverlay>();
        _speedEffects?.TurboFlash();

        IsTurboActive = true;

        float remaining = duration;
        while (remaining > 0f)
        {
            if (_physicsBody != null && _controller != null && _controller.VisualTransform != null)
                _physicsBody.AddForce(_controller.VisualTransform.forward * acceleration, ForceMode.Acceleration);

            remaining -= Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        IsTurboActive = false;
        _turboRoutine = null;
    }

    private int OwnerActorNumber()
    {
        return photonView != null && photonView.Owner != null ? photonView.OwnerActorNr : -1;
    }
}
