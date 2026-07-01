using TMPro;
using UnityEngine;

/// <summary>
/// Agrega a cualquier TextMeshProUGUI para que su contenido se actualice automáticamente
/// cuando cambia el idioma. Asignar la clave desde el Inspector.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string _key;

    TMP_Text _text;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
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
        _text.text = LocalizationManager.Get(_key);
    }

    // Permite cambiar la clave en runtime (útil para textos dinámicos).
    public void SetKey(string key)
    {
        _key = key;
        Refresh();
    }
}
