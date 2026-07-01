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
    [SerializeField] private Sprite          _joinSprite;
    [SerializeField] private Sprite          _fullSprite;

    [Header("Max Players Icon")]
    [SerializeField] private Image  _maxPlayersIcon;
    [Tooltip("Índice 0 = 1 jugador, 1 = 2, 2 = 3, 3 = 4 (o más, se usa el último disponible).")]
    [SerializeField] private Sprite[] _playerCountIcons;

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
        _joinButtonImage.sprite  = roomFull ? _fullSprite : _joinSprite;
        _joinButtonText.SetText(roomFull ? "FULL" : "JOIN");

        if (_maxPlayersIcon != null && _playerCountIcons != null && _playerCountIcons.Length > 0)
        {
            int idx = Mathf.Clamp(roomInfo.MaxPlayers, 1, _playerCountIcons.Length) - 1;
            _maxPlayersIcon.sprite = _playerCountIcons[idx];
        }
    }

    private void OnEnable()  => _joinButton.onClick.AddListener(OnClick);
    private void OnDisable() => _joinButton.onClick.RemoveListener(OnClick);

    private void OnClick() => _commands?.RequestJoinRoom(_roomName.text);
}
