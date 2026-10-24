using System.Collections.Generic;
using UnityEngine;

/// <summary>Hitscan or projectile gun with procedural viewmodel animation. Driven each frame by WeaponManager.</summary>
public class Gun : MonoBehaviour
{
    public enum ShotSound { Rifle, Shotgun, Pistol, Smg, Sniper, Rocket }

    /// <summary>Raised once per trigger pull: (gun, any pellet hit a creature, any headshot).</summary>
    public static event System.Action<Gun, bool, bool> Fired;
    /// <summary>Raised per creature hit: (target, damage, headshot, killed, point).</summary>
    public static event System.Action<IDamageable, float, bool, bool, Vector3> TargetHit;

    [Header("Info")]
    public string displayName = "Rifle";
    public string shortName = "AR";
    public ShotSound sound = ShotSound.Rifle;

    [Header("Firing")]
    public bool automatic = true;
    public float fireRate = 10f;
    public float damage = 22f;
    public int pellets = 1;
    public float range = 150f;
    public float impactForce = 8f;
    [Tooltip("Extra enemies a hitscan round passes through.")]
    public int pierce;

    [Header("Projectile (leave speed 0 for hitscan)")]
    public float projectileSpeed;
    public Projectile.Kind projectileKind = Projectile.Kind.Rocket;
    public float splashRadius = 5f;
    public Transform backblast;
    public GameObject loadedRound;

    [Header("Accuracy (degrees)")]
    public float hipSpread = 1.2f;
    public float adsSpread = 0.15f;
    public float moveSpreadBonus = 1.5f;
    public float airSpreadBonus = 3f;
    public float spreadPerShot = 0.35f;
    public float maxShotSpread = 2.5f;
    public float spreadRecovery = 6f;

    [Header("Ammo")]
    public int magazineSize = 30;
    public int reserveAmmo = 150;
    public int maxReserve = 300;
    public bool infiniteReserve;
    public int ammoPerPickup = 60;
    public float reloadTime = 1.9f;
    [Tooltip("Load one shell at a time (shotguns). Firing interrupts the reload.")]
    public bool reloadPerShell;
    public float shellInsertTime = 0.45f;

    [Header("Action")]
    public bool pumpAction;
    public bool boltAction;
    public float cycleDelay = 0.12f;
    public float cycleDuration = 0.4f;
    public float boltKick = 0.05f;

    [Header("Recoil")]
    public float recoilUp = 0.55f;
    public float recoilSide = 0.2f;
    public float kickBack = 0.03f;
    public float kickRotation = 3f;
    public float shake = 0.06f;

    [Header("Aiming")]
    public Vector3 adsPosition = new Vector3(0f, -0.1f, 0.32f);
    public float adsSpeed = 8f;
    public float adsFovMultiplier = 0.75f;
    [Tooltip("Full-screen scope overlay; viewmodel hides when fully aimed.")]
    public bool scope;

    [Header("Parts (animated)")]
    public Transform muzzle;
    public GameObject muzzleFlash;
    public Transform ejectionPort;
    public Transform magazine;
    public Transform bolt;
    public Transform pump;

    public int AmmoInMag { get; private set; }
    public bool IsReloading { get; private set; }
    public float ReloadProgress => !IsReloading ? 0f
        : reloadPerShell ? Mathf.Clamp01((float)AmmoInMag / magazineSize)
        : Mathf.Clamp01(1f - (reloadEnd - Time.time) / reloadTime);
    public float AimAmount { get; private set; }
    public float CurrentSpread { get; private set; }
    public bool IsScoped => scope && AimAmount > 0.92f;
    public bool IsCycling => (pumpAction || boltAction) && Time.time < cycleStart + cycleDelay + cycleDuration;
    public int ReserveDisplay => infiniteReserve ? -1 : reserveAmmo;

    WeaponManager owner;
    Vector3 hipPosition, magRest, boltRest, pumpRest;
    Quaternion magRestRot, boltRestRot;
    float nextFireTime, reloadEnd, shotSpread, flashOffTime, reloadTilt, cycleStart = -10f, nextShellTime, shellBump;
    int hitMask, reloadSoundStage;
    bool initialized, cycleEjected, cycleForwardPlayed, cycleBackPlayed = true, needsCycleAfterReload, visible = true;
    Renderer[] renderers;
    readonly List<RaycastHit> hitBuffer = new List<RaycastHit>();

    // Spring-driven viewmodel recoil (position + euler rotation).
    Vector3 kickPos, kickPosVel, kickRot, kickRotVel;

