using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PowerUpDatabase", menuName = "Racing/Power Up Database")]
public class PowerUpDatabase : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public PowerUpType type;
        public Sprite      icon;
        public string      displayName;
    }

    [SerializeField] private Entry[] _entries;

    public Sprite GetIcon(PowerUpType type)
    {
        for (int i = 0; i < _entries.Length; i++)
            if (_entries[i].type == type)
                return _entries[i].icon;
        return null;
    }

    public string GetDisplayName(PowerUpType type)
    {
        for (int i = 0; i < _entries.Length; i++)
            if (_entries[i].type == type)
                return _entries[i].displayName;
        return type.ToString();
    }
}
