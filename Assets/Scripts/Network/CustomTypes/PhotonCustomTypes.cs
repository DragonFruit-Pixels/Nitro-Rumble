using System;
using System.Text;
using ExitGames.Client.Photon;
using UnityEngine;

/// <summary>
/// Registra los tipos custom propios del proyecto en el protocolo de serialización de Photon.
///
/// Sigue el patrón de la PPT (Sync2, "Custom Package"): le indicamos a PUN cómo pasar
/// nuestra clase a bytes y viceversa mediante PhotonPeer.RegisterType, usando la misma
/// firma StreamBuffer que Photon emplea para Vector3/Quaternion (CustomTypesUnity.cs).
///
/// Para la conversión campo a campo usamos System.BitConverter (el camino alternativo que
/// la propia PPT muestra en las slides 25-28), respetando el orden de los campos en ambos
/// sentidos como exige la diapositiva 22.
/// </summary>
public static class PhotonCustomTypes
{
    // Código de tipo único (0-255). Photon ya usa 'P'(80) Player, 'Q'(81) Quaternion,
    // 'V'(86) Vector3, 'W'(87) Vector2 — usamos 'R'(82) para RaceResultPackage.
    private const byte RACE_RESULT_CODE = (byte)'R';

    private static bool _registered;

    /// <summary>
    /// Registra los tipos custom una sola vez (idempotente). Se llama al inicio de la
    /// conexión desde NetworkManager.Awake(), antes de cualquier RaiseEvent que los use.
    /// </summary>
    public static void Register()
    {
        if (_registered) return;

        bool ok = PhotonPeer.RegisterType(
            typeof(RaceResultPackage),
            RACE_RESULT_CODE,
            SerializeRaceResult,
            DeserializeRaceResult
        );

        _registered = true;
        Logger.Log($"[PhotonCustomTypes] RegisterType(RaceResultPackage, '{(char)RACE_RESULT_CODE}') → {(ok ? "OK" : "FALLÓ (¿código en uso?)")}");
    }

    // ── Serialización: clase → bytes ──────────────────────────────────────────────
    // Layout (orden fijo, idéntico en deserialización):
    //   [int viewId][int serverTimestamp][float raceTime][int nameLen][nameLen bytes UTF8]
    private static short SerializeRaceResult(StreamBuffer outStream, object customObject)
    {
        var p = (RaceResultPackage)customObject;
        byte[] nameBytes = Encoding.UTF8.GetBytes(p.RacerName ?? string.Empty);

        int size = 4 + 4 + 4 + 4 + nameBytes.Length;
        byte[] bytes = new byte[size];
        int i = 0;

        Buffer.BlockCopy(BitConverter.GetBytes(p.RacerViewId),     0, bytes, i, 4); i += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(p.ServerTimestamp), 0, bytes, i, 4); i += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(p.RaceTime),        0, bytes, i, 4); i += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(nameBytes.Length),  0, bytes, i, 4); i += 4;
        Buffer.BlockCopy(nameBytes,                                0, bytes, i, nameBytes.Length);

        outStream.Write(bytes, 0, size);
        return (short)size;
    }

    // ── Deserialización: bytes → clase ────────────────────────────────────────────
    private static object DeserializeRaceResult(StreamBuffer inStream, short length)
    {
        byte[] bytes = new byte[length];
        inStream.Read(bytes, 0, length);
        int i = 0;

        int   viewId    = BitConverter.ToInt32(bytes, i);  i += 4;
        int   timestamp = BitConverter.ToInt32(bytes, i);  i += 4;
        float raceTime  = BitConverter.ToSingle(bytes, i); i += 4;
        int   nameLen   = BitConverter.ToInt32(bytes, i);  i += 4;
        string name     = nameLen > 0 ? Encoding.UTF8.GetString(bytes, i, nameLen) : string.Empty;

        return new RaceResultPackage(viewId, name, raceTime, timestamp);
    }
}
