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

// Como HasLocalInputAuthority, pero excluye bots: usar donde la pregunta es
    // "cual auto/HUD soy YO, el humano" (nametag, minimap, HUD, leaderboard) en vez
    // de "tengo autoridad de fisica/red sobre esto" (para eso sigue sirviendo el de arriba).
    // Necesario porque el Master Client puede ser dueno de su propio auto Y de un bot a la vez.
    public static bool IsLocalHumanRacer(PhotonView view)
    {
        if (!HasLocalInputAuthority(view)) return false;

        var racer = view != null ? view.GetComponent<Racer>() : null;
        return racer == null || !racer.IsBot;
    }

}
