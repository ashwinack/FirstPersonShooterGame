using UnityEngine;
using UnityEngine.AI;

/// <summary>NavMesh enemy with several attack styles: melee, hitscan, projectile, kamikaze explosion, ground slam.</summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour, IDamageable, IHitFeedback
{
    public enum AttackType { Melee, Hitscan, Projectile, Explode, Slam }

    [Header("Identity")]
    public string displayName = "Grunt";
    public bool isBoss;

    [Header("Stats")]
    public float maxHealth = 60f;
    public float moveSpeed = 4.5f;
    public int scoreValue = 100;
    [Tooltip("Damage multiplier for hits that aren't on a Hitbox (armor).")]
    public float bodyDamageMultiplier = 1f;

    [Header("Attack")]
    public AttackType attackType = AttackType.Melee;
    public float attackRange = 1.9f;
    public float attackDamage = 12f;
    public float attackCooldown = 1f;
    public float attackWindup = 0.3f;
    [Tooltip("Ranged types hold position at this distance when they can see the player.")]
    public float preferredDistance = 14f;
    public float rangedSpread = 3f;
    public float projectileSpeed = 18f;
    [Tooltip("Explode / Slam radius.")]
    public float splashRadius = 4f;

    [Header("Movement")]
    [Tooltip("Keeps circling the player at preferredDistance instead of standing still (drones).")]
    public bool strafe;

    [Header("References")]
    public Transform eye;
    public Transform weaponMuzzle;
    public Renderer[] renderers;
    [Tooltip("Pieces that detach and fly apart on death.")]
    public Transform[] breakableParts;

    [Header("Look & Sound")]
    public Color bloodColor = new Color(0.08f, 0.07f, 0.06f);
    public Color glowColor = new Color(1f, 0.8f, 0.2f);
    public float voicePitch = 1f;
    public bool humLoop;

    [Header("Drops")]
    [Range(0f, 1f)] public float dropChance = 0.25f;

    public float Health { get; private set; }
    public bool IsDead { get; private set; }
    public Color ImpactColor => bloodColor;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    NavMeshAgent agent;
    EnemyAnimator anim;
    Transform player;
    PlayerHealth playerHealth;
    MaterialPropertyBlock mpb;
    Color[] baseColors;
    float nextAttackTime, repathTimer, flashTimer, windupEnd = -1f, losTimer, hurtSoundTime, staggerUntil, nextBeep, strafeAngle, strafeTimer, behindTime;
    bool flashing, cachedLos, killedByPlayer = true;
    int losMask;

    bool IsRanged => attackType == AttackType.Hitscan || attackType == AttackType.Projectile;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<EnemyAnimator>();
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        Health = maxHealth;
        attackDamage *= GameSettings.EnemyDamageMult;
        agent.speed = moveSpeed;
        agent.stoppingDistance = IsRanged ? 1f : attackRange * 0.6f;
        strafeAngle = Random.Range(0f, 360f);

        var gm = GameManager.Instance;
        if (gm != null && gm.player != null)
        {
            player = gm.player.transform;
            playerHealth = gm.playerHealth;
        }

        losMask = ~LayerMask.GetMask("Enemy", "Ignore Raycast");
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = renderers[i].sharedMaterial;
            baseColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
        }
        nextAttackTime = Time.time + Random.Range(0.8f, 1.6f);

        if (anim != null) anim.Footstep = OnFootstep;
        if (humLoop) ProceduralAudio.AttachLoop(transform, ProceduralAudio.DroneHum, 0.5f);
        var voice = isBoss ? ProceduralAudio.BossRoar : voicePitch > 1.3f ? ProceduralAudio.RunnerScreech : ProceduralAudio.EnemyGrowl;
        ProceduralAudio.PlayAt(voice, transform.position + Vector3.up, isBoss ? 1f : 0.7f, isBoss ? 15f : 6f, isBoss ? 1f : voicePitch);
        Effects.SpawnFlash(transform.position, glowColor);
    }

    void OnFootstep()
    {
        if (player != null && (player.position - transform.position).sqrMagnitude < 15f * 15f)
            ProceduralAudio.PlayAt(ProceduralAudio.EnemyStep, transform.position, isBoss ? 0.8f : 0.35f, isBoss ? 6f : 2f, isBoss ? 0.6f : voicePitch);
        if (isBoss && player != null)
        {
            float d = Vector3.Distance(player.position, transform.position);
            GameManager.Instance?.player.AddShake(Mathf.Clamp01(1f - d / 18f) * 0.12f);
        }
    }

    void Update()
    {
        if (IsDead) return;
        UpdateFlash();

        if (player == null || playerHealth == null || playerHealth.IsDead)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            if (anim) anim.SetWindup(-1f);
            return;
        }

        // Front-facing shooter: an enemy that ends up behind the player's view (walked around, knocked back)
        // is moved back to the front arc after a moment. It isn't a kill and nothing is dropped.
        var gm = GameManager.Instance;
        if (gm != null && gm.IsBehindPlayer(transform.position))
        {
            behindTime += Time.deltaTime;
            if (behindTime >= gm.BehindRespawnDelay && RelocateToFront(gm)) return;
        }
        else behindTime = 0f;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        losTimer -= Time.deltaTime;
        if (losTimer <= 0f)
        {
            cachedLos = HasLineOfSight();
            losTimer = 0.1f;
        }
        bool inRange = dist <= attackRange && cachedLos;
        bool staggered = Time.time < staggerUntil;

        if (agent.isOnNavMesh)
        {
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                agent.SetDestination(Destination());
                repathTimer = strafe ? 0.5f : 0.25f;
            }
            bool holdPosition = !strafe && IsRanged && cachedLos && dist <= preferredDistance;
            agent.isStopped = staggered || windupEnd > 0f || holdPosition;
        }

        if (inRange || (IsRanged && cachedLos && dist <= preferredDistance + 2f)) FaceTowards(toPlayer);

        if (windupEnd > 0f)
        {
            float t = 1f - Mathf.Clamp01((windupEnd - Time.time) / attackWindup);
            if (anim) anim.SetWindup(t);
            if (attackType == AttackType.Explode) Beep(t);
            if (Time.time >= windupEnd)
            {
                windupEnd = -1f;
                if (anim)
                {
                    anim.SetWindup(-1f);
                    anim.PlayStrike();
                }
                PerformAttack();
            }
        }
        else if (inRange && !staggered && Time.time >= nextAttackTime)
        {
            windupEnd = Time.time + attackWindup;
            nextAttackTime = windupEnd + attackCooldown * Random.Range(0.85f, 1.2f);
            nextBeep = 0f;
            if (attackType == AttackType.Slam) ProceduralAudio.PlayAt(ProceduralAudio.BossRoar, transform.position + Vector3.up * 2f, 0.6f, 10f, 1.3f);
        }
    }

    bool RelocateToFront(GameManager gm)
    {
        if (!agent.isOnNavMesh || !gm.TryGetFrontSpawnPosition(out Vector3 pos)) return false;
        behindTime = 0f;
        windupEnd = -1f;
        if (anim) anim.SetWindup(-1f);
        agent.Warp(pos);
        Vector3 look = player.position - pos;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
        repathTimer = 0f;
        Effects.SpawnFlash(pos, glowColor);
        return true;
    }

    Vector3 Destination()
    {
        if (!strafe) return player.position;
        // Orbit the player at preferred range, drifting around over time.
        strafeTimer -= 0.5f;
        if (strafeTimer <= 0f)
        {
            strafeAngle += Random.Range(-70f, 70f);
            strafeTimer = Random.Range(2f, 4f);
        }
        // ...but only across the front arc, never round the back.
        var gm = GameManager.Instance;
        if (gm != null)
        {
            float half = gm.SpawnArcAngle * 0.5f;
            strafeAngle = gm.FrontYaw + Mathf.Clamp(Mathf.DeltaAngle(gm.FrontYaw, strafeAngle), -half, half);
        }
        Vector3 offset = Quaternion.Euler(0f, strafeAngle, 0f) * Vector3.forward * preferredDistance;
        Vector3 target = player.position + offset;
        return NavMesh.SamplePosition(target, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : player.position;
    }

    void Beep(float t)
    {
        if (Time.time < nextBeep) return;
        nextBeep = Time.time + Mathf.Lerp(0.25f, 0.06f, t);
        ProceduralAudio.PlayAt(ProceduralAudio.Beep, transform.position + Vector3.up, 0.7f, 4f, 1f + t * 0.5f);
        flashTimer = 0.05f;
    }

    void FaceTowards(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * Time.deltaTime);
    }

    bool HasLineOfSight()
    {
        Vector3 origin = eye ? eye.position : transform.position + Vector3.up * 1.6f;
        Vector3 target = player.position + Vector3.up * 1.4f;
        Vector3 d = target - origin;
        float len = d.magnitude;
        if (Physics.Raycast(origin, d / len, out RaycastHit hit, len + 0.5f, losMask, QueryTriggerInteraction.Ignore))
            return hit.collider.transform.IsChildOf(player);
        return true;
    }

    void PerformAttack()
    {
        if (playerHealth == null || playerHealth.IsDead) return;
        Vector3 flatToPlayer = player.position - transform.position;
        flatToPlayer.y = 0f;

        switch (attackType)
        {
            case AttackType.Melee:
                ProceduralAudio.PlayAt(ProceduralAudio.Swipe, transform.position + Vector3.up, 0.8f, 4f, voicePitch);
                if (flatToPlayer.magnitude <= attackRange + 0.4f)
                {
                    playerHealth.TakeDamage(attackDamage, transform.position, Vector3.zero);
                    ProceduralAudio.Play2D(ProceduralAudio.MeleeHit, 0.7f);
                }
                break;

            case AttackType.Hitscan:
                FireHitscan();
                break;

            case AttackType.Projectile:
            {
                Vector3 origin = weaponMuzzle ? weaponMuzzle.position : eye.position;
                // Lead the target a little.
                var pc = player.GetComponent<PlayerController>();
                Vector3 target = player.position + Vector3.up * 1.2f;
                if (pc != null) target += pc.Velocity * (Vector3.Distance(origin, target) / projectileSpeed) * 0.6f;
                Vector3 dir = Quaternion.Euler(Random.Range(-rangedSpread, rangedSpread), Random.Range(-rangedSpread, rangedSpread), 0f) * (target - origin).normalized;
                Projectile.Spawn(Projectile.Kind.Plasma, origin, dir, projectileSpeed, attackDamage, 0f, false);
                ProceduralAudio.PlayAt(ProceduralAudio.PlasmaShot, origin, 0.8f, 5f);
                break;
            }

            case AttackType.Explode:
                killedByPlayer = false;
                Die(Vector3.up, transform.position + Vector3.up);
                break;

            case AttackType.Slam:
            {
                Vector3 center = transform.position + transform.forward * 1.2f;
                Effects.Shockwave(center, splashRadius, new Color(1f, 0.5f, 0.3f, 0.9f));
                Effects.Burst(new Effects.BurstDesc
                {
                    position = center, rotation = Quaternion.LookRotation(Vector3.up), material = FXLibrary.Instance ? FXLibrary.Instance.smoke : null,
                    count = 14, life = new Vector2(0.6f, 1.2f), speed = new Vector2(2f, 6f), size = new Vector2(0.5f, 1.1f),
                    colorA = Effects.DustColor, colorB = Effects.DustColor * 0.8f, cone = 85f, grow = true, fade = true, drag = 3f,
                });
                ProceduralAudio.PlayAt(ProceduralAudio.Slam, center, 1f, 10f);
                Vector3 toP = player.position - center;
                float d = new Vector2(toP.x, toP.z).magnitude;
                if (d <= splashRadius && Mathf.Abs(toP.y) < 2.5f)
                {
                    playerHealth.TakeDamage(attackDamage * (1f - d / splashRadius * 0.5f), transform.position, Vector3.zero);
                    var pc = player.GetComponent<PlayerController>();
                    if (pc != null) pc.AddImpulse(new Vector3(toP.x, 0f, toP.z).normalized * 9f + Vector3.up * 6f);
                }
                GameManager.Instance?.player.AddShake(Mathf.Clamp01(1f - d / 20f) * 0.7f);
                break;
            }
        }
    }

    void FireHitscan()
    {
        Vector3 origin = weaponMuzzle ? weaponMuzzle.position : eye.position;
        Vector3 dir = (player.position + Vector3.up * 1.2f - origin).normalized;
        dir = Quaternion.Euler(Random.Range(-rangedSpread, rangedSpread), Random.Range(-rangedSpread, rangedSpread), 0f) * dir;
        float maxDist = attackRange * 1.5f;
        Vector3 end = origin + dir * maxDist;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, losMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            var ph = hit.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(attackDamage, transform.position, hit.normal);
            else Effects.ImpactWorld(hit.point, hit.normal, true, false);
        }

        if (weaponMuzzle)
        {
            Effects.Burst(new Effects.BurstDesc
            {
                position = origin, rotation = Quaternion.LookRotation(dir), material = FXLibrary.Instance ? FXLibrary.Instance.additive : null,
                count = 6, life = new Vector2(0.04f, 0.08f), speed = new Vector2(1f, 4f), size = new Vector2(0.1f, 0.25f),
                colorA = new Color(1f, 0.4f, 0.3f), colorB = new Color(1f, 0.8f, 0.5f), cone = 20f, shrink = true,
            });
            Effects.MuzzleSmoke(origin, dir, 0.8f);
        }
        Effects.Tracer(origin, end, true);
        ProceduralAudio.PlayAt(ProceduralAudio.EnemyShot, origin, 0.8f);
    }

    public bool TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead) return false;
        Health -= amount;
        flashTimer = 0.08f;

        // hitNormal points back toward the shooter, so the shot travels along -hitNormal.
        Vector3 shotDir = -hitNormal;
        if (anim) anim.Flinch(shotDir, Mathf.Clamp(amount / 25f, 0.3f, 1.5f) * (isBoss ? 0.3f : 1f));
        if (!isBoss && amount >= 20f && windupEnd < 0f) staggerUntil = Time.time + 0.12f;

        if (Health <= 0f)
        {
            Die(shotDir, hitPoint);
            return true;
        }
        if (Time.time > hurtSoundTime)
        {
            hurtSoundTime = Time.time + 0.35f;
            ProceduralAudio.PlayAt(ProceduralAudio.EnemyHurt, transform.position + Vector3.up * 1.5f, 0.6f, 4f, voicePitch);
        }
        return false;
    }

    /// <summary>Damage taken by a body hit (no Hitbox) after armor.</summary>
    public float ScaleBodyDamage(float amount) => amount * bodyDamageMultiplier;

    void UpdateFlash()
    {
        if (flashTimer <= 0f && !flashing) return;
        flashTimer -= Time.deltaTime;
        flashing = flashTimer > 0f;
        SetColors(flashing ? 0.85f : 0f, 1f);
    }

    void SetColors(float whiteAmount, float brightness)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, Color.Lerp(baseColors[i] * brightness, Color.white, whiteAmount));
            renderers[i].SetPropertyBlock(mpb);
        }
    }

    void Die(Vector3 shotDir, Vector3 hitPoint)
    {
        IsDead = true;
        windupEnd = -1f;
        SetColors(0f, 0.4f);
        agent.enabled = false;
        if (anim) anim.enabled = false;

        Vector3 center = eye ? Vector3.Lerp(transform.position, eye.position, 0.6f) : transform.position + Vector3.up * 1.1f;
        Effects.DeathBurst(center, baseColors.Length > 0 ? baseColors[0] : Color.grey, glowColor);
        ProceduralAudio.PlayAt(ProceduralAudio.EnemyDeath, center, 1f, isBoss ? 15f : 6f, isBoss ? 0.6f : voicePitch);
        if (GameManager.Instance != null) GameManager.Instance.OnEnemyKilled(this, killedByPlayer);
        if (killedByPlayer && Random.value < dropChance)
            Pickup.Spawn(Random.value < 0.5f ? Pickup.Kind.Health : Pickup.Kind.Ammo, transform.position + Vector3.up * 0.5f);

        // Exploders go off whether they reached you or got shot (chain reactions!).
        if (attackType == AttackType.Explode)
            Explosion.Explode(center, splashRadius, attackDamage, 12f, killedByPlayer);

        // Robot falls apart: every breakable piece becomes its own physics body.
        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        Vector3 push = shotDir.normalized;
        foreach (var part in breakableParts)
        {
            if (part == null) continue;
            part.SetParent(null, true);
            foreach (var t in part.GetComponentsInChildren<Transform>()) t.gameObject.layer = ignoreLayer;
            if (part.GetComponent<Collider>() == null) part.gameObject.AddComponent<BoxCollider>();
            var rb = part.gameObject.AddComponent<Rigidbody>();
            rb.mass = 5f;
            Vector3 away = (part.position - hitPoint).normalized;
            rb.AddForce((push * 4f + away * 3f + Vector3.up * 3f) * Random.Range(0.7f, 1.3f), ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
            part.gameObject.AddComponent<Debris>().Init(Random.Range(3f, 4.5f));
        }

        Destroy(gameObject);
    }
}

/// <summary>Shrinks away and destroys a detached body part.</summary>
public class Debris : MonoBehaviour
{
    float dieAt, shrinkTime = 0.6f;
    Vector3 startScale;

    public void Init(float lifetime)
    {
        dieAt = Time.time + lifetime;
        startScale = transform.localScale;
    }

    void Update()
    {
        float t = (Time.time - (dieAt - shrinkTime)) / shrinkTime;
        if (t <= 0f) return;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        transform.localScale = startScale * (1f - t);
    }
}
