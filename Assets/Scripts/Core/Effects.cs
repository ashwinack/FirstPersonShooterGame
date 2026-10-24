using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Spawns short-lived visual effects: tracers, impacts, blood, shells, smoke, decals.</summary>
public static class Effects
{
    const int MaxDecals = 80;
    const int MaxShells = 40;
    static readonly Queue<GameObject> decals = new Queue<GameObject>();
    static readonly List<ShellCasing> shells = new List<ShellCasing>();
    static int shellIndex;

    public static readonly Color SparkColor = new Color(1f, 0.75f, 0.35f);
    public static readonly Color DustColor = new Color(0.55f, 0.53f, 0.5f, 0.6f);
    public static readonly Color BloodColor = new Color(0.55f, 0.03f, 0.03f);
    public static readonly Color OilColor = new Color(0.08f, 0.07f, 0.06f);

    static FXLibrary Lib => FXLibrary.Instance;

    // ---------- Tracers ----------

    public static void Tracer(Vector3 from, Vector3 to, bool enemy = false)
    {
        if (Lib == null) return;
        var go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = enemy ? 0.06f : 0.03f;
        lr.endWidth = enemy ? 0.03f : 0.008f;
        lr.sharedMaterial = enemy ? Lib.enemyTracer : Lib.tracer;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        go.AddComponent<TracerMover>().Init(from, to, enemy ? 120f : 380f, enemy ? 3f : 5f);
    }

    // ---------- Impacts ----------

    /// Bullet hitting the world: sparks, dust puff, debris, bullet hole.
    public static void ImpactWorld(Vector3 point, Vector3 normal, bool decal = true, bool sound = false)
    {
        if (Lib == null) return;
        var rot = Rot(normal);
        Burst(new BurstDesc
        {
            position = point + normal * 0.02f, rotation = rot, material = Lib.additive, count = 10,
            life = new Vector2(0.08f, 0.25f), speed = new Vector2(3f, 9f), size = new Vector2(0.02f, 0.05f),
            colorA = SparkColor, colorB = new Color(1f, 0.9f, 0.6f), gravity = 2f, cone = 40f, shrink = true, stretch = true,
        });
        Burst(new BurstDesc
        {
            position = point + normal * 0.05f, rotation = rot, material = Lib.smoke, count = 3,
            life = new Vector2(0.5f, 0.9f), speed = new Vector2(0.4f, 1.2f), size = new Vector2(0.2f, 0.35f),
            colorA = DustColor, colorB = DustColor * 0.8f, gravity = -0.05f, cone = 25f, grow = true, fade = true, drag = 3f,
        });
        Burst(new BurstDesc
        {
            position = point + normal * 0.02f, rotation = rot, material = Lib.particle, count = 4,
            life = new Vector2(0.4f, 0.8f), speed = new Vector2(1.5f, 4f), size = new Vector2(0.015f, 0.035f),
            colorA = new Color(0.35f, 0.33f, 0.3f), colorB = new Color(0.2f, 0.19f, 0.18f), gravity = 1.5f, cone = 50f, collide = true,
        });
        if (decal) Decal(Lib.bulletHole, point, normal, Random.Range(0.07f, 0.11f));
        if (sound) ProceduralAudio.PlayAt(ProceduralAudio.ImpactWorld, point, 0.4f, 2f);
    }

