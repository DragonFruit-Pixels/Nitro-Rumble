using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Feedback visual de la reconexión. Se suscribe a <see cref="ReconnectionManager.OnReconnectStateChanged"/>
/// y muestra un panel con el estado ("Reconectando… intento X/3", "Reconectado", "Conexión perdida").
///
/// Sigue el patrón de NetworkStatusText: suscripción defensiva con re-intento en OnEnable.
/// Wiring (referencias) se asigna en la escena de juego.
/// </summary>
public class ReconnectionPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Raíz visual que se muestra/oculta. DEBE ser un hijo, NO este mismo GameObject: " +
             "si se desactiva el objeto del script, OnDisable lo desuscribe y nunca vuelve a aparecer. " +
             "Mantené este componente en un objeto siempre activo y togglea un hijo.")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Timing")]
    [Tooltip("Segundos que el mensaje final (éxito/fallo) queda visible antes de ocultarse.")]
    [SerializeField] private float _hideDelay = 2f;

    private Coroutine _hideRoutine;
    private ReconnectionManager.ReconnectState _lastState;
    private int _lastAttempt;
    private bool _hasState;

    private void Start()
    {
        SetPanelVisible(false);
        TrySubscribe();
    }

    private void OnEnable()
    {
        TrySubscribe();
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        TryUnsubscribe();
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(Language _)
    {
        if (_hasState) HandleStateChanged(_lastState, _lastAttempt);
    }

    private void TrySubscribe()
    {
        if (ReconnectionManager.Instance == null) return;
        ReconnectionManager.Instance.OnReconnectStateChanged -= HandleStateChanged;
        ReconnectionManager.Instance.OnReconnectStateChanged += HandleStateChanged;
    }

    private void TryUnsubscribe()
    {
        if (ReconnectionManager.Instance == null) return;
        ReconnectionManager.Instance.OnReconnectStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(ReconnectionManager.ReconnectState state, int attempt)
    {
        if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }

        _lastState   = state;
        _lastAttempt = attempt;
        _hasState    = true;

        int maxRetries = ReconnectionManager.Instance != null ? ReconnectionManager.Instance.MaxRetries : attempt;

        switch (state)
        {
            case ReconnectionManager.ReconnectState.Reconnecting:
                SetPanelVisible(true);
                SetText(string.Format(LocalizationManager.Get("reconnect.reconnecting"), attempt, maxRetries));
                break;

            case ReconnectionManager.ReconnectState.Success:
                SetPanelVisible(true);
                SetText(LocalizationManager.Get("reconnect.success"));
                _hideRoutine = StartCoroutine(HideAfterDelay());
                break;

            case ReconnectionManager.ReconnectState.Failed:
                SetPanelVisible(true);
                SetText(LocalizationManager.Get("reconnect.failed"));
                _hideRoutine = StartCoroutine(HideAfterDelay());
                break;
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(_hideDelay);
        SetPanelVisible(false);
        _hideRoutine = null;
    }

    private void SetText(string text)
    {
        if (_statusText != null) _statusText.text = text;
    }

    private void SetPanelVisible(bool visible)
    {
        if (_panelRoot != null) _panelRoot.SetActive(visible);
    }
}