    public void Init(WeaponManager manager)
    {
        owner = manager;
        hipPosition = transform.localPosition;
        if (magazine) { magRest = magazine.localPosition; magRestRot = magazine.localRotation; }
        if (bolt) { boltRest = bolt.localPosition; boltRestRot = bolt.localRotation; }
        if (pump) pumpRest = pump.localPosition;
        AmmoInMag = magazineSize;
        hitMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
        renderers = GetComponentsInChildren<Renderer>(true);
        if (muzzleFlash) muzzleFlash.SetActive(false);
        initialized = true;
    }

    void OnDisable()
    {
        IsReloading = false;
        AimAmount = 0f;
        reloadTilt = 0f;
        kickPos = kickPosVel = kickRot = kickRotVel = Vector3.zero;
        if (muzzleFlash) muzzleFlash.SetActive(false);
        if (!initialized) return;
        SetVisible(true);
        transform.localPosition = hipPosition;
        if (magazine) magazine.SetLocalPositionAndRotation(magRest, magRestRot);
        if (bolt) bolt.SetLocalPositionAndRotation(boltRest, boltRestRot);
        if (pump) pump.localPosition = pumpRest;
    }

    public void Tick(bool fireHeld, bool firePressed, bool reloadPressed, bool aimHeld, bool canFire)
    {
        float dt = Time.deltaTime;
        bool aiming = aimHeld && canFire && !IsReloading;
        AimAmount = Mathf.MoveTowards(AimAmount, aiming ? 1f : 0f, dt * adsSpeed);
        SetVisible(!IsScoped);

        UpdateReload(firePressed, canFire);
        if (canFire && reloadPressed) StartReload();

        bool wantsFire = automatic ? fireHeld : firePressed;
        if (canFire && wantsFire && !IsReloading && !IsCycling && Time.time >= nextFireTime)
        {
            if (AmmoInMag > 0) Shoot();
            else if (firePressed)
            {
                ProceduralAudio.Play2D(ProceduralAudio.Empty, 0.5f);
                nextFireTime = Time.time + 0.25f;
            }
        }

        // Auto-reload when the mag runs dry.
        if (!IsReloading && !IsCycling && AmmoInMag == 0 && HasReserve && Time.time >= nextFireTime) StartReload();

        if (loadedRound) loadedRound.SetActive(AmmoInMag > 0);

        UpdateSpread(dt);
        UpdateCycle();
        Animate(dt);

        if (muzzleFlash && muzzleFlash.activeSelf && Time.time >= flashOffTime) muzzleFlash.SetActive(false);
    }

    bool HasReserve => infiniteReserve || reserveAmmo > 0;

    void SetVisible(bool show)
    {
        if (show == visible || renderers == null) return;
        visible = show;
        foreach (var r in renderers)
            if (r != null) r.enabled = show;
    }

    // ---------------- Firing ----------------

    void Shoot()
    {
        AmmoInMag--;
        nextFireTime = Time.time + 1f / fireRate;

        Transform cam = owner.cam.transform;
        Vector3 muzzlePos = muzzle ? muzzle.position : cam.position;

        if (projectileSpeed > 0f) ShootProjectile(cam, muzzlePos);
        else ShootHitscan(cam, muzzlePos);

        ProceduralAudio.Play2D(ShotClip, sound == ShotSound.Smg ? 0.55f : 0.75f, 0.06f);

        if (muzzleFlash)
        {
            muzzleFlash.SetActive(true);
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            muzzleFlash.transform.localScale = Vector3.one * Random.Range(0.75f, 1.3f);
            flashOffTime = Time.time + 0.035f;
        }
        if (muzzle) Effects.MuzzleSmoke(muzzle.position, muzzle.forward, pumpAction || boltAction ? 2.2f : 0.5f);
        if (backblast) Effects.MuzzleSmoke(backblast.position, backblast.forward, 4f);

        if (pumpAction || boltAction)
        {
            cycleStart = Time.time;
            cycleEjected = false;
            cycleForwardPlayed = false;
            cycleBackPlayed = false;
        }
        else
        {
            EjectShell();
            if (bolt) bolt.localPosition = boltRest + Vector3.back * boltKick;
        }

        // Viewmodel kick (springs) + camera recoil.
        float adsScale = Mathf.Lerp(1f, 0.45f, AimAmount);
        kickPosVel += new Vector3(Random.Range(-0.2f, 0.2f) * kickBack, kickBack * 0.4f, -kickBack) * 60f * adsScale;
        kickRotVel += new Vector3(-kickRotation, Random.Range(-1f, 1f) * kickRotation * 0.4f, Random.Range(-1f, 1f) * kickRotation * 0.6f) * 60f * adsScale;

        shotSpread = Mathf.Min(shotSpread + spreadPerShot, maxShotSpread);
        float recoilScale = Mathf.Lerp(1f, 0.6f, AimAmount);
        owner.player.AddRecoil(recoilUp * recoilScale, Random.Range(-recoilSide, recoilSide) * recoilScale);
        owner.player.AddShake(shake);
    }

