using UnityEngine;

/// <summary>Holds material references used by runtime-spawned effects.</summary>
public class FXLibrary : MonoBehaviour
{
    public static FXLibrary Instance { get; private set; }

    [Header("Lines")]
    public Material tracer;
    public Material enemyTracer;

    [Header("Particles")]
    public Material particle;   // opaque chunks (debris, blood droplets)
    public Material additive;   // glowing sparks / embers
    public Material smoke;      // soft alpha puffs
    public Material ring;       // additive expanding shockwave ring

    [Header("Projectiles")]
    public Material plasmaGlow;
    public Material rocketBody;

    [Header("Decals")]
    public Material bulletHole;
    public Material bloodDecal;
    public Material scorch;

    [Header("Props")]
    public Material brass;
    public Material shotgunShell;
    public Material healthPickup;
    public Material ammoPickup;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
