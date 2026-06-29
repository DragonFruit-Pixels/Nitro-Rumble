using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class JoinRoomHandler : LobbySubcategory, IJoinRoomHandlerCommands
{
    [SerializeField] private GameObject _roomsHolder;
    [SerializeField] private RoomObject _roomObjectPrefab;
    [SerializeField] private TrackCatalogueSO _trackCatalogue;
    
    private List<RoomObject> _roomObjects = new List<RoomObject>();
    private Dictionary<string, RoomInfo> _cachedRoomList = new Dictionary<string, RoomInfo>();
    
    private void Start()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.InLobby)
            NetworkManager.Instance.RequestJoinLobby();
    }

    public override void OnConnectedToMaster()
    {
        if (!PhotonNetwork.InLobby)
            NetworkManager.Instance.RequestJoinLobby();
    }
    
    private void UpdateCachedRoomList(List<RoomInfo> roomList)
    {
        for(int i=0; i<roomList.Count; i++)
        {
            RoomInfo info = roomList[i];
            if (info.RemovedFromList)
            {
                _cachedRoomList.Remove(info.Name);
            }
            else
            {
                _cachedRoomList[info.Name] = info;
            }
        }
    }
    
    private void UpdateRoomObjects(List<RoomInfo> roomList)
    {
        DestroyRoomObjects();
        
        foreach (RoomInfo room in roomList)
        {
            RoomObject roomObject = Instantiate(_roomObjectPrefab, _roomsHolder.transform);
            
            roomObject.Init(this , room, _trackCatalogue);
            
            _roomObjects.Add(roomObject);
        }
    }

    public override void OnJoinedLobby()
    {
        DestroyRoomObjects(true);
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        UpdateCachedRoomList(roomList);
        UpdateRoomObjects(_cachedRoomList.Values.ToList());
    }

    public override void OnLeftLobby()
    {
        DestroyRoomObjects(true);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        DestroyRoomObjects(true);
    }

    public override void OnJoinedRoom()
    {
        DestroyRoomObjects(true);
    }

    public override void OnLeftRoom()
    {
        DestroyRoomObjects(true);

        if (PhotonNetwork.IsConnected && !PhotonNetwork.InLobby)
            NetworkManager.Instance.RequestJoinLobby();
    }

    public void RequestJoinRoom(string roomName)
    {
        LobbyHandlerCommands.RequestJoinRoom(roomName);
    }

    private void DestroyRoomObjects(bool clearCache = false)
    {
        foreach (RoomObject roomObject in _roomObjects)
        {
            Destroy(roomObject.gameObject);
        }
        
        _roomObjects.Clear();
        if (clearCache) _cachedRoomList.Clear();
    }
}

public interface IJoinRoomHandlerCommands
{
    public void RequestJoinRoom(string roomName);
}
