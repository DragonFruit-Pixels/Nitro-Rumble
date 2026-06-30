/// <summary>
/// Paquete de datos custom que viaja por la red cuando un auto cruza la meta.
/// Contiene int, string y float — el caso exacto que la PPT (Sync2, "Custom Package")
/// plantea como ejemplo de estructura propia que Photon no sabe serializar por defecto.
///
/// Se registra en <see cref="PhotonCustomTypes"/> con PhotonPeer.RegisterType para poder
/// enviarlo directamente dentro de un RaiseEvent (ver RaceManager.EVENT_REPORT_FINISH),
/// en lugar de un object[] suelto sin tipo.
/// </summary>
public class RaceResultPackage
{
    public int    RacerViewId;     // ViewID del PhotonView del auto que terminó
    public string RacerName;       // nickname del jugador (longitud variable → length-prefixed)
    public float  RaceTime;        // tiempo de carrera en segundos
    public int    ServerTimestamp; // PhotonNetwork.ServerTimestamp del cruce de meta (orden del podio)

    public RaceResultPackage() { }

    public RaceResultPackage(int racerViewId, string racerName, float raceTime, int serverTimestamp)
    {
        RacerViewId     = racerViewId;
        RacerName       = racerName;
        RaceTime        = raceTime;
        ServerTimestamp = serverTimestamp;
    }
}
