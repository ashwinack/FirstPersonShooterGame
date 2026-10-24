using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// Generates the whole game: textures, materials, post-processing, enemy prefabs,
/// the Arena scene (level, player, viewmodels, managers) and the MainMenu scene.
/// Menu: FPS > Build All Scenes. Safe to re-run; it regenerates everything.
/// Split across partial files: Level, Weapons, Enemies.
/// </summary>
public static partial class FPSSceneBuilder
{
    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string TexDir = "Assets/Textures";
    const string ArenaPath = "Assets/Scenes/Arena.unity";
    const string MenuPath = "Assets/Scenes/MainMenu.unity";
    const string ProfilePath = "Assets/Settings/ArenaProfile.asset";

    // Gun order matters: index = slot key - 1, and GameManager.weaponUnlockWaves follows it.
    static readonly int[] WeaponUnlockWaves = { 0, 0, 2, 3, 5, 7 };
    static readonly bool[] WeaponStartOwned = { true, true, false, false, false, false };

    [MenuItem("FPS/Build All Scenes")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    // ---------- Shared materials (created in CreateMaterials) ----------
    static Material groundMat, wallMat, platformMat, crateMat, crateTrimMat, coverMat, barrelMat, barrelBandMat,
        explosiveMat, hazardMat, stripMat, stripCoolMat, stripRedMat, warehouseMat, roofMat, shelfMat, sandbagMat, postMat, lampMat;
    static Material[] containerMats;
    static Material gunMetal, gunMetalLight, gunPolymer, wood, glove, sleeve, redDot, greenDot, brass, shellRed, olive, lensMat;
    static Material flashMat, tracerMat, enemyTracerMat, additiveMat, smokeMat, particleMat, bulletHoleMat, bloodMat, scorchMat,
        ringMat, plasmaGlowMat, rocketBodyMat, healthMat, ammoMat;
    static Texture2D softDot;

    public static void Build()
    {
        EnsureLayer(8, "Player");
        EnsureLayer(9, "Enemy");
        EnsureFolder(MatDir);
        EnsureFolder(PrefabDir);
        EnsureFolder(TexDir);

        CreateMaterials();
        BuildEnemyPrefabs();
        var profile = BuildVolumeProfile();

        BuildMenuScene(profile);
        BuildArenaScene(profile);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuPath, true),
            new EditorBuildSettingsScene(ArenaPath, true),
        };
        AssetDatabase.SaveAssets();
        FixEmissionKeywords();
        EditorSceneManager.OpenScene(MenuPath);
        Debug.Log("[FPS] Built MainMenu + Arena. Open MainMenu and press Play!");
    }

    // ======================= Scenes =======================

    static void BuildArenaScene(VolumeProfile profile)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLighting(profile);
        var level = BuildLevel();
        BuildSurvivalRig(level.playerStart, 0f, level.spawnPoints);
        EditorSceneManager.SaveScene(scene, ArenaPath);
    }

    /// Player + navmesh + GameManager (wave table, unlocks) + FX + HUD. Shared by every survival map.
    static void BuildSurvivalRig(Vector3 playerStart, float playerYaw, Transform[] spawnPoints)
    {
        var (pc, ph, wm, cam) = BuildPlayer(playerStart, playerYaw, true, WeaponStartOwned);

        // Fixed start point at the entrance; its forward is the front of the player's 180-degree view.
        var start = new GameObject("PlayerStartPoint").transform;
        start.SetPositionAndRotation(playerStart, Quaternion.Euler(0f, playerYaw, 0f));
        var so = new SerializedObject(pc);
        so.FindProperty("playerStartPoint").objectReferenceValue = start;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Managers ----------
        var navGO = new GameObject("NavMesh");
        var surface = navGO.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast");

        Enemy Load(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{n}.prefab").GetComponent<Enemy>();

        var gmGO = new GameObject("GameManager");
        var gm = gmGO.AddComponent<GameManager>();
        gm.player = pc;
        gm.playerHealth = ph;
        gm.weapons = wm;
        gm.spawnPoints = spawnPoints;
        gm.navMeshSurface = surface;
        gm.weaponUnlockWaves = WeaponUnlockWaves;
        gm.enemyTable = new[]
        {
            new GameManager.EnemyEntry { prefab = Load("Grunt"), minWave = 1, weight = 10f, weightPerWave = -0.4f },
            new GameManager.EnemyEntry { prefab = Load("Runner"), minWave = 2, weight = 3f, weightPerWave = 0.8f },
            new GameManager.EnemyEntry { prefab = Load("Gunner"), minWave = 2, weight = 3f, weightPerWave = 0.6f },
            new GameManager.EnemyEntry { prefab = Load("Exploder"), minWave = 3, weight = 2f, weightPerWave = 0.5f },
            new GameManager.EnemyEntry { prefab = Load("Drone"), minWave = 4, weight = 2f, weightPerWave = 0.5f },
            new GameManager.EnemyEntry { prefab = Load("Brute"), minWave = 8, weight = 0.3f, weightPerWave = 0.15f },
        };
        gm.bossPrefab = Load("Brute");

        AddFxLibrary(gmGO);
        var hud = gmGO.AddComponent<HUD>();
        hud.player = pc;
        hud.health = ph;
        hud.weapons = wm;
    }

    /// Player rig: controller, camera, all six viewmodels. Health is optional (the range has no damage).
    static (PlayerController, PlayerHealth, WeaponManager, Camera) BuildPlayer(Vector3 position, float yaw, bool withHealth, bool[] owned)
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Player");
        player.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.4f;
        cc.skinWidth = 0.06f;
        cc.slopeLimit = 50f;

        var pc = player.AddComponent<PlayerController>();
        var ph = withHealth ? player.AddComponent<PlayerHealth>() : null;

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(player.transform, false);
        camGO.transform.localPosition = new Vector3(0, 1.65f, 0);
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 75f;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 400f;
        camGO.AddComponent<AudioListener>();
        ConfigureCamera(cam);
        pc.cameraPivot = camGO.transform;

        var holder = new GameObject("WeaponHolder").transform;
        holder.SetParent(camGO.transform, false);

        var wm = player.AddComponent<WeaponManager>();
        wm.player = pc;
        wm.cam = cam;
        wm.holder = holder;
        wm.guns = new[]
        {
            BuildPistol(holder), BuildRifle(holder), BuildShotgun(holder),
            BuildSmg(holder), BuildSniper(holder), BuildRocketLauncher(holder),
        };
        wm.startOwned = owned;
        return (pc, ph, wm, cam);
    }

    static FXLibrary AddFxLibrary(GameObject host)
    {
        var fx = host.AddComponent<FXLibrary>();
        fx.tracer = tracerMat;
        fx.enemyTracer = enemyTracerMat;
        fx.particle = particleMat;
        fx.additive = additiveMat;
        fx.smoke = smokeMat;
        fx.ring = ringMat;
        fx.plasmaGlow = plasmaGlowMat;
        fx.rocketBody = rocketBodyMat;
        fx.bulletHole = bulletHoleMat;
        fx.bloodDecal = bloodMat;
        fx.scorch = scorchMat;
        fx.brass = brass;
        fx.shotgunShell = shellRed;
        fx.healthPickup = healthMat;
        fx.ammoPickup = ammoMat;
        return fx;
    }

    static void BuildMenuScene(VolumeProfile profile)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLighting(profile);
        BuildLevel();

        var camGO = new GameObject("Menu Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.farClipPlane = 400f;
        camGO.AddComponent<AudioListener>();
        ConfigureCamera(cam);

        var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
        menu.menuCamera = cam;
        menu.orbitCenter = new Vector3(0, 3f, 0);
        menu.orbitRadius = 15f;
        menu.orbitHeight = 4.5f;

        // Robot line-up on the central platform (animation only, no AI), clear of the cover blocks.
        var lineup = new (string name, Vector3 pos, float yaw)[]
        {
            ("Brute", new Vector3(0, 3f, 1.5f), 180f),
            ("Grunt", new Vector3(-2.2f, 3f, -0.8f), 160f),
            ("Gunner", new Vector3(2.2f, 3f, -0.8f), 200f),
            ("Runner", new Vector3(-3.4f, 3f, 2.2f), 140f),
            ("Exploder", new Vector3(3.4f, 3f, 2.2f), 220f),
            ("Drone", new Vector3(0f, 3f, -2.8f), 180f),
        };
        foreach (var (n, pos, yaw) in lineup)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{n}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            Object.DestroyImmediate(go.GetComponent<Enemy>());
            Object.DestroyImmediate(go.GetComponent<NavMeshAgent>());
        }

        // Spotlights on the line-up.
        foreach (var p in new[] { new Vector3(-6, 9, -6), new Vector3(6, 9, -6) })
        {
            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.transform.position = p;
            spot.transform.LookAt(new Vector3(0, 3.5f, 0));
            spot.spotAngle = 45f;
            spot.range = 20f;
            spot.intensity = 12f;
            spot.color = new Color(1f, 0.85f, 0.7f);
            spot.shadows = LightShadows.Soft;
        }

        EditorSceneManager.SaveScene(scene, MenuPath);
    }

    static void ConfigureCamera(Camera cam)
    {
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
    }

    static void SetupLighting(VolumeProfile profile)
    {
        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.7f;
        sun.color = new Color(1f, 0.84f, 0.68f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sunGO.transform.rotation = Quaternion.Euler(30f, -40f, 0f);

        var sky = GetOrCreateMat("ArenaSky", "Skybox/Procedural");
        sky.SetFloat("_SunSize", 0.035f);
        sky.SetFloat("_AtmosphereThickness", 1.25f);
        sky.SetColor("_SkyTint", new Color(0.55f, 0.55f, 0.65f));
        sky.SetColor("_GroundColor", new Color(0.3f, 0.28f, 0.26f));
        sky.SetFloat("_Exposure", 1.15f);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.56f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.45f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.16f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.006f;
        RenderSettings.fogColor = new Color(0.7f, 0.64f, 0.6f);

        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
    }

    // ======================= Materials & textures =======================

    static void CreateMaterials()
    {
        var grid = Tex("Grid", 256, true, (u, v) =>
        {
            int x = (int)(u * 256), y = (int)(v * 256);
            bool line = x < 4 || y < 4;
            bool sub = x % 128 < 2 || y % 128 < 2;
            float n = Mathf.PerlinNoise(u * 12f, v * 12f) * 0.06f + Mathf.PerlinNoise(u * 40f, v * 40f) * 0.03f;
            float g = (line ? 0.55f : sub ? 0.82f : 0.93f) - n;
            return new Color(g, g, g, 1f);
        });
        var ribs = Tex("ContainerRibs", 128, true, (u, v) =>
        {
            float rib = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 8f));
            float n = Mathf.PerlinNoise(u * 20f, v * 20f) * 0.12f;
            float g = rib - n;
            return new Color(g, g, g, 1f);
        });
        var hazard = Tex("Hazard", 128, true, (u, v) =>
        {
            bool stripe = ((u + v) * 4f) % 1f < 0.5f;
            return stripe ? new Color(1f, 0.8f, 0.1f) : new Color(0.08f, 0.08f, 0.08f);
        });
        softDot = Tex("SoftDot", 128, false, (u, v) =>
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
            return new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2f));
        });
        var ringTex = Tex("Ring", 256, false, (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Exp(-Mathf.Pow((d - 0.85f) / 0.07f, 2f));
            return new Color(1f, 1f, 1f, a);
        });
        var holeTex = Tex("BulletHole", 128, false, (u, v) =>
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float d = p.magnitude;
            float ang = Mathf.Atan2(p.y, p.x);
            float jag = Mathf.PerlinNoise(Mathf.Cos(ang) * 3f + 5f, Mathf.Sin(ang) * 3f + 5f) * 0.35f;
            float core = d < 0.22f ? 1f : 0f;
            float ring = Mathf.Clamp01(1f - (d - 0.2f) / (0.5f + jag)) * 0.75f;
            float shade = d < 0.22f ? 0f : 0.15f;
            return new Color(shade, shade, shade, Mathf.Max(core, ring * ring));
        });
        var splatTex = Tex("Splat", 256, false, (u, v) =>
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float d = p.magnitude;
            float n = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.6f + Mathf.PerlinNoise(u * 18f, v * 18f) * 0.25f;
            float blob = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.75f - d + n * 0.55f - 0.25f) * 6f));
            float drops = 0f;
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.4f, r = 0.55f + (i % 3) * 0.13f;
                Vector2 c = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                drops = Mathf.Max(drops, Mathf.Clamp01((0.06f - Vector2.Distance(p, c)) * 40f));
            }
            float shade = 0.75f + n * 0.4f;
            return new Color(shade, shade, shade, Mathf.Max(blob, drops));
        });
        var scorchTex = Tex("Scorch", 128, false, (u, v) =>
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float n = Mathf.PerlinNoise(u * 8f, v * 8f);
            float a = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - p.magnitude), 1.3f) * (0.6f + n * 0.6f));
            return new Color(0.05f, 0.04f, 0.03f, a * 0.85f);
        });

        // World
        groundMat = LitMat("Ground", new Color(0.45f, 0.47f, 0.5f), grid, new Vector2(62, 62));
        wallMat = LitMat("Wall", new Color(0.62f, 0.6f, 0.56f), grid, new Vector2(63, 3.5f));
        platformMat = LitMat("Platform", new Color(0.35f, 0.42f, 0.5f), grid, new Vector2(6, 6));
        crateMat = LitMat("Crate", new Color(0.55f, 0.4f, 0.25f), grid, new Vector2(1, 1));
        crateTrimMat = LitMat("CrateTrim", new Color(0.3f, 0.2f, 0.12f), smoothness: 0.2f);
        coverMat = LitMat("Cover", new Color(0.3f, 0.33f, 0.36f), grid, new Vector2(2, 1));
        barrelMat = LitMat("Barrel", new Color(0.25f, 0.4f, 0.55f), smoothness: 0.45f, metallic: 0.3f);
        barrelBandMat = LitMat("BarrelBand", new Color(0.2f, 0.2f, 0.2f), smoothness: 0.5f, metallic: 0.8f);
        explosiveMat = LitMat("ExplosiveBarrel", new Color(0.8f, 0.12f, 0.08f), smoothness: 0.5f, metallic: 0.2f);
        hazardMat = LitMat("HazardStripe", Color.white, hazard, new Vector2(3, 1), emission: new Color(0.6f, 0.45f, 0.05f));
        stripMat = LitMat("LightStrip", Color.black, emission: new Color(3f, 1.6f, 0.5f));
        stripCoolMat = LitMat("LightStripCool", Color.black, emission: new Color(0.4f, 1.6f, 3f));
        stripRedMat = LitMat("LightStripRed", Color.black, emission: new Color(4f, 0.4f, 0.3f));
        warehouseMat = LitMat("Warehouse", new Color(0.5f, 0.52f, 0.5f), ribs, new Vector2(10, 2));
        roofMat = LitMat("Roof", new Color(0.3f, 0.31f, 0.32f), ribs, new Vector2(8, 4));
        shelfMat = LitMat("Shelf", new Color(0.2f, 0.32f, 0.5f), smoothness: 0.4f, metallic: 0.6f);
        sandbagMat = LitMat("Sandbag", new Color(0.55f, 0.5f, 0.38f), grid, new Vector2(3, 1));
        postMat = LitMat("LampPost", new Color(0.15f, 0.15f, 0.16f), smoothness: 0.5f, metallic: 0.7f);
        lampMat = LitMat("LampHead", Color.black, emission: new Color(4f, 3f, 2f));
        containerMats = new[]
        {
            LitMat("ContainerRed", new Color(0.62f, 0.16f, 0.1f), ribs, new Vector2(3, 1), 0.35f, metallic: 0.4f),
            LitMat("ContainerBlue", new Color(0.12f, 0.28f, 0.55f), ribs, new Vector2(3, 1), 0.35f, metallic: 0.4f),
            LitMat("ContainerGreen", new Color(0.18f, 0.42f, 0.22f), ribs, new Vector2(3, 1), 0.35f, metallic: 0.4f),
            LitMat("ContainerOrange", new Color(0.8f, 0.45f, 0.1f), ribs, new Vector2(3, 1), 0.35f, metallic: 0.4f),
        };

        // Guns
        gunMetal = LitMat("GunMetal", new Color(0.09f, 0.09f, 0.1f), smoothness: 0.55f, metallic: 0.8f);
        gunMetalLight = LitMat("GunMetalLight", new Color(0.3f, 0.31f, 0.33f), smoothness: 0.6f, metallic: 0.9f);
        gunPolymer = LitMat("GunPolymer", new Color(0.24f, 0.23f, 0.19f), smoothness: 0.25f);
        wood = LitMat("Wood", new Color(0.38f, 0.22f, 0.11f), smoothness: 0.35f);
        glove = LitMat("Glove", new Color(0.08f, 0.08f, 0.08f), smoothness: 0.2f);
        sleeve = LitMat("Sleeve", new Color(0.22f, 0.25f, 0.18f), smoothness: 0.1f);
        olive = LitMat("Olive", new Color(0.28f, 0.32f, 0.2f), smoothness: 0.3f);
        lensMat = LitMat("Lens", new Color(0.05f, 0.1f, 0.15f), smoothness: 0.95f, metallic: 0.5f);
        redDot = LitMat("RedDot", Color.black, emission: new Color(8f, 0.3f, 0.2f));
        greenDot = LitMat("GreenDot", Color.black, emission: new Color(0.3f, 6f, 0.5f));
        brass = LitMat("Brass", new Color(0.85f, 0.65f, 0.3f), smoothness: 0.75f, metallic: 1f);
        shellRed = LitMat("ShellRed", new Color(0.65f, 0.08f, 0.06f), smoothness: 0.5f);

        // Effects
        flashMat = FxMat("MuzzleFlash", new Color(5f, 3.2f, 1.2f, 1f), softDot, additive: true, twoSided: true);
        tracerMat = FxMat("Tracer", new Color(4f, 3f, 1.4f, 1f), null, additive: true);
        enemyTracerMat = FxMat("EnemyTracer", new Color(6f, 0.8f, 0.5f, 1f), null, additive: true);
        additiveMat = FxMat("Additive", new Color(3f, 2.4f, 1.6f, 1f), softDot, additive: true, particles: true);
        smokeMat = FxMat("Smoke", Color.white, softDot, additive: false, particles: true);
        particleMat = UnlitMat("ImpactParticle", Color.white, "Universal Render Pipeline/Particles/Unlit");
        bulletHoleMat = FxMat("BulletHoleDecal", Color.white, holeTex, additive: false);
        bloodMat = FxMat("BloodDecal", new Color(0.09f, 0.08f, 0.07f, 0.95f), splatTex, additive: false);
        scorchMat = FxMat("ScorchDecal", Color.white, scorchTex, additive: false);
        ringMat = FxMat("Shockwave", Color.white, ringTex, additive: true, twoSided: true);
        plasmaGlowMat = LitMat("PlasmaGlow", new Color(0.6f, 0.9f, 1f), emission: new Color(2f, 5f, 8f));
        rocketBodyMat = LitMat("RocketBody", new Color(0.3f, 0.34f, 0.22f), smoothness: 0.4f);
        healthMat = LitMat("HealthPickup", new Color(0.2f, 0.9f, 0.3f), emission: new Color(0.3f, 2f, 0.5f));
        ammoMat = LitMat("AmmoPickup", new Color(0.9f, 0.75f, 0.2f), emission: new Color(2f, 1.5f, 0.3f));
    }

    // ======================= Post-processing =======================

    static VolumeProfile BuildVolumeProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        T Get<T>() where T : VolumeComponent
        {
            if (!profile.TryGet(out T comp))
            {
                comp = profile.Add<T>(true);
                comp.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(comp, profile);
            }
            comp.active = true;
            return comp;
        }

        var bloom = Get<Bloom>();
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.7f);
        Get<Tonemapping>().mode.Override(TonemappingMode.ACES);
        var color = Get<ColorAdjustments>();
        color.postExposure.Override(0.35f);
        color.contrast.Override(14f);
        color.saturation.Override(6f);
        var lift = Get<LiftGammaGain>();
        lift.lift.Override(new Vector4(0.98f, 0.99f, 1.03f, 0f));
        lift.gain.Override(new Vector4(1.03f, 1.0f, 0.97f, 0f));
        var vignette = Get<Vignette>();
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.45f);
        var grain = Get<FilmGrain>();
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.15f);
        Get<ChromaticAberration>().intensity.Override(0.08f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    /// URP's material import drops _EMISSION unless the GI flags say it's emissive; enforce both after saving.
    static void FixEmissionKeywords()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || !mat.HasProperty("_EmissionColor") || mat.shader.name != "Universal Render Pipeline/Lit") continue;
            if (mat.GetColor("_EmissionColor").maxColorComponent <= 0.001f) continue;
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
    }

    // ======================= Helpers =======================

    static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent, PrimitiveType type = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    /// Box with a Y rotation (world placement helper).
    static GameObject BoxRot(string name, Vector3 pos, Vector3 size, float yaw, Material mat, Transform parent)
    {
        var go = Box(name, pos, size, mat, parent);
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return go;
    }

    /// Emissive trim with no collider (doesn't affect navmesh or bullets).
    static GameObject Strip(Vector3 pos, Vector3 size, Material mat, Transform parent)
    {
        var go = Box("LightStrip", pos, size, mat, parent);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static void Ramp(string name, Vector3 bottom, Vector3 top, float width, Material mat, Transform parent)
    {
        Vector3 d = top - bottom;
        var go = Box(name, Vector3.zero, new Vector3(width, 0.3f, d.magnitude + 0.4f), mat, parent);
        go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        // Offset down by half thickness so the top surface runs through bottom->top.
        go.transform.position = (bottom + top) * 0.5f - go.transform.up * 0.15f;
    }

    static void PointLight(Vector3 pos, Color color, float range, float intensity, Transform parent)
    {
        var l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.position = pos;
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
    }

    static Transform Pivot(Transform parent, string name, Vector3 pos, Vector3 euler)
    {
        var p = new GameObject(name).transform;
        p.SetParent(parent, false);
        p.localPosition = pos;
        p.localRotation = Quaternion.Euler(euler);
        return p;
    }

    static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat,
        Vector3 euler = default, bool castShadows = false, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }

    /// Attaches a collider-less piece to a scaled mesh, specified in unscaled units relative to the parent's center.
    static void Decor(Transform scaledParent, string name, Vector3 offset, Vector3 size, Material mat, PrimitiveType type = PrimitiveType.Cube)
    {
        Vector3 s = scaledParent.localScale;
        var go = Part(scaledParent, name, type, Div(offset, s), Div(size, s), mat, castShadows: true);
        go.GetComponent<Renderer>().receiveShadows = true;
    }

    /// A box stretched between two points (forearms).
    static void Limb(Transform parent, string name, Vector3 from, Vector3 to, float thickness, Material mat)
    {
        Vector3 d = to - from;
        var go = Part(parent, name, PrimitiveType.Cube, (from + to) * 0.5f, new Vector3(thickness, thickness, d.magnitude), mat);
        go.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    /// Cylinder lying along local Z, spanning z0..z1.
    static GameObject Tube(Transform parent, string name, float y, float z0, float z1, float diameter, Material mat, float x = 0f)
    {
        return Part(parent, name, PrimitiveType.Cylinder, new Vector3(x, y, (z0 + z1) * 0.5f), new Vector3(diameter, (z1 - z0) * 0.5f, diameter), mat, new Vector3(90, 0, 0));
    }

    static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    static Material LitMat(string name, Color color, Texture2D tex = null, Vector2? tiling = null,
        float smoothness = 0.15f, Color? emission = null, float metallic = 0f)
    {
        var mat = GetOrCreateMat(name, "Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material UnlitMat(string name, Color color, string shader = "Universal Render Pipeline/Unlit")
    {
        var mat = GetOrCreateMat(name, shader);
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// Transparent effect material (alpha-blended or additive), unlit or particle shader.
    static Material FxMat(string name, Color color, Texture2D tex, bool additive, bool particles = false, bool twoSided = false)
    {
        var mat = GetOrCreateMat(name, particles ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit");
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", twoSided ? 0f : 2f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetShaderPassEnabled("DepthOnly", false);
        mat.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material GetOrCreateMat(string name, string shaderName)
    {
        string path = $"{MatDir}/{name}.mat";
        var shader = Shader.Find(shaderName);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader) mat.shader = shader;
        return mat;
    }

    /// Generates (or regenerates) a PNG texture from a per-pixel function of UV.
    static Texture2D Tex(string name, int size, bool repeat, Func<float, float, Color> pixel)
    {
        string path = $"{TexDir}/{name}.png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            px[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
        tex.SetPixels(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.alphaIsTransparency = !repeat;
        importer.anisoLevel = repeat ? 8 : 1;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void EnsureLayer(int index, string name)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(index);
        if (layer.stringValue == name) return;
        if (!string.IsNullOrEmpty(layer.stringValue))
        {
            Debug.LogWarning($"[FPS] Layer {index} is '{layer.stringValue}', expected '{name}'. Leaving it alone.");
            return;
        }
        layer.stringValue = name;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }
}
