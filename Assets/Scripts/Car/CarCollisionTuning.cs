using UnityEngine;

[CreateAssetMenu(menuName = "Racing/Car Collision Tuning")]
public class CarCollisionTuning : ScriptableObject
{
    [Header("Impact")]
    [SerializeField] private float _minImpactSpeed = 1.5f;
    [SerializeField] private float _impulseStrength = 7f;
    [SerializeField] private float _sideImpulseMultiplier = 0.75f;
    [SerializeField] private float _separationImpulse = 2f;
    [SerializeField] private float _stayImpulseMultiplier = 0.18f;
    [SerializeField] private float _maxImpulse = 12f;

    public float MinImpactSpeed => _minImpactSpeed;
    public float ImpulseStrength => _impulseStrength;
    public float SideImpulseMultiplier => _sideImpulseMultiplier;
    public float SeparationImpulse => _separationImpulse;
    public float StayImpulseMultiplier => _stayImpulseMultiplier;
    public float MaxImpulse => _maxImpulse;

    public float GetImpulseMagnitude(float closingSpeed, CarImpactSide side, bool isStay)
    {
        float impulseMagnitude = Mathf.Clamp(
            closingSpeed * _impulseStrength + _separationImpulse,
            _separationImpulse,
            _maxImpulse
        );

        if (side == CarImpactSide.Left || side == CarImpactSide.Right)
            impulseMagnitude *= _sideImpulseMultiplier;

        if (isStay)
            impulseMagnitude *= _stayImpulseMultiplier;

        return impulseMagnitude;
    }
}
