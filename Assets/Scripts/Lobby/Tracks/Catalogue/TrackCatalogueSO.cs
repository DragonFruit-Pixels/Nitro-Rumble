using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Track Catalogue", menuName = "Tracks/Track Catalogue")]
public class TrackCatalogueSO : ScriptableObject
{
    public List<TrackSO> Tracks = new List<TrackSO>();

    // Se construye en forma diferida y también en OnEnable/OnValidate, de modo que
    // GetTrack/HasTrack funcionen TAMBIÉN en build (OnValidate solo corre en el editor).
    private Dictionary<int, TrackSO> _tracksByID;

    private Dictionary<int, TrackSO> Lookup
    {
        get
        {
            if (_tracksByID == null) RebuildLookup();
            return _tracksByID;
        }
    }

    public TrackSO GetTrack(int trackID) =>
        Lookup.TryGetValue(trackID, out TrackSO track) ? track : null;

    public bool HasTrack(int trackID) => Lookup.ContainsKey(trackID);

    private void RebuildLookup()
    {
        _tracksByID = new Dictionary<int, TrackSO>();
        foreach (TrackSO track in Tracks)
        {
            if (track == null) continue;
            // Indexador (no Add) para tolerar IDs duplicados sin lanzar excepción.
            _tracksByID[track.TrackID] = track;
        }
    }

    private void OnEnable()   => RebuildLookup();
    private void OnValidate() => RebuildLookup();
}