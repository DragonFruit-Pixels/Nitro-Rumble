using Photon.Pun;

public static class PhotonViewAuthority
{
    public static bool HasLocalInputAuthority(PhotonView view)
    {
        if (view == null || view.ViewID == 0)
            return true;

        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
            return view.IsMine;

        return PhotonNetwork.LocalPlayer != null
            && view.Owner != null
            && !view.Owner.IsInactive
            && view.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber;
    }
}
