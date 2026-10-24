using System.Collections.Generic;
using UnityEngine;

/// <summary>Area damage with falloff, physics push, player knockback, effects and sound.</summary>
public static class Explosion
{
    static readonly Collider[] overlap = new Collider[128];

    /// <param name="fromPlayer">Player-caused: hitmarkers count, and the player takes reduced self-damage.</param>
    public static void Explode(Vector3 position, float radius, float damage, float force, bool fromPlayer, float selfDamageScale = 0.35f)
    {
        Effects.Explosion(position, radius);
        ProceduralAudio.PlayAt(ProceduralAudio.ExplosionBoom, position, 1f, 12f);

        int count = Physics.OverlapSphereNonAlloc(position, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
        var done = new HashSet<IDamageable>();
        var pushed = new HashSet<Rigidbody>();
        int worldMask = ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast");
        bool anyHit = false, anyKill = false;

        for (int i = 0; i < count; i++)
        {
            var col = overlap[i];
            if (col == null) continue;

            var rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic && pushed.Add(rb))
                rb.AddExplosionForce(force, position, radius, 0.6f, ForceMode.Impulse);

            var target = col.GetComponentInParent<IDamageable>();
            if (target == null || !done.Add(target)) continue;

            Vector3 closest = col.ClosestPoint(position);
            float dist = Vector3.Distance(closest, position);
            // Walls block the blast.
            if (dist > 0.3f && Physics.Linecast(position, closest, out RaycastHit block, worldMask, QueryTriggerInteraction.Ignore)
                && block.collider != col && block.collider.GetComponentInParent<IDamageable>() != target)
                continue;

            float dmg = damage * (1f - Mathf.Clamp01(dist / radius) * 0.7f);
            Vector3 towardSource = (position - closest).sqrMagnitude > 0.0001f ? (position - closest).normalized : Vector3.up;

            if (target is PlayerHealth ph)
            {
                if (fromPlayer) dmg *= selfDamageScale;
                ph.TakeDamage(dmg, position, Vector3.zero);
                var pc = ph.GetComponent<PlayerController>();
                if (pc != null)
                {
                    Vector3 away = ph.transform.position - position;
                    away.y = 0f;
                    pc.AddImpulse(away.normalized * force * 0.5f * (1f - dist / radius) + Vector3.up * force * 0.35f * (1f - dist / radius));
                }
            }
            else
            {
                bool killed = target.TakeDamage(dmg, closest, towardSource);
                if (fromPlayer && target is IHitFeedback)
                {
                    anyHit = true;
                    anyKill |= killed;
                }
            }
        }

        if (anyHit && HUD.Instance != null) HUD.Instance.ShowHitmarker(anyKill, false);

        var gm = GameManager.Instance;
        if (gm != null && gm.player != null)
        {
            float d = Vector3.Distance(gm.player.transform.position, position);
            gm.player.AddShake(Mathf.Clamp01(1f - d / (radius * 5f)) * 0.9f);
        }
    }
}
