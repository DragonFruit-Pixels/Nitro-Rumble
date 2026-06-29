using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class PowerUpInventory : MonoBehaviourPun
{
    [Header("Input")]
    [SerializeField] private KeyCode _useKey = KeyCode.E;

    private PowerUpEffects _effects;
    private PowerUpType _currentPowerUp = PowerUpType.None;

    public PowerUpType CurrentPowerUp => _currentPowerUp;
    public bool HasPowerUp => _currentPowerUp != PowerUpType.None;

    private bool IsLocalAuthority =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private void Awake()
    {
        _effects = GetComponent<PowerUpEffects>();
    }

    private void Update()
    {
        if (!IsLocalAuthority || !HasPowerUp)
            return;

        if (Input.GetKeyDown(_useKey))
            TryUseCurrentPowerUp();
    }

    public bool CanReceivePowerUp()
    {
        return !HasPowerUp;
    }

    public void ReceivePowerUp(PowerUpType powerUp, int grantedActorNumber)
    {
        _currentPowerUp = powerUp;
    }

    private void TryUseCurrentPowerUp()
    {
        if (_effects == null || _currentPowerUp == PowerUpType.None)
            return;

        PowerUpType usedPowerUp = _currentPowerUp;
        bool consumed = _effects.TryUsePowerUp(usedPowerUp);
        if (!consumed)
            return;

        _currentPowerUp = PowerUpType.None;
    }
}
