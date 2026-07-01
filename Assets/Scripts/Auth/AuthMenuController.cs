using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla la zona de sesión (arriba a la derecha del menú) y el popup de login.
/// - Invitado: muestra el botón "Iniciar sesión / Registrarse" que abre el popup.
/// - Logueado: muestra el username + botón "Cerrar sesión".
/// Se refresca al vuelo escuchando <see cref="AuthSession.OnChanged"/> (login/logout).
///
/// Wiring (vía MCP / inspector): asignar el popup, el botón de login, el texto de username
/// y el botón de logout. El botón cerrar (X) del popup y el evento OnAuthenticated del AuthPanel
/// se enganchan a <see cref="ClosePopup"/>.
/// </summary>
public class AuthMenuController : MonoBehaviour
{
    [Header("Popup")]
    [Tooltip("El GameObject del Auth Panel (popup). Debe estar oculto por defecto.")]
    [SerializeField] private GameObject _authPopup;

    [Header("Estado de sesión (arriba-derecha)")]
    [SerializeField] private Button _openLoginButton;       // "Iniciar sesión / Registrarse"
    [SerializeField] private TextMeshProUGUI _usernameText; // muestra el username logueado
    [SerializeField] private Button _logoutButton;          // "Cerrar sesión"
    [SerializeField] private Button _closeButton;           // botón X del popup

    private void Awake()
    {
        if (_openLoginButton != null) _openLoginButton.onClick.AddListener(OpenPopup);
        if (_logoutButton != null)    _logoutButton.onClick.AddListener(Logout);
        if (_closeButton != null)     _closeButton.onClick.AddListener(ClosePopup);
    }

    private void OnEnable()
    {
        AuthSession.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        AuthSession.OnChanged -= Refresh;
    }

    private void OnDestroy()
    {
        if (_openLoginButton != null) _openLoginButton.onClick.RemoveListener(OpenPopup);
        if (_logoutButton != null)    _logoutButton.onClick.RemoveListener(Logout);
        if (_closeButton != null)     _closeButton.onClick.RemoveListener(ClosePopup);
    }

    public void OpenPopup()
    {
        if (_authPopup != null) _authPopup.SetActive(true);
    }

    public void ClosePopup()
    {
        if (_authPopup != null) _authPopup.SetActive(false);
    }

    public void Logout()
    {
        AuthSession.Clear(); // dispara OnChanged → Refresh()
    }

    // Ajusta la UI arriba-derecha según haya o no un usuario logueado.
    private void Refresh()
    {
        bool logged = AuthSession.IsLoggedIn;

        if (_openLoginButton != null) _openLoginButton.gameObject.SetActive(!logged);
        if (_logoutButton != null)    _logoutButton.gameObject.SetActive(logged);
        if (_usernameText != null)
        {
            _usernameText.gameObject.SetActive(logged);
            if (logged) _usernameText.text = AuthSession.Username;
        }
    }
}
