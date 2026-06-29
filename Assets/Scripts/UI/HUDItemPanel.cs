using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HUDItemPanel : MonoBehaviour
{
    [SerializeField] private PowerUpDatabase _database;
    [SerializeField] private Image _itemFrame;
    [SerializeField] private Image _itemIcon;

    private PowerUpInventory _inventory;
    private PowerUpType _lastType = PowerUpType.None;

    private void Update()
    {
        if (_inventory == null)
            TryFindInventory();

        if (_inventory == null) return;

        PowerUpType current = _inventory.CurrentPowerUp;
        if (current != _lastType)
        {
            _lastType = current;
            Refresh(current);
        }
    }

    private void TryFindInventory()
    {
        foreach (PowerUpInventory inv in FindObjectsOfType<PowerUpInventory>())
        {
            var pv = inv.GetComponent<Photon.Pun.PhotonView>();
            if (PhotonViewAuthority.HasLocalInputAuthority(pv))
            {
                _inventory = inv;
                Refresh(PowerUpType.None);
                break;
            }
        }
    }

    private void Refresh(PowerUpType type)
    {
        if (_itemIcon == null) return;

        Sprite sprite = _database != null ? _database.GetIcon(type) : null;
        _itemIcon.sprite  = sprite;
        _itemIcon.enabled = sprite != null;
    }
}
