using UnityEngine;

/// <summary>Shoot it and it blows up (chaining into nearby barrels).</summary>
public class ExplosiveBarrel : MonoBehaviour, IDamageable
{
    public float health = 25f;
    public float radius = 5.5f;
    public float damage = 110f;
    public float force = 16f;

    bool exploding;

    public bool TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (exploding) return false;
        health -= amount;
        ProceduralAudio.PlayAt(ProceduralAudio.BarrelHit, hitPoint, 0.5f, 3f);
        if (health <= 0f)
        {
            exploding = true;
            // Small delay so chain reactions ripple instead of popping at once.
            Invoke(nameof(Explode), Random.Range(0.06f, 0.18f));
        }
        return false;
    }

    void Explode()
    {
        Explosion.Explode(transform.position + Vector3.up * 0.4f, radius, damage, force, true);
        Destroy(gameObject);
    }
}
