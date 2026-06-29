using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomObject : MonoBehaviour
{
    [Header("Room Info")]
    [SerializeField] private TextMeshProUGUI _roomName;
    [SerializeField] private TextMeshProUGUI _roomPlayerQuantity;
    [SerializeField] private TextMeshProUGUI _roomConfig;

    [Header("Room Join Button")]
    [SerializeField] private Button          _joinButton;
    [SerializeField] private Image           _joinButtonImage;
    [SerializeField] private TextMeshProUGUI _joinButtonText;
    [SerializeField] private Color           _joinColor;
    [SerializeField] private Color           _fullColor;

    private IJoinRoomHandlerCommands _commands;
    private RoomInfo                 _roomInfo;

    // trackCatalogue mantenido en firma por compatibilidad con JoinRoomHandler (no se usa)
    public void Init(IJoinRoomHandlerCommands commands, RoomInfo roomInfo, TrackCatalogueSO unused)
    {
        _commands = commands;
        _roomInfo = roomInfo;

        bool roomFull = roomInfo.PlayerCount >= roomInfo.MaxPlayers;

        _roomName.SetText(roomInfo.Name);
        _roomPlayerQuantity.SetText(
            $"[{roomInfo.PlayerCount}/{roomInfo.MaxPlayers}] {(roomFull ? "Sala llena" : "Jugadores")}");

        var props = roomInfo.CustomProperties;
        int laps = props.TryGetValue(Keys.LAPS_KEY,       out object l)  && l  is int li ? li : 3;
        int rc   = props.TryGetValue(Keys.RACE_COUNT_KEY, out object r)  && r  is int ri ? ri : 1;

        if (_roomConfig != null)
            _roomConfig.SetText($"{rc} Races\n{laps} Laps");

        _joinButton.interactable = !roomFull;
        _joinButtonImage.color   = roomFull ? _fullColor : _joinColor;
        _joinButtonText.SetText(roomFull ? "FULL" : "JOIN");
    }

    private void OnEnable()  => _joinButton.onClick.AddListener(OnClick);
    private void OnDisable() => _joinButton.onClick.RemoveListener(OnClick);

    private void OnClick() => _commands?.RequestJoinRoom(_roomName.text);
}
