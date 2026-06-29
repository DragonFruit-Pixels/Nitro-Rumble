using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de creación de sala simplificado.
/// Solo pide nombre y crea la sala; la configuración del campeonato
/// se hace adentro de la sala con RoomConfigPanel.
/// </summary>
public class CreateRoomHandler : LobbySubcategory
{
    [Header("Settings")]
    [SerializeField] private int _roomCharacterLimit = 16;

    [Header("UI")]
    [SerializeField] private TMP_InputField _createRoomInputField;
    [SerializeField] private Button         _createRoomButton;

    private string CreateRoomInput => _createRoomInputField.text;

    private void Awake()
    {
        _createRoomInputField.characterLimit = _roomCharacterLimit;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        _createRoomButton.onClick.AddListener(OnCreateRoomPressed);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        _createRoomButton.onClick.RemoveListener(OnCreateRoomPressed);
    }

    private void OnCreateRoomPressed()
    {
        string roomName = CreateRoomInput;
        if (roomName == string.Empty)
            roomName = $"Room_{Random.Range(0, 10000)}-{Random.Range(0, 10000)}";
        
        LobbyHandlerCommands?.RequestCreateRoom(roomName);
    }
}
