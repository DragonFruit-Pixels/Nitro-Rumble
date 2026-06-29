using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Guarda y carga el PlayerProfile de forma local con encriptación XOR.
///
/// API pública — los puntos de llamada ya están conectados en RaceManager.
/// Para implementar el guardado real, solo hay que rellenar Save() y Load().
/// </summary>
public class LocalSaveManager : Singleton<LocalSaveManager>
{
    // La clave XOR puede ser cualquier secuencia de bytes — cuanto más larga, mejor.
    // Cambiarla invalida todos los archivos guardados existentes.
    private static readonly byte[] EncryptionKey = { 0x52, 0x61, 0x63, 0x69, 0x6E, 0x67, 0x4B, 0x65, 0x79 };

    private static string SavePath => Path.Combine(Application.persistentDataPath, "playerdata.bin");

    public PlayerProfile Profile { get; private set; } = new PlayerProfile();

    public override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(this.gameObject);
        
        Load();
    }

    // ── API pública ────────────────────────────────────────────────────────

    /// <summary>
    /// Llamado por RaceManager al terminar la carrera (desde HandlePodiumEvent).
    /// trackId = nombre de la escena activa ("Track01", "Track02", etc.)
    /// </summary>
    public void OnRaceCompleted(string trackId, double raceTime, int position, int totalPlayers)
    {
        if (string.IsNullOrEmpty(trackId)) return;

        Profile.totalRaces++;
        if (position == 1) Profile.totalWins++;
        if (position == 2) Profile.totalSilvers++;
        if (position == 3) Profile.totalBronzes++;

        TrackRecord rec = Profile.GetOrCreateRecord(trackId);
        rec.racesCompleted++;

        bool improved = false;
        if (raceTime > 0 && rec.IsBetterTime(raceTime))
        {
            rec.bestRaceTime = raceTime;
            improved = true;
        }
        if (rec.IsBetterPosition(position))
        {
            rec.bestPosition = position;
            improved = true;
        }

        if (improved)
            Logger.Log($"[LocalSaveManager] Nuevo récord en {trackId}: pos={position}, time={raceTime:F2}s");

        Save();
    }

    /// <summary>
    /// Guarda el nickname actual (tomado de Photon.NickName si no se pasa uno).
    /// </summary>
    public void SaveNickname(string nickname)
    {
        Profile.nickname = nickname;
        Save();
    }

    /// <summary>
    /// Guarda el nickname actual (tomado de Photon.NickName si no se pasa uno).
    /// </summary>
    public void SaveSkin(int selectedSkin)
    {
        Profile.selectedSkin = selectedSkin;
        Save();
    }
    
    public double GetBestTime(string trackId)
    {
        if (Profile.trackRecords.TryGetValue(trackId, out TrackRecord rec)
            && rec.bestRaceTime < double.MaxValue)
            return rec.bestRaceTime;
        return -1;
    }

    public int GetBestPosition(string trackId)
    {
        if (Profile.trackRecords.TryGetValue(trackId, out TrackRecord rec)
            && rec.bestPosition < int.MaxValue)
            return rec.bestPosition;
        return -1;
    }

    // ── Serialización + XOR ───────────────────────────────────────────────

    public void Save()
    {
        try
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // CRÍTICO: leer en Load() en este mismo orden exacto.
                writer.Write(Profile.nickname);
                writer.Write(Profile.selectedSkin);
                writer.Write(Profile.totalRaces);
                writer.Write(Profile.totalWins);
                writer.Write(Profile.totalSilvers);
                writer.Write(Profile.totalBronzes);
                writer.Write(Profile.totalTrophies);

                writer.Write(Profile.trackRecords.Count);
                foreach (KeyValuePair<string, TrackRecord> kv in Profile.trackRecords)
                {
                    writer.Write(kv.Key);
                    writer.Write(kv.Value.bestRaceTime);
                    writer.Write(kv.Value.bestLapTime);
                    writer.Write(kv.Value.bestPosition);
                    writer.Write(kv.Value.racesCompleted);
                }

                byte[] plain     = ms.ToArray();
                byte[] encrypted = XorCipher(plain);
                File.WriteAllBytes(SavePath, encrypted);
            }
            Logger.Log("[LocalSaveManager] Perfil guardado.");
        }
        catch (Exception e)
        {
            Logger.LogWarning("[LocalSaveManager] Error al guardar: " + e.Message);
        }
    }

    private void Load()
    {
        if (!File.Exists(SavePath))
        {
            Profile = new PlayerProfile();
            return;
        }

        try
        {
            byte[] encrypted = File.ReadAllBytes(SavePath);
            byte[] plain     = XorCipher(encrypted); // XOR es su propia inversa

            using (var ms = new MemoryStream(plain))
            using (var reader = new BinaryReader(ms))
            {
                // CRÍTICO: mismo orden que Save().
                var profile = new PlayerProfile();
                profile.nickname      = reader.ReadString();
                profile.selectedSkin  = reader.ReadInt32();
                profile.totalRaces    = reader.ReadInt32();
                profile.totalWins     = reader.ReadInt32();
                profile.totalSilvers  = reader.ReadInt32();
                profile.totalBronzes  = reader.ReadInt32();
                profile.totalTrophies = reader.ReadInt32();

                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    var rec = new TrackRecord
                    {
                        bestRaceTime   = reader.ReadDouble(),
                        bestLapTime    = reader.ReadSingle(),
                        bestPosition   = reader.ReadInt32(),
                        racesCompleted = reader.ReadInt32()
                    };
                    profile.trackRecords[key] = rec;
                }

                Profile = profile;
            }
            Logger.Log("[LocalSaveManager] Perfil cargado.");
        }
        catch (Exception e)
        {
            Logger.LogWarning("[LocalSaveManager] Archivo corrupto, creando perfil nuevo. " + e.Message);
            Profile = new PlayerProfile();
        }
    }

    // XOR simétrico: aplicar dos veces restaura el original.
    private static byte[] XorCipher(byte[] data)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ EncryptionKey[i % EncryptionKey.Length]);
        return result;
    }
}
