using System;
using UnityEngine;

/// <summary>
/// Punto central de acceso al idioma activo. No requiere estar en la escena —
/// LocalizedText lo usa estáticamente. SettingsManager lo llama al cambiar idioma.
/// </summary>
public static class LocalizationManager
{
    public static event Action<Language> OnLanguageChanged;

    public static Language CurrentLanguage { get; private set; } = Language.Spanish;

    static LanguageTable _table;

    static LanguageTable Table
    {
        get
        {
            if (_table == null)
                _table = Resources.Load<LanguageTable>("LanguageTable");
            return _table;
        }
    }

    public static void SetLanguage(Language lang)
    {
        CurrentLanguage = lang;
        OnLanguageChanged?.Invoke(lang);
    }

    public static string Get(string key)
    {
        if (Table == null)
        {
            Debug.LogWarning("[LocalizationManager] LanguageTable no encontrada en Resources/.");
            return key;
        }
        return Table.Get(key, CurrentLanguage);
    }
}
