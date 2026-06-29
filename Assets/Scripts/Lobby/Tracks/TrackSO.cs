using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Track", menuName = "Tracks/Track")]
public class TrackSO : ScriptableObject
{
    public int TrackID;
    public string TrackName;
    public string TrackSceneName;
    public Sprite TrackImage;
}