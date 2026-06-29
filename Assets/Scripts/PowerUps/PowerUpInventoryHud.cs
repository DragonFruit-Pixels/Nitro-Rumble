using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PowerUpInventoryHud : MonoBehaviourPun
{
    [SerializeField] private PowerUpInventory _inventory;
    [SerializeField] private GameObject _hudPrefab;

    private Text _label;

    private bool IsLocalAuthority =>
        PhotonViewAuthority.HasLocalInputAuthority(photonView);

    private void Awake()
    {
        if (_inventory == null)
            _inventory = GetComponent<PowerUpInventory>();
    }

    private void Start()
    {
        if (!IsLocalAuthority)
            return;

        SpawnHud();
        UpdateHud();
    }

    private void Update()
    {
        if (_label != null)
            UpdateHud();
    }

    private void SpawnHud()
    {
        if (_hudPrefab == null)
        {
            Debug.LogWarning("[PowerUpInventoryHud] _hudPrefab not assigned.", this);
            return;
        }

        GameObject instance = Instantiate(_hudPrefab);
        _label = instance.GetComponentInChildren<Text>();
    }

    private void UpdateHud()
    {
        PowerUpType current = _inventory != null ? _inventory.CurrentPowerUp : PowerUpType.None;
        _label.text = current == PowerUpType.None ? "Power-up: Empty" : "Power-up: " + current;
    }
}
