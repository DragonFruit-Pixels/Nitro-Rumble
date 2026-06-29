using System;
using System.Diagnostics;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/LoggerSO")]
public class LoggerSO : ScriptableObject
{
    [Header("Logger Configuration")]
    [SerializeField] private string _prefix;
    [SerializeField] private Color _prefixColor;
    [SerializeField] private bool _showLogs;

    private string _hexColor;
    
    private void OnEnable()
    {
        _hexColor = $"#{ColorUtility.ToHtmlStringRGB(_prefixColor)}";
    }

    [Conditional("UNITY_EDITOR")]
    public void Log(string message, Object sender)
    {
        if (_showLogs)
        {
            Debug.unityLogger.Log($"<color={_hexColor}>{_prefix}:</color> {message}", sender);
        }
    }
}
