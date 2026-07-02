using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Agrega a cualquier texto (TMP_Text o el UI.Text legacy que todavía usa parte del HUD de carrera)
/// para que su contenido se actualice automáticamente cuando cambia el idioma.
/// Asignar la clave desde el Inspector.
/// </summary>
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string _key;

    TMP_Text _tmpText;
    Text     _legacyText;

    void Awake()
    {
        _tmpText = GetComponent<TMP_Text>();
        if (_tmpText == null)
            _legacyText = GetComponent<Text>();

        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        Refresh();
    }

    void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    void OnLanguageChanged(Language _) => Refresh();

    void Refresh()
    {
        if (string.IsNullOrEmpty(_key)) return;
        string value = LocalizationManager.Get(_key);
        if (_tmpText != null) _tmpText.text = value;
        else if (_legacyText != null) _legacyText.text = value;
    }

    // Permite cambiar la clave en runtime (útil para textos dinámicos).
    public void SetKey(string key)
    {
        _key = key;
        Refresh();
    }
}