    /// Bullet hitting an enemy: oil/blood spray, sparks off armor, splat on a surface behind.
    public static void ImpactFlesh(Vector3 point, Vector3 normal, Vector3 shotDir, Color tint)
    {
        if (Lib == null) return;
        Burst(new BurstDesc
        {
            position = point, rotation = Rot(Vector3.Lerp(normal, shotDir, 0.5f).normalized), material = Lib.particle, count = 12,
            life = new Vector2(0.3f, 0.6f), speed = new Vector2(1.5f, 5f), size = new Vector2(0.025f, 0.06f),
            colorA = tint, colorB = tint * 0.6f, gravity = 2f, cone = 35f, shrink = true, collide = true,
        });
        Burst(new BurstDesc
        {
            position = point, rotation = Rot(normal), material = Lib.additive, count = 5,
            life = new Vector2(0.05f, 0.15f), speed = new Vector2(2f, 6f), size = new Vector2(0.02f, 0.04f),
            colorA = SparkColor, colorB = Color.white, gravity = 1f, cone = 60f, shrink = true, stretch = true,
        });

        // Splatter onto whatever is behind the target.
        if (Physics.Raycast(point + shotDir * 0.3f, shotDir, out RaycastHit hit, 4f, ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast"), QueryTriggerInteraction.Ignore)
            && hit.rigidbody == null)
            Decal(Lib.bloodDecal, hit.point, hit.normal, Random.Range(0.25f, 0.5f));
    }

    /// Big burst when an enemy is destroyed.
    public static void DeathBurst(Vector3 center, Color bodyColor, Color glowColor)
    {
        if (Lib == null) return;
        Burst(new BurstDesc
        {
            position = center, rotation = Quaternion.LookRotation(Vector3.up), material = Lib.particle, count = 30,
            life = new Vector2(0.8f, 1.6f), speed = new Vector2(2f, 7f), size = new Vector2(0.05f, 0.14f),
            colorA = bodyColor, colorB = bodyColor * 0.5f, gravity = 2f, cone = 80f, collide = true, shrink = true,
        });
        Burst(new BurstDesc
        {
            position = center, rotation = Quaternion.LookRotation(Vector3.up), material = Lib.additive, count = 25,
            life = new Vector2(0.3f, 0.8f), speed = new Vector2(3f, 10f), size = new Vector2(0.03f, 0.08f),
            colorA = glowColor, colorB = Color.white, gravity = 1.2f, cone = 90f, shrink = true, stretch = true,
        });
        Burst(new BurstDesc
        {
            position = center, rotation = Quaternion.LookRotation(Vector3.up), material = Lib.smoke, count = 8,
            life = new Vector2(1f, 1.8f), speed = new Vector2(0.5f, 1.8f), size = new Vector2(0.6f, 1.1f),
            colorA = new Color(0.2f, 0.2f, 0.2f, 0.7f), colorB = new Color(0.35f, 0.33f, 0.3f, 0.5f), gravity = -0.1f, cone = 70f,
            grow = true, fade = true, drag = 2f,
        });
        if (Physics.Raycast(center, Vector3.down, out RaycastHit hit, 3f, ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast"), QueryTriggerInteraction.Ignore))
        {
            Decal(Lib.scorch, hit.point, hit.normal, Random.Range(1.2f, 1.8f));
            Decal(Lib.bloodDecal, hit.point + Random.insideUnitSphere * 0.3f, hit.normal, Random.Range(0.8f, 1.3f));
        }
    }

    /// <summary>Fireball, smoke column, debris, sparks, shockwave, flash light and scorch.</summary>
    public static void Explosion(Vector3 center, float radius)
    {
        if (Lib == null) return;
        float s = Mathf.Max(0.5f, radius / 5f);
        var up = Quaternion.LookRotation(Vector3.up);
        Burst(new BurstDesc
        {
            position = center, rotation = up, material = Lib.additive, count = 1,
            life = new Vector2(0.12f, 0.12f), speed = Vector2.zero, size = new Vector2(radius * 1.6f, radius * 1.6f),
            colorA = new Color(1f, 0.85f, 0.6f), colorB = new Color(1f, 0.85f, 0.6f), cone = 0f, shrink = true,
        });
        Burst(new BurstDesc
        {
            position = center, rotation = up, material = Lib.additive, count = 28,
            life = new Vector2(0.25f, 0.6f), speed = new Vector2(2f, 7f) * s, size = new Vector2(0.8f, 2.2f) * s,
            colorA = new Color(1f, 0.55f, 0.15f), colorB = new Color(1f, 0.85f, 0.4f), gravity = -0.3f, cone = 90f, shrink = true, drag = 4f,
        });
        Burst(new BurstDesc
        {
            position = center, rotation = up, material = Lib.smoke, count = 16,
            life = new Vector2(1.6f, 3.2f), speed = new Vector2(1f, 4f) * s, size = new Vector2(1.2f, 2.6f) * s,
            colorA = new Color(0.12f, 0.11f, 0.1f, 0.85f), colorB = new Color(0.3f, 0.28f, 0.26f, 0.6f), gravity = -0.25f, cone = 70f,
            grow = true, fade = true, drag = 2.5f,
        });
        Burst(new BurstDesc
        {
            position = center, rotation = up, material = Lib.additive, count = 40,
            life = new Vector2(0.3f, 0.9f), speed = new Vector2(6f, 18f) * s, size = new Vector2(0.03f, 0.08f),
            colorA = SparkColor, colorB = Color.white, gravity = 1.5f, cone = 90f, shrink = true, stretch = true,
        });
        Burst(new BurstDesc
        {
            position = center, rotation = up, material = Lib.particle, count = 24,
            life = new Vector2(1f, 2f), speed = new Vector2(4f, 11f) * s, size = new Vector2(0.06f, 0.16f),
            colorA = new Color(0.15f, 0.14f, 0.13f), colorB = new Color(0.35f, 0.32f, 0.3f), gravity = 2f, cone = 75f, collide = true,
        });
        Shockwave(center, radius * 1.4f, new Color(1f, 0.7f, 0.4f, 0.8f));

        var lightGO = new GameObject("ExplosionLight");
        lightGO.transform.position = center + Vector3.up * 0.5f;
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.3f);
        light.range = radius * 4f;
        light.intensity = 12f;
        lightGO.AddComponent<FadingLight>().duration = 0.45f;

        if (Physics.Raycast(center + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, radius, ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast"), QueryTriggerInteraction.Ignore))
            Decal(Lib.scorch, hit.point, hit.normal, radius * 0.9f);
    }

    /// <summary>Flat expanding ring along the ground (explosions, brute slams).</summary>
    public static void Shockwave(Vector3 center, float radius, Color color)
    {
        if (Lib == null || Lib.ring == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Shockwave";
        go.transform.SetPositionAndRotation(center + Vector3.up * 0.1f, Quaternion.Euler(90f, 0f, 0f));
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = Lib.ring;
        r.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<ShockwaveRing>().Init(radius * 2f, 0.4f, color);
    }

    public static void PlasmaImpact(Vector3 point, Vector3 normal, Color color)
    {
        if (Lib == null) return;
        Burst(new BurstDesc
        {
            position = point + normal * 0.05f, rotation = Rot(normal), material = Lib.additive, count = 14,
            life = new Vector2(0.15f, 0.4f), speed = new Vector2(2f, 6f), size = new Vector2(0.04f, 0.1f),
            colorA = color, colorB = Color.white, gravity = 0.5f, cone = 60f, shrink = true, stretch = true,
        });
        Burst(new BurstDesc
        {
            position = point + normal * 0.05f, rotation = Rot(normal), material = Lib.additive, count = 1,
            life = new Vector2(0.12f, 0.12f), speed = Vector2.zero, size = new Vector2(0.8f, 0.8f),
            colorA = color, colorB = color, shrink = true,
        });
        Decal(Lib.scorch, point, normal, 0.4f);
        ProceduralAudio.PlayAt(ProceduralAudio.PlasmaImpact, point, 0.5f, 3f);
    }

    /// <summary>Glowing column when an enemy materializes.</summary>
    public static void SpawnFlash(Vector3 position, Color color)
    {
        if (Lib == null) return;
        Burst(new BurstDesc
        {
            position = position, rotation = Quaternion.LookRotation(Vector3.up), material = Lib.additive, count = 24,
            life = new Vector2(0.4f, 0.8f), speed = new Vector2(1f, 5f), size = new Vector2(0.05f, 0.15f),
            colorA = color, colorB = Color.white, gravity = -0.5f, cone = 15f, shrink = true, stretch = true,
        });
        Shockwave(position, 1.5f, color);
    }

    /// Lingering puff at the muzzle.
    public static void MuzzleSmoke(Vector3 position, Vector3 forward, float amount = 1f)
    {
        if (Lib == null) return;
        Burst(new BurstDesc
        {
            position = position, rotation = Rot(forward), material = Lib.smoke, count = Mathf.Max(1, Mathf.RoundToInt(3 * amount)),
            life = new Vector2(0.4f, 0.9f), speed = new Vector2(0.3f, 1.2f * amount), size = new Vector2(0.06f, 0.12f) * Mathf.Sqrt(amount),
            colorA = new Color(0.8f, 0.8f, 0.8f, 0.35f), colorB = new Color(0.6f, 0.6f, 0.6f, 0.25f), gravity = -0.15f, cone = 15f,
            grow = true, fade = true, drag = 4f,
        });
    }

    // ---------- Shells ----------

    public static void EjectShell(Vector3 position, Quaternion rotation, Vector3 velocity, bool shotgunShell)
    {
        if (Lib == null) return;
        ShellCasing shell;
        if (shells.Count < MaxShells)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Shell";
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            Object.Destroy(go.GetComponent<Collider>());
            go.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.02f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.5f;
            shell = go.AddComponent<ShellCasing>();
            shells.Add(shell);
        }
        else
        {
            shellIndex = (shellIndex + 1) % shells.Count;
            shell = shells[shellIndex];
            if (shell == null)
            {
                shells.RemoveAt(shellIndex);
                return;
            }
        }
        shell.Launch(position, rotation, velocity, shotgunShell, shotgunShell ? Lib.shotgunShell : Lib.brass);
    }

    // ---------- Decals ----------

    public static void Decal(Material mat, Vector3 point, Vector3 normal, float size)
    {
        if (mat == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Decal";
        // A Quad's visible face points down -Z, so aim -Z along the surface normal.
        go.transform.SetPositionAndRotation(point + normal * (0.004f + decals.Count * 0.00002f),
            Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        go.transform.localScale = new Vector3(size, size * Random.Range(0.8f, 1.2f), 1f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;

        decals.Enqueue(go);
        while (decals.Count > MaxDecals)
        {
            var old = decals.Dequeue();
            if (old != null) Object.Destroy(old);
        }
    }

    // ---------- Particle helper ----------

    public struct BurstDesc
    {
        public Vector3 position;
        public Quaternion rotation;
        public Material material;
        public int count;
        public Vector2 life, speed, size;
        public Color colorA, colorB;
        public float gravity, cone, drag;
        public bool shrink, grow, fade, stretch, collide;
    }

    public static void Burst(BurstDesc d)
    {
        if (d.material == null || d.count <= 0) return;
        var go = new GameObject("FX");
        go.transform.SetPositionAndRotation(d.position, d.rotation);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(d.life.x, d.life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(d.speed.x, d.speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(d.size.x, d.size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(d.colorA, d.colorB);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = d.gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = d.count;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)d.count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = d.cone;
        shape.radius = 0.02f;

        if (d.shrink || d.grow)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, d.grow ? AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.2f) : AnimationCurve.Linear(0f, 1f, 1f, 0f));
        }

        if (d.fade)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        if (d.drag > 0f)
        {
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.drag = d.drag;
            lim.multiplyDragByParticleSize = false;
        }

        if (d.collide)
        {
            var c = ps.collision;
            c.enabled = true;
            c.type = ParticleSystemCollisionType.World;
            c.mode = ParticleSystemCollisionMode.Collision3D;
            c.bounce = 0.3f;
            c.dampen = 0.5f;
            c.collidesWith = ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast");
            c.quality = ParticleSystemCollisionQuality.Medium;
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = d.material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (d.stretch)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1f;
        }

        ps.Play();
        Object.Destroy(go, d.life.y + 0.2f);
    }

    static Quaternion Rot(Vector3 dir) => dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
}

/// <summary>Fades a light's intensity to zero, then destroys it.</summary>
public class FadingLight : MonoBehaviour
{
    public float duration = 0.4f;
    Light lightRef;
    float start, startIntensity;

    void Start()
    {
        lightRef = GetComponent<Light>();
        start = Time.time;
        startIntensity = lightRef.intensity;
    }

    void Update()
    {
        float t = (Time.time - start) / duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        lightRef.intensity = startIntensity * (1f - t) * (1f - t);
    }
}

/// <summary>Expands and fades a ring quad.</summary>
public class ShockwaveRing : MonoBehaviour
{
    float size, duration, start;
    Color color;
    Renderer rend;
    MaterialPropertyBlock mpb;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public void Init(float finalSize, float time, Color c)
    {
        size = finalSize;
        duration = time;
        color = c;
        start = Time.time;
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        Apply(0f);
    }

    void Update()
    {
        float t = (Time.time - start) / duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Apply(t);
    }

    void Apply(float t)
    {
        float e = 1f - (1f - t) * (1f - t);
        transform.localScale = Vector3.one * Mathf.Lerp(0.2f, size, e);
        var c = color;
        c.a *= 1f - t;
        mpb.SetColor(BaseColorId, c * 3f);
        rend.SetPropertyBlock(mpb);
    }
}

/// <summary>Moves a short bright streak from muzzle to impact point.</summary>
public class TracerMover : MonoBehaviour
{
    LineRenderer line;
    Vector3 from, dir;
    float total, speed, length, head;

    public void Init(Vector3 start, Vector3 end, float metersPerSecond, float streakLength)
    {
        line = GetComponent<LineRenderer>();
        from = start;
        Vector3 d = end - start;
        total = d.magnitude;
        dir = total > 0.001f ? d / total : Vector3.forward;
        speed = metersPerSecond;
        length = streakLength;
        head = Mathf.Min(total, length * 0.6f);
        Apply();
    }

    void Update()
    {
        head += speed * Time.deltaTime;
        if (head - length >= total)
        {
            Destroy(gameObject);
            return;
        }
        Apply();
    }

    void Apply()
    {
        float h = Mathf.Min(head, total), t = Mathf.Clamp(head - length, 0f, total);
        line.SetPosition(0, from + dir * t);
        line.SetPosition(1, from + dir * h);
    }
}

/// <summary>Pooled ejected casing; clinks on its first bounces.</summary>
public class ShellCasing : MonoBehaviour
{
    Rigidbody rb;
    Renderer rend;
    float despawnAt;
    int bounces;
    bool isShotgun;

    public void Launch(Vector3 position, Quaternion rotation, Vector3 velocity, bool shotgunShell, Material mat)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            rend = GetComponent<Renderer>();
        }
        gameObject.SetActive(true);
        isShotgun = shotgunShell;
        transform.localScale = shotgunShell ? new Vector3(0.022f, 0.032f, 0.022f) : new Vector3(0.011f, 0.02f, 0.011f);
        rend.sharedMaterial = mat;
        rb.position = position;
        rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        rb.linearVelocity = velocity;
        rb.angularVelocity = Random.insideUnitSphere * 25f;
        bounces = 0;
        despawnAt = Time.time + 6f;
    }

    void OnCollisionEnter(Collision c)
    {
        if (bounces < 3 && c.relativeVelocity.magnitude > 0.8f)
            ProceduralAudio.PlayAt(isShotgun ? ProceduralAudio.ShotgunShellDrop : ProceduralAudio.ShellTink,
                transform.position, 0.3f / (bounces + 1), 1.5f);
        bounces++;
    }

    void Update()
    {
        if (Time.time > despawnAt) gameObject.SetActive(false);
    }
}
