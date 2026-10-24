using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Physical projectile (rockets, plasma bolts). Built from primitives at runtime.</summary>
public class Projectile : MonoBehaviour
{
    public enum Kind { Rocket, Plasma }

    public float damage;
    public float splashRadius;
    public float force = 10f;
    public bool fromPlayer;
    public float gravity;
    public float radius = 0.1f;
    public Color color = Color.white;

    Vector3 velocity, origin;
    float dieAt;
    int mask;
    bool done;
    TrailRenderer[] trails;

    public static Projectile Spawn(Kind kind, Vector3 position, Vector3 direction, float speed, float damage, float splashRadius, bool fromPlayer)
    {
        var lib = FXLibrary.Instance;
        var go = new GameObject(kind == Kind.Rocket ? "Rocket" : "PlasmaBolt");
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        var p = go.AddComponent<Projectile>();
        p.damage = damage;
        p.splashRadius = splashRadius;
        p.fromPlayer = fromPlayer;
        p.velocity = direction.normalized * speed;
        p.origin = position;
        p.dieAt = Time.time + 6f;
        p.mask = fromPlayer ? ~LayerMask.GetMask("Player", "Ignore Raycast") : ~LayerMask.GetMask("Enemy", "Ignore Raycast");

        if (kind == Kind.Rocket)
        {
            p.color = new Color(1f, 0.6f, 0.25f);
            p.radius = 0.15f;
            p.force = 14f;
            Visual(go.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, 0.22f, 0.1f), new Vector3(90, 0, 0), lib ? lib.rocketBody : null);
            Visual(go.transform, PrimitiveType.Sphere, new Vector3(0, 0, 0.22f), new Vector3(0.1f, 0.1f, 0.14f), Vector3.zero, lib ? lib.rocketBody : null);
            Visual(go.transform, PrimitiveType.Sphere, new Vector3(0, 0, -0.24f), Vector3.one * 0.12f, Vector3.zero, lib ? lib.plasmaGlow : null);
            p.trails = new[]
            {
                Trail(go.transform, lib ? lib.smoke : null, 1.2f, 0.25f, 0.9f, new Color(0.75f, 0.75f, 0.75f, 0.5f), new Color(0.5f, 0.5f, 0.5f, 0f)),
                Trail(go.transform, lib ? lib.additive : null, 0.12f, 0.18f, 0.02f, new Color(1f, 0.7f, 0.3f, 1f), new Color(1f, 0.3f, 0.1f, 0f)),
            };
        }
        else
        {
            p.color = new Color(0.3f, 0.8f, 1f);
            p.radius = 0.2f;
            Visual(go.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.28f, Vector3.zero, lib ? lib.plasmaGlow : null);
            p.trails = new[] { Trail(go.transform, lib ? lib.additive : null, 0.25f, 0.25f, 0.02f, new Color(0.4f, 0.85f, 1f, 1f), new Color(0.2f, 0.5f, 1f, 0f)) };
        }

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = p.color;
        light.range = 5f;
        light.intensity = 3f;
        light.shadows = LightShadows.None;
        return p;
    }

    static void Visual(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var v = GameObject.CreatePrimitive(type);
        Destroy(v.GetComponent<Collider>());
        v.transform.SetParent(parent, false);
        v.transform.localPosition = pos;
        v.transform.localRotation = Quaternion.Euler(euler);
        v.transform.localScale = scale;
        var r = v.GetComponent<Renderer>();
        if (mat) r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static TrailRenderer Trail(Transform parent, Material mat, float time, float startWidth, float endWidth, Color start, Color end)
    {
        var go = new GameObject("Trail");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TrailRenderer>();
        t.sharedMaterial = mat;
        t.time = time;
        t.startWidth = startWidth;
        t.endWidth = endWidth;
        t.minVertexDistance = 0.1f;
        t.shadowCastingMode = ShadowCastingMode.Off;
        t.receiveShadows = false;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
        t.colorGradient = g;
        return t;
    }

    void Update()
    {
        if (done) return;
        float dt = Time.deltaTime;
        velocity += Vector3.up * gravity * dt;
        Vector3 step = velocity * dt;
        float dist = step.magnitude;
        if (dist > 0f && Physics.SphereCast(transform.position, radius, step / dist, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
            Hit(hit.point, hit.normal, hit.collider);
            return;
        }
        transform.position += step;
        if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
        if (Time.time > dieAt) Hit(transform.position, -velocity.normalized, null);
    }

    void Hit(Vector3 point, Vector3 normal, Collider col)
    {
        done = true;
        if (splashRadius > 0f)
        {
            Explosion.Explode(point + normal * 0.2f, splashRadius, damage, force, fromPlayer);
        }
        else
        {
            var target = col != null ? col.GetComponentInParent<IDamageable>() : null;
            if (target is PlayerHealth ph) ph.TakeDamage(damage, origin, normal);
            else target?.TakeDamage(damage, point, -velocity.normalized);
            Effects.PlasmaImpact(point, normal, color);
        }

        // Let trails fade out instead of vanishing.
        foreach (var t in trails)
        {
            if (t == null) continue;
            t.transform.SetParent(null, true);
            t.emitting = false;
            t.autodestruct = true;
        }
        Destroy(gameObject);
    }
}
