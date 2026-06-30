using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Controlador del panel de login/registro. Lee usuario y contraseña, llama a
/// <see cref="AuthService"/> y muestra el resultado. Al autenticar con éxito dispara
/// <see cref="OnAuthenticated"/> (wireado en el inspector: ej. mostrar el lobby / conectar).
///
/// Wiring (vía MCP / inspector): asignar los TMP_InputField, los Button y el status text.
/// </summary>
public class AuthPanel : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private TMP_InputField _usernameInput;
    [SerializeField] private TMP_InputField _passwordInput;

    [Header("Buttons")]
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _registerButton;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Eventos")]
    [Tooltip("Se dispara cuando login o registro fueron exitosos.")]
    public UnityEvent OnAuthenticated;

    private void Awake()
    {
        if (_loginButton != null)    _loginButton.onClick.AddListener(OnLoginClicked);
        if (_registerButton != null) _registerButton.onClick.AddListener(OnRegisterClicked);
    }

    private void OnDestroy()
    {
        if (_loginButton != null)    _loginButton.onClick.RemoveListener(OnLoginClicked);
        if (_registerButton != null) _registerButton.onClick.RemoveListener(OnRegisterClicked);
    }

    private void OnLoginClicked()
    {
        if (AuthService.Instance == null) { SetStatus("AuthService no está en la escena."); return; }
        SetInteractable(false);
        SetStatus("Ingresando…");
        AuthService.Instance.Login(_usernameInput.text, _passwordInput.text, HandleResult);
    }

    private void OnRegisterClicked()
    {
        if (AuthService.Instance == null) { SetStatus("AuthService no está en la escena."); return; }
        SetInteractable(false);
        SetStatus("Registrando…");
        AuthService.Instance.Register(_usernameInput.text, _passwordInput.text, HandleResult);
    }

    private void HandleResult(bool success, string error)
    {
        SetInteractable(true);
        if (success)
        {
            SetStatus($"¡Bienvenido, {AuthSession.Username}!");
            OnAuthenticated?.Invoke();
        }
        else
        {
            SetStatus(error ?? "Error de autenticación.");
        }
    }

    private void SetInteractable(bool value)
    {
        if (_loginButton != null)    _loginButton.interactable    = value;
        if (_registerButton != null) _registerButton.interactable = value;
    }

    private void SetStatus(string text)
    {
        if (_statusText != null) _statusText.text = text;
    }
}
