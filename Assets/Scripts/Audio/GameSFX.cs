using UnityEngine;

[System.Serializable]
public struct SFXEntry
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume;

    public void Play(AudioSource source)
    {
        if (source != null && clip != null)
            source.PlayOneShot(clip, volume);
    }
}

[System.Serializable]
public struct EngineAudioSettings
{
    [Range(0.1f, 1f)]  public float minPitch;
    [Range(1f,   5f)]  public float maxPitch;
    [Range(0f,   1f)]  public float minVolume;
    [Range(0f,   1f)]  public float maxVolume;
}

[System.Serializable]
public struct ScreechAudioSettings
{
    [Range(0f, 1f)] public float driftThreshold;
}

[System.Serializable]
public struct ImpactAudioSettings
{
    [Min(0f)] public float minSpeed;
    [Min(0f)] public float maxSpeed;
}

[CreateAssetMenu(fileName = "GameSFX", menuName = "Game/SFX Settings")]
public class GameSFX : ScriptableObject
{
    [Header("Countdown")]
    public SFXEntry countdownBeep;
    public SFXEntry countdownGo;

    [Header("Combat")]
    public SFXEntry carExplosion;
    public SFXEntry carImpact;
    public SFXEntry wallSparks;

    [Header("Power-ups")]
    public SFXEntry turboActivate;
    public SFXEntry empFire;
    public SFXEntry empHit;
    public SFXEntry shieldActivate;
    public SFXEntry shieldBlock;

    [Header("Race")]
    public SFXEntry checkpointPass;
    public SFXEntry lapComplete;
    public SFXEntry raceFinish;

    [Header("UI")]
    public SFXEntry uiClick;

    [Header("Car - Engine")]
    public EngineAudioSettings engine;

    [Header("Car - Screech / Drift")]
    public ScreechAudioSettings screech;

    [Header("Car - Impact")]
    public ImpactAudioSettings impact;

    private static GameSFX _instance;
    public static GameSFX Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GameSFX>("GameSFX");
            return _instance;
        }
    }
}
