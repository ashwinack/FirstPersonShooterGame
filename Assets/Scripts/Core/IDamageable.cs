using UnityEngine;

/// <summary>Anything that can be shot. Returns true if this hit killed it.</summary>
public interface IDamageable
{
    bool TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal);
}

/// <summary>
/// A "creature-like" target (enemies, training bots): gets hitmarkers, impact sprays,
/// armor scaling, and lets bullets pass through once dead.
/// </summary>
public interface IHitFeedback
{
    bool IsDead { get; }
    Color ImpactColor { get; }
    float ScaleBodyDamage(float amount);
}