    AudioClip ShotClip => sound switch
    {
        ShotSound.Shotgun => ProceduralAudio.Shotgun,
        ShotSound.Pistol => ProceduralAudio.PistolShot,
        ShotSound.Smg => ProceduralAudio.SmgShot,
        ShotSound.Sniper => ProceduralAudio.SniperShot,
        ShotSound.Rocket => ProceduralAudio.RocketLaunch,
        _ => ProceduralAudio.Rifle,
    };

    /// <summary>The ray shots follow: through the on-screen crosshair (moves in fixed-camera mode), with a basis for spread.</summary>
    void AimBasis(Transform cam, out Vector3 origin, out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        Ray ray = owner.player != null ? owner.player.AimRay : new Ray(cam.position, cam.forward);
        origin = ray.origin;
        forward = ray.direction;
        var basis = Quaternion.LookRotation(forward, cam.up);
        right = basis * Vector3.right;
        up = basis * Vector3.up;
    }

    void ShootProjectile(Transform cam, Vector3 muzzlePos)
    {
        // Aim from the muzzle at whatever the crosshair is on.
        AimBasis(cam, out Vector3 origin, out Vector3 forward, out _, out _);
        Vector3 aimPoint = origin + forward * 200f;
        if (Physics.Raycast(origin, forward, out RaycastHit hit, 200f, hitMask, QueryTriggerInteraction.Ignore))
            aimPoint = hit.point;
        Vector3 dir = aimPoint - muzzlePos;
        // Very close targets: fire straight from the camera so the rocket can't spawn past them.
        Vector3 spawn = dir.magnitude < 1.5f ? origin + forward * 0.3f : muzzlePos;
        Projectile.Spawn(projectileKind, spawn, (aimPoint - spawn).normalized, projectileSpeed, damage, splashRadius, true);
        Fired?.Invoke(this, false, false);
    }

    void ShootHitscan(Transform cam, Vector3 muzzlePos)
    {
        float tan = Mathf.Tan(CurrentSpread * Mathf.Deg2Rad);
        bool anyHit = false, anyKill = false, headshot = false;
        int worldSounds = 0;
        AimBasis(cam, out Vector3 origin, out Vector3 forward, out Vector3 right, out Vector3 up);

        for (int i = 0; i < pellets; i++)
        {
            Vector2 r = Random.insideUnitCircle * tan;
            Vector3 dir = (forward + right * r.x + up * r.y).normalized;
            Vector3 end = origin + dir * range;

            hitBuffer.Clear();
            hitBuffer.AddRange(Physics.RaycastAll(origin, dir, range, hitMask, QueryTriggerInteraction.Ignore));
            hitBuffer.Sort((a, b) => a.distance.CompareTo(b.distance));

            int enemiesHit = 0;
            var damaged = new HashSet<IDamageable>();
            foreach (var hit in hitBuffer)
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();
                end = hit.point;
                if (target != null && !damaged.Add(target)) continue; // same enemy, another collider

                if (target is IHitFeedback creature)
                {
                    if (creature.IsDead) continue;
                    var hitbox = hit.collider.GetComponent<Hitbox>();
                    float mult = hitbox ? hitbox.damageMultiplier : 1f;
                    bool head = mult > 1f;
                    headshot |= head;
                    anyHit = true;
                    float falloff = enemiesHit == 0 ? 1f : 0.7f;
                    float dmg = damage * mult * falloff;
                    if (hitbox == null) dmg = creature.ScaleBodyDamage(dmg);
                    bool killed = target.TakeDamage(dmg, hit.point, -dir);
                    anyKill |= killed;
                    TargetHit?.Invoke(target, dmg, head, killed, hit.point);
                    Effects.ImpactFlesh(hit.point, hit.normal, dir, creature.ImpactColor);
                    if (++enemiesHit > pierce) break;
                    continue;
                }

                // Barrels and other damageables, then the world stops the round.
                target?.TakeDamage(damage, hit.point, -dir);
                Effects.ImpactWorld(hit.point, hit.normal, hit.rigidbody == null && target == null, worldSounds++ < 2);
                if (hit.rigidbody) hit.rigidbody.AddForceAtPosition(dir * impactForce, hit.point, ForceMode.Impulse);
                break;
            }

            if (pellets == 1 || i % 3 == 0) Effects.Tracer(muzzlePos, end);
        }

