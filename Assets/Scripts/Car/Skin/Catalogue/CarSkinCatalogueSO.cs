using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Car Skin Catalogue", menuName = "Car Customization/Car Skin Catalogue")]
public class CarSkinCatalogueSO : ScriptableObject
{
    public List<CarSkinSO> Skins = new List<CarSkinSO>();

    // Se construye en forma diferida y también en OnEnable/OnValidate, de modo que
    // GetSkin/HasSkin funcionen TAMBIÉN en build (OnValidate solo corre en el editor).
    // CarSkinLoader llama HasSkin/GetSkin en runtime, así que esto es necesario.
    private Dictionary<int, CarSkinSO> _skinsByID;

    private Dictionary<int, CarSkinSO> Lookup
    {
        get
        {
            if (_skinsByID == null) RebuildLookup();
            return _skinsByID;
        }
    }

    public CarSkinSO GetSkin(int skinID) =>
        Lookup.TryGetValue(skinID, out CarSkinSO skin) ? skin : null;

    public bool HasSkin(int skinID) => Lookup.ContainsKey(skinID);

    private void RebuildLookup()
    {
        _skinsByID = new Dictionary<int, CarSkinSO>();
        foreach (CarSkinSO skin in Skins)
        {
            if (skin == null) continue;
            // Indexador (no Add) para tolerar IDs duplicados sin lanzar excepción.
            _skinsByID[skin.skinID] = skin;
        }
    }

    private void OnEnable()   => RebuildLookup();
    private void OnValidate() => RebuildLookup();
}
