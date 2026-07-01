using System;
using System.Collections.Generic;
using UnityEngine;

public enum Language { Spanish = 0, English = 1 }

[CreateAssetMenu(fileName = "LanguageTable", menuName = "Nitro Rumble/Language Table")]
public class LanguageTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;
        [Tooltip("Una cadena por idioma. Índice 0 = Spanish, 1 = English, etc.")]
        public string[] translations;
    }

    [SerializeField] private Entry[] _entries;

    private Dictionary<string, string[]> _map;

    void OnEnable() => Build();

    void Build()
    {
        _map = new Dictionary<string, string[]>(_entries?.Length ?? 0);
        if (_entries == null) return;
        foreach (Entry e in _entries)
            if (!string.IsNullOrEmpty(e.key))
                _map[e.key] = e.translations;
    }

    public string Get(string key, Language lang)
    {
        if (_map == null) Build();
        if (!_map.TryGetValue(key, out string[] translations)) return key;
        int idx = (int)lang;
        if (translations == null || idx >= translations.Length) return key;
        string val = translations[idx];
        return string.IsNullOrEmpty(val) ? key : val;
    }
}
