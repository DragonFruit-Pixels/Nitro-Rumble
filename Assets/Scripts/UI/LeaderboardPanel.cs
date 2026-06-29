using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tabla de puntuaciones. Lee el top-N vía LeaderboardService (REST API) y
/// popula una fila (LeaderboardRow) por entrada.
///
/// Por defecto queda SIEMPRE VISIBLE y se auto-refresca cada _refreshInterval
/// segundos (no hace falta botón). También se puede mostrar/ocultar a mano con
/// Show()/Hide() o forzar una lectura con Refresh().
///
/// Wiring en el Editor:
///  - _rowsContainer: el Transform con un VerticalLayoutGroup donde se instancian las filas.
///  - _rowPrefab: prefab con un componente LeaderboardRow.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject _root;          // el panel a mostrar/ocultar (opcional)
    [SerializeField] private Transform  _rowsContainer;
    [SerializeField] private GameObject _rowPrefab;

    [Header("Settings")]
    [SerializeField] private int   _topN = 10;
    [Tooltip("Mostrar la tabla y empezar a refrescar automáticamente al cargar.")]
    [SerializeField] private bool  _autoStart = true;
    [Tooltip("Cada cuántos segundos se vuelve a leer el leaderboard (auto-refresh).")]
    [SerializeField] private float _refreshInterval = 60f;

    private void Start()
    {
        if (!_autoStart) return;

        if (_root != null) _root.SetActive(true);   // siempre visible
        StartCoroutine(AutoRefreshLoop());
    }

    // Hace un refresh inmediato y después repite cada _refreshInterval segundos.
    private IEnumerator AutoRefreshLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(1f, _refreshInterval));
        while (true)
        {
            Refresh();
            yield return wait;
        }
    }

    public void Show()
    {
        if (_root != null) _root.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    public void Refresh()
    {
        if (LeaderboardService.Instance == null)
        {
            Logger.LogWarning("[LeaderboardPanel] LeaderboardService no presente.");
            return;
        }

        LeaderboardService.Instance.GetTopScores(_topN, Populate);
    }

    private void Populate(List<ScoreEntry> scores)
    {
        ClearRows();

        if (_rowsContainer == null || _rowPrefab == null)
        {
            Logger.LogWarning("[LeaderboardPanel] Falta wiring de _rowsContainer o _rowPrefab.");
            return;
        }

        for (int i = 0; i < scores.Count; i++)
        {
            GameObject go = Instantiate(_rowPrefab, _rowsContainer);
            if (go.TryGetComponent(out LeaderboardRow row))
                row.Set(i + 1, scores[i]);
        }
    }

    private void ClearRows()
    {
        if (_rowsContainer == null) return;

        for (int i = _rowsContainer.childCount - 1; i >= 0; i--)
            Destroy(_rowsContainer.GetChild(i).gameObject);
    }
}