        Fired?.Invoke(this, anyHit, headshot);
        if (anyHit)
        {
            if (HUD.Instance != null) HUD.Instance.ShowHitmarker(anyKill, headshot);
            if (anyKill) ProceduralAudio.Play2D(ProceduralAudio.Kill, 0.55f, 0f);
            else if (headshot) ProceduralAudio.Play2D(ProceduralAudio.Headshot, 0.4f, 0f);
            else ProceduralAudio.Play2D(ProceduralAudio.Hit, 0.3f, 0f);
        }
    }

    void EjectShell()
    {
        if (ejectionPort == null) return;
        var p = owner.player;
        Vector3 vel = p.Velocity + ejectionPort.right * Random.Range(1.6f, 2.6f) + ejectionPort.up * Random.Range(1f, 2f)
                      - ejectionPort.forward * Random.Range(0.2f, 0.8f);
        Effects.EjectShell(ejectionPort.position, ejectionPort.rotation * Quaternion.Euler(90f, 0f, 0f), vel, pumpAction);
    }

    /// Pump stroke or bolt cycle after each shot.
    void UpdateCycle()
    {
        if (!pumpAction && !boltAction) return;
        float t = (Time.time - cycleStart - cycleDelay) / cycleDuration;
        if (t < 0f || t > 1f)
        {
            if (pump) pump.localPosition = pumpRest;
            if (boltAction && bolt) bolt.SetLocalPositionAndRotation(boltRest, boltRestRot);
            return;
        }
        if (!cycleBackPlayed)
        {
            cycleBackPlayed = true;
            ProceduralAudio.Play2D(pumpAction ? ProceduralAudio.PumpBack : ProceduralAudio.BoltRack, 0.6f);
        }
        if (t >= 0.45f && !cycleEjected)
        {
            cycleEjected = true;
            EjectShell();
        }
        if (t >= 0.55f && !cycleForwardPlayed)
        {
            cycleForwardPlayed = true;
            ProceduralAudio.Play2D(pumpAction ? ProceduralAudio.PumpForward : ProceduralAudio.Empty, 0.6f);
        }

        if (pump) pump.localPosition = pumpRest + Vector3.back * 0.09f * Mathf.Sin(t * Mathf.PI);
        if (boltAction && bolt)
        {
            // Lift the handle, pull back, push forward, lock down.
            float lift = Mathf.Clamp01(t / 0.2f) * (1f - Mathf.Clamp01((t - 0.8f) / 0.2f));
            float slide = t < 0.2f || t > 0.8f ? 0f : Mathf.Sin((t - 0.2f) / 0.6f * Mathf.PI);
            bolt.SetLocalPositionAndRotation(boltRest + Vector3.back * 0.09f * slide, boltRestRot * Quaternion.Euler(0f, 0f, 70f * lift));
            kickRotVel += new Vector3(0f, 0f, 25f * lift) * Time.deltaTime;
        }
        else kickRotVel += new Vector3(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 20f) * Time.deltaTime;
    }

    void UpdateSpread(float dt)
    {
        shotSpread = Mathf.MoveTowards(shotSpread, 0f, dt * spreadRecovery);
        var p = owner.player;
        float moveFactor = Mathf.Clamp01(p.HorizontalSpeed / p.walkSpeed);
        CurrentSpread = Mathf.Lerp(hipSpread, adsSpread, AimAmount)
                        + moveFactor * moveSpreadBonus * (1f - AimAmount * 0.7f)
                        + (p.IsGrounded ? 0f : airSpreadBonus)
                        + shotSpread;
    }

    // ---------------- Reloading ----------------

    void StartReload()
    {
        if (IsReloading || AmmoInMag >= magazineSize || !HasReserve) return;
        IsReloading = true;
        reloadSoundStage = 0;
        if (reloadPerShell)
        {
            needsCycleAfterReload = AmmoInMag == 0;
            nextShellTime = Time.time + 0.3f + shellInsertTime;
        }
        else reloadEnd = Time.time + reloadTime;
    }

    void TakeFromReserve(int amount)
    {
        if (!infiniteReserve) reserveAmmo -= amount;
        AmmoInMag += amount;
    }

    void UpdateReload(bool firePressed, bool canFire)
    {
        if (!IsReloading) return;

        if (reloadPerShell)
        {
            // Firing cancels a shell-by-shell reload as long as something is loaded.
            if (canFire && firePressed && AmmoInMag > 0)
            {
                IsReloading = false;
                return;
            }
            if (Time.time >= nextShellTime)
            {
                TakeFromReserve(1);
                shellBump = 1f;
                ProceduralAudio.Play2D(ProceduralAudio.ShellInsert, 0.6f);
                nextShellTime = Time.time + shellInsertTime;
                if (AmmoInMag >= magazineSize || !HasReserve)
                {
                    IsReloading = false;
                    if (needsCycleAfterReload && (pumpAction || boltAction))
                    {
                        cycleStart = Time.time - cycleDelay * 0.5f;
                        cycleEjected = true; // nothing to eject, chamber was empty
                        cycleForwardPlayed = false;
                        cycleBackPlayed = false;
                    }
                }
            }
            return;
        }

        float p = ReloadProgress;
        if (reloadSoundStage == 0 && p >= 0.18f) { ProceduralAudio.Play2D(ProceduralAudio.MagOut, 0.6f); reloadSoundStage++; }
        if (reloadSoundStage == 1 && p >= 0.58f) { ProceduralAudio.Play2D(ProceduralAudio.MagIn, 0.7f); reloadSoundStage++; kickPosVel += Vector3.up * 0.6f; }
        if (reloadSoundStage == 2 && p >= 0.78f) { ProceduralAudio.Play2D(ProceduralAudio.BoltRack, 0.7f); reloadSoundStage++; }

        if (Time.time >= reloadEnd)
        {
            TakeFromReserve(infiniteReserve ? magazineSize - AmmoInMag : Mathf.Min(magazineSize - AmmoInMag, reserveAmmo));
            IsReloading = false;
        }
    }

    public bool AddAmmo(int amount)
    {
        if (infiniteReserve || reserveAmmo >= maxReserve) return false;
        reserveAmmo = Mathf.Min(maxReserve, reserveAmmo + amount);
        return true;
    }

    // ---------------- Animation ----------------

    void Animate(float dt)
    {
        // Damped springs pulling the kick back to rest.
        const float stiffness = 220f, damping = 18f;
        kickPosVel += (-kickPos * stiffness - kickPosVel * damping) * dt;
        kickPos += kickPosVel * dt;
        kickRotVel += (-kickRot * stiffness - kickRotVel * damping) * dt;
        kickRot += kickRotVel * dt;

        reloadTilt = Mathf.MoveTowards(reloadTilt, IsReloading ? 1f : 0f, dt * 5f);
        float tilt = Mathf.SmoothStep(0f, 1f, reloadTilt);
        shellBump = Mathf.MoveTowards(shellBump, 0f, dt * 5f);

        Vector3 pos = Vector3.Lerp(hipPosition, adsPosition, AimAmount) + kickPos;
        Vector3 euler = kickRot;

        if (reloadPerShell)
        {
            // Roll the gun to show the loading port; bump per shell.
            pos += new Vector3(-0.03f, -0.04f + shellBump * 0.015f, 0f) * tilt;
            euler += new Vector3(-8f + shellBump * 6f, 10f, 40f) * tilt;
        }
        else
        {
            float p = ReloadProgress;
            pos += new Vector3(-0.02f, -0.05f, 0f) * tilt;
            euler += new Vector3(-12f, 15f, 30f) * tilt;

            if (magazine)
            {
                // Mag drops out, pauses off-screen, then slides back in.
                float drop = 0f;
                if (IsReloading)
                {
                    if (p < 0.15f) drop = 0f;
                    else if (p < 0.3f) drop = Mathf.SmoothStep(0f, 1f, (p - 0.15f) / 0.15f);
                    else if (p < 0.45f) drop = 1f;
                    else if (p < 0.58f) drop = 1f - Mathf.SmoothStep(0f, 1f, (p - 0.45f) / 0.13f);
                }
                magazine.SetLocalPositionAndRotation(magRest + new Vector3(0f, -0.35f, 0.05f) * drop, magRestRot * Quaternion.Euler(drop * 25f, 0f, 0f));
            }

            if (bolt && !boltAction)
            {
                float rack = IsReloading && p > 0.74f && p < 0.86f ? Mathf.Sin((p - 0.74f) / 0.12f * Mathf.PI) : 0f;
                Vector3 target = boltRest + Vector3.back * 0.07f * rack;
                bolt.localPosition = rack > 0f ? target : Vector3.Lerp(bolt.localPosition, boltRest, dt * 30f);
            }
        }

        transform.localPosition = pos;
        transform.localRotation = Quaternion.Euler(euler);
    }
}
