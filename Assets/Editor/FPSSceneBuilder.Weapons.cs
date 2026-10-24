using UnityEngine;

/// <summary>First-person weapon models (primitives + arms) and their stats.</summary>
public static partial class FPSSceneBuilder
{
    static GameObject GunRoot(Transform holder, string name, Vector3 hip)
    {
        var root = new GameObject(name);
        root.transform.SetParent(holder, false);
        root.transform.localPosition = hip;
        return root;
    }

    // ---------------- Pistol ----------------
    static Gun BuildPistol(Transform holder)
    {
        var t = GunRoot(holder, "Pistol", new Vector3(0.15f, -0.17f, 0.38f)).transform;

        Part(t, "Frame", PrimitiveType.Cube, new Vector3(0, -0.005f, 0.02f), new Vector3(0.034f, 0.04f, 0.17f), gunPolymer);
        Part(t, "TriggerGuard", PrimitiveType.Cube, new Vector3(0, -0.04f, 0.03f), new Vector3(0.012f, 0.008f, 0.05f), gunPolymer);
        Part(t, "Grip", PrimitiveType.Cube, new Vector3(0, -0.065f, -0.045f), new Vector3(0.034f, 0.1f, 0.05f), gunPolymer, new Vector3(-15, 0, 0));
        Tube(t, "BarrelTip", 0.03f, 0.1f, 0.125f, 0.014f, gunMetalLight);

        var slide = Pivot(t, "Slide", new Vector3(0, 0.03f, 0.02f), Vector3.zero);
        Part(slide, "SlideBody", PrimitiveType.Cube, Vector3.zero, new Vector3(0.037f, 0.032f, 0.19f), gunMetal);
        for (int i = 0; i < 4; i++)
            Part(slide, "Serration", PrimitiveType.Cube, new Vector3(0, 0, -0.085f + i * 0.01f), new Vector3(0.039f, 0.026f, 0.004f), gunMetalLight);
        Part(slide, "FrontSight", PrimitiveType.Cube, new Vector3(0, 0.021f, 0.085f), new Vector3(0.006f, 0.01f, 0.006f), gunMetal);
        Part(slide, "FrontDot", PrimitiveType.Cube, new Vector3(0, 0.023f, 0.0885f), new Vector3(0.003f, 0.003f, 0.001f), greenDot);
        Part(slide, "RearSight", PrimitiveType.Cube, new Vector3(0, 0.021f, -0.08f), new Vector3(0.03f, 0.01f, 0.008f), gunMetal);

        var mag = Pivot(t, "Magazine", new Vector3(0, -0.07f, -0.047f), new Vector3(-15, 0, 0));
        Part(mag, "MagBase", PrimitiveType.Cube, new Vector3(0, -0.053f, 0), new Vector3(0.036f, 0.012f, 0.052f), gunMetal);
        Part(mag, "MagBody", PrimitiveType.Cube, new Vector3(0, -0.02f, 0), new Vector3(0.028f, 0.06f, 0.044f), gunMetal);

        // Two-handed grip.
        Part(t, "RightHand", PrimitiveType.Cube, new Vector3(0.004f, -0.06f, -0.048f), new Vector3(0.05f, 0.075f, 0.07f), glove, new Vector3(-15, 0, 0));
        Part(t, "LeftHand", PrimitiveType.Cube, new Vector3(-0.024f, -0.068f, -0.035f), new Vector3(0.04f, 0.07f, 0.075f), glove, new Vector3(-15, 0, 20));
        Limb(t, "RightForearm", new Vector3(0.012f, -0.1f, -0.085f), new Vector3(0.1f, -0.34f, -0.38f), 0.07f, sleeve);
        Limb(t, "LeftForearm", new Vector3(-0.035f, -0.1f, -0.07f), new Vector3(-0.17f, -0.35f, -0.32f), 0.065f, sleeve);

        var port = Pivot(t, "EjectionPort", new Vector3(0.025f, 0.035f, 0.03f), Vector3.zero);
        var muzzle = Pivot(t, "Muzzle", new Vector3(0, 0.03f, 0.13f), Vector3.zero);

        var gun = t.gameObject.AddComponent<Gun>();
        gun.displayName = "Pistol";
        gun.shortName = "PISTOL";
        gun.sound = Gun.ShotSound.Pistol;
        gun.automatic = false;
        gun.fireRate = 7f;
        gun.damage = 26f;
        gun.magazineSize = 12;
        gun.reserveAmmo = 0;
        gun.infiniteReserve = true;
        gun.reloadTime = 1.3f;
        gun.hipSpread = 1.4f;
        gun.adsSpread = 0.35f;
        gun.spreadPerShot = 0.6f;
        gun.recoilUp = 1.0f;
        gun.recoilSide = 0.3f;
        gun.kickBack = 0.035f;
        gun.kickRotation = 6f;
        gun.shake = 0.05f;
        gun.boltKick = 0.035f;
        gun.adsPosition = new Vector3(0, -0.056f, 0.3f);
        gun.adsFovMultiplier = 0.85f;
        gun.adsSpeed = 12f;
        gun.muzzle = muzzle;
        gun.ejectionPort = port;
        gun.magazine = mag;
        gun.bolt = slide;
        gun.muzzleFlash = BuildFlash(muzzle, 0.8f);
        return gun;
    }

    // ---------------- Assault rifle ----------------
    static Gun BuildRifle(Transform holder)
    {
        var t = GunRoot(holder, "Rifle", new Vector3(0.19f, -0.2f, 0.46f)).transform;

        Part(t, "LowerReceiver", PrimitiveType.Cube, new Vector3(0, -0.02f, 0), new Vector3(0.06f, 0.07f, 0.26f), gunMetal);
        Part(t, "UpperReceiver", PrimitiveType.Cube, new Vector3(0, 0.045f, 0.02f), new Vector3(0.065f, 0.06f, 0.3f), gunMetal);
        Part(t, "Rail", PrimitiveType.Cube, new Vector3(0, 0.081f, 0.02f), new Vector3(0.03f, 0.012f, 0.28f), gunMetal);
        for (int i = 0; i < 8; i++)
            Part(t, "RailTooth", PrimitiveType.Cube, new Vector3(0, 0.089f, -0.1f + i * 0.035f), new Vector3(0.036f, 0.006f, 0.012f), gunMetalLight);
        Part(t, "PortCover", PrimitiveType.Cube, new Vector3(0.034f, 0.045f, 0.03f), new Vector3(0.004f, 0.026f, 0.06f), gunMetalLight);
        Part(t, "MagWell", PrimitiveType.Cube, new Vector3(0, -0.055f, 0.06f), new Vector3(0.055f, 0.04f, 0.085f), gunMetal);
        Part(t, "TriggerGuard", PrimitiveType.Cube, new Vector3(0, -0.075f, -0.02f), new Vector3(0.012f, 0.01f, 0.07f), gunMetal);
        Part(t, "Grip", PrimitiveType.Cube, new Vector3(0, -0.085f, -0.09f), new Vector3(0.045f, 0.11f, 0.055f), gunPolymer, new Vector3(-18, 0, 0));
        Part(t, "Handguard", PrimitiveType.Cube, new Vector3(0, 0.03f, 0.27f), new Vector3(0.07f, 0.075f, 0.24f), gunPolymer);
        for (int i = 0; i < 3; i++)
        {
            Part(t, "Vent", PrimitiveType.Cube, new Vector3(0.033f, 0.035f, 0.2f + i * 0.07f), new Vector3(0.008f, 0.015f, 0.04f), gunMetal);
            Part(t, "Vent", PrimitiveType.Cube, new Vector3(-0.033f, 0.035f, 0.2f + i * 0.07f), new Vector3(0.008f, 0.015f, 0.04f), gunMetal);
        }
        Tube(t, "Barrel", 0.035f, 0.39f, 0.55f, 0.025f, gunMetal);
        Tube(t, "MuzzleBrake", 0.035f, 0.537f, 0.593f, 0.042f, gunMetalLight);
        Tube(t, "StockTube", 0.025f, -0.27f, -0.13f, 0.032f, gunMetal);
        Part(t, "Stock", PrimitiveType.Cube, new Vector3(0, 0.0f, -0.29f), new Vector3(0.05f, 0.1f, 0.09f), gunPolymer);
        Part(t, "ButtPad", PrimitiveType.Cube, new Vector3(0, -0.005f, -0.337f), new Vector3(0.052f, 0.11f, 0.012f), glove);

        // Red dot (dot at y=0.125 -> ADS alignment).
        Part(t, "SightBase", PrimitiveType.Cube, new Vector3(0, 0.098f, 0.0f), new Vector3(0.035f, 0.02f, 0.07f), gunMetal);
        Part(t, "SightTop", PrimitiveType.Cube, new Vector3(0, 0.152f, 0.01f), new Vector3(0.046f, 0.008f, 0.045f), gunMetal);
        Part(t, "SightL", PrimitiveType.Cube, new Vector3(-0.021f, 0.125f, 0.01f), new Vector3(0.006f, 0.056f, 0.045f), gunMetal);
        Part(t, "SightR", PrimitiveType.Cube, new Vector3(0.021f, 0.125f, 0.01f), new Vector3(0.006f, 0.056f, 0.045f), gunMetal);
        Part(t, "Dot", PrimitiveType.Sphere, new Vector3(0, 0.125f, 0.03f), Vector3.one * 0.0035f, redDot);

        var mag = Pivot(t, "Magazine", new Vector3(0, -0.06f, 0.06f), new Vector3(10, 0, 0));
        Part(mag, "MagUpper", PrimitiveType.Cube, new Vector3(0, -0.055f, 0), new Vector3(0.045f, 0.11f, 0.075f), gunPolymer);
        Part(mag, "MagLower", PrimitiveType.Cube, new Vector3(0, -0.14f, 0.015f), new Vector3(0.045f, 0.08f, 0.075f), gunPolymer, new Vector3(15, 0, 0));
        Part(mag, "MagBase", PrimitiveType.Cube, new Vector3(0, -0.183f, 0.027f), new Vector3(0.05f, 0.012f, 0.082f), gunMetal, new Vector3(15, 0, 0));
        var bolt = Pivot(t, "Bolt", new Vector3(0.036f, 0.06f, -0.06f), Vector3.zero);
        Part(bolt, "Handle", PrimitiveType.Cube, Vector3.zero, new Vector3(0.022f, 0.014f, 0.035f), gunMetalLight);
        var port = Pivot(t, "EjectionPort", new Vector3(0.045f, 0.045f, 0.03f), Vector3.zero);
        var muzzle = Pivot(t, "Muzzle", new Vector3(0, 0.035f, 0.6f), Vector3.zero);

        Part(t, "RightHand", PrimitiveType.Cube, new Vector3(0.002f, -0.085f, -0.09f), new Vector3(0.058f, 0.085f, 0.09f), glove, new Vector3(-18, 0, 0));
        Part(t, "RightFinger", PrimitiveType.Cube, new Vector3(-0.012f, -0.06f, -0.025f), new Vector3(0.02f, 0.018f, 0.05f), glove);
        Limb(t, "RightForearm", new Vector3(0.015f, -0.12f, -0.13f), new Vector3(0.11f, -0.34f, -0.42f), 0.075f, sleeve);
        Part(t, "LeftHand", PrimitiveType.Cube, new Vector3(-0.012f, -0.012f, 0.3f), new Vector3(0.085f, 0.055f, 0.1f), glove, new Vector3(0, 0, -15));
        Limb(t, "LeftForearm", new Vector3(-0.03f, -0.04f, 0.28f), new Vector3(-0.17f, -0.36f, 0.14f), 0.065f, sleeve);

        var gun = t.gameObject.AddComponent<Gun>();
        gun.displayName = "Assault Rifle";
        gun.shortName = "RIFLE";
        gun.sound = Gun.ShotSound.Rifle;
        gun.automatic = true;
        gun.fireRate = 11f;
        gun.damage = 22f;
        gun.magazineSize = 30;
        gun.reserveAmmo = 150;
        gun.maxReserve = 300;
        gun.ammoPerPickup = 60;
        gun.reloadTime = 2f;
        gun.adsPosition = new Vector3(0, -0.125f, 0.33f);
        gun.adsFovMultiplier = 0.72f;
        gun.muzzle = muzzle;
        gun.ejectionPort = port;
        gun.magazine = mag;
        gun.bolt = bolt;
        gun.muzzleFlash = BuildFlash(muzzle, 1f);
        return gun;
    }

    // ---------------- Shotgun ----------------
    static Gun BuildShotgun(Transform holder)
    {
        var t = GunRoot(holder, "Shotgun", new Vector3(0.19f, -0.21f, 0.42f)).transform;

        Part(t, "Receiver", PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(0.07f, 0.09f, 0.3f), gunMetal);
        Part(t, "RearGroove", PrimitiveType.Cube, new Vector3(0, 0.046f, 0.02f), new Vector3(0.012f, 0.004f, 0.08f), gunMetalLight);
        Tube(t, "Barrel", 0.025f, 0.15f, 0.68f, 0.04f, gunMetal);
        Tube(t, "MagTube", -0.025f, 0.15f, 0.6f, 0.035f, gunMetal);
        Part(t, "BarrelClamp", PrimitiveType.Cube, new Vector3(0, 0, 0.6f), new Vector3(0.05f, 0.095f, 0.02f), gunMetalLight);
        Part(t, "Bead", PrimitiveType.Sphere, new Vector3(0, 0.05f, 0.67f), Vector3.one * 0.012f, brass);
        Part(t, "TriggerGuard", PrimitiveType.Cube, new Vector3(0, -0.055f, -0.07f), new Vector3(0.012f, 0.01f, 0.07f), gunMetal);
        Part(t, "Grip", PrimitiveType.Cube, new Vector3(0, -0.075f, -0.17f), new Vector3(0.05f, 0.11f, 0.065f), wood, new Vector3(-25, 0, 0));
        Part(t, "Stock", PrimitiveType.Cube, new Vector3(0, -0.045f, -0.34f), new Vector3(0.055f, 0.1f, 0.26f), wood, new Vector3(5, 0, 0));
        Part(t, "RecoilPad", PrimitiveType.Cube, new Vector3(0, -0.058f, -0.475f), new Vector3(0.058f, 0.11f, 0.02f), glove, new Vector3(5, 0, 0));
        Part(t, "Saddle", PrimitiveType.Cube, new Vector3(-0.038f, 0.0f, 0.02f), new Vector3(0.006f, 0.05f, 0.14f), gunPolymer);
        for (int i = 0; i < 4; i++)
        {
            Part(t, "SpareShell", PrimitiveType.Cylinder, new Vector3(-0.05f, 0.0f, -0.035f + i * 0.035f), new Vector3(0.024f, 0.028f, 0.024f), shellRed);
            Part(t, "SpareShellBase", PrimitiveType.Cylinder, new Vector3(-0.05f, -0.03f, -0.035f + i * 0.035f), new Vector3(0.025f, 0.006f, 0.025f), brass);
        }

        var pump = Pivot(t, "Pump", new Vector3(0, -0.028f, 0.36f), Vector3.zero);
        Part(pump, "PumpGrip", PrimitiveType.Cube, Vector3.zero, new Vector3(0.062f, 0.062f, 0.18f), wood);
        for (int i = 0; i < 5; i++)
            Part(pump, "Ridge", PrimitiveType.Cube, new Vector3(0, 0, -0.07f + i * 0.035f), new Vector3(0.066f, 0.066f, 0.008f), gunMetal);
        Part(pump, "LeftHand", PrimitiveType.Cube, new Vector3(-0.008f, -0.022f, 0.0f), new Vector3(0.088f, 0.06f, 0.11f), glove, new Vector3(0, 0, -15));
        Limb(pump, "LeftForearm", new Vector3(-0.02f, -0.045f, -0.03f), new Vector3(-0.16f, -0.36f, -0.16f), 0.065f, sleeve);

        Part(t, "RightHand", PrimitiveType.Cube, new Vector3(0.002f, -0.075f, -0.165f), new Vector3(0.06f, 0.085f, 0.09f), glove, new Vector3(-25, 0, 0));
        Limb(t, "RightForearm", new Vector3(0.015f, -0.11f, -0.21f), new Vector3(0.11f, -0.34f, -0.48f), 0.075f, sleeve);

        var port = Pivot(t, "EjectionPort", new Vector3(0.045f, 0.02f, 0.05f), Vector3.zero);
        var muzzle = Pivot(t, "Muzzle", new Vector3(0, 0.025f, 0.69f), Vector3.zero);

        var gun = t.gameObject.AddComponent<Gun>();
        gun.displayName = "Shotgun";
        gun.shortName = "SHOTGUN";
        gun.sound = Gun.ShotSound.Shotgun;
        gun.automatic = false;
        gun.fireRate = 1.3f;
        gun.damage = 13f;
        gun.pellets = 9;
        gun.range = 60f;
        gun.impactForce = 4f;
        gun.hipSpread = 4.5f;
        gun.adsSpread = 3.2f;
        gun.spreadPerShot = 0f;
        gun.magazineSize = 6;
        gun.reserveAmmo = 30;
        gun.maxReserve = 60;
        gun.ammoPerPickup = 12;
        gun.reloadPerShell = true;
        gun.shellInsertTime = 0.42f;
        gun.pumpAction = true;
        gun.cycleDuration = 0.4f;
        gun.recoilUp = 3f;
        gun.recoilSide = 0.8f;
        gun.kickBack = 0.06f;
        gun.kickRotation = 9f;
        gun.shake = 0.25f;
        gun.adsPosition = new Vector3(0, -0.05f, 0.27f);
        gun.adsFovMultiplier = 0.85f;
        gun.muzzle = muzzle;
        gun.ejectionPort = port;
        gun.pump = pump;
        gun.muzzleFlash = BuildFlash(muzzle, 1.7f);
        return gun;
    }

    // ---------------- SMG (suppressed) ----------------
    static Gun BuildSmg(Transform holder)
    {
        var t = GunRoot(holder, "SMG", new Vector3(0.17f, -0.19f, 0.42f)).transform;

        Part(t, "Receiver", PrimitiveType.Cube, new Vector3(0, 0.01f, 0.02f), new Vector3(0.055f, 0.07f, 0.28f), gunMetal);
        Part(t, "TopRail", PrimitiveType.Cube, new Vector3(0, 0.05f, 0.03f), new Vector3(0.025f, 0.01f, 0.2f), gunMetalLight);
        Tube(t, "Shroud", 0.015f, 0.16f, 0.24f, 0.04f, gunMetal);
        Tube(t, "Suppressor", 0.015f, 0.24f, 0.42f, 0.046f, gunPolymer);
        Tube(t, "SuppressorCap", 0.015f, 0.415f, 0.425f, 0.048f, gunMetalLight);
        Part(t, "Grip", PrimitiveType.Cube, new Vector3(0, -0.06f, -0.07f), new Vector3(0.04f, 0.1f, 0.05f), gunPolymer, new Vector3(-15, 0, 0));
        Part(t, "ForeGrip", PrimitiveType.Cube, new Vector3(0, -0.06f, 0.12f), new Vector3(0.032f, 0.08f, 0.032f), gunPolymer);
        Part(t, "TriggerGuard", PrimitiveType.Cube, new Vector3(0, -0.035f, -0.02f), new Vector3(0.012f, 0.01f, 0.06f), gunMetal);
        Part(t, "StockBarL", PrimitiveType.Cube, new Vector3(-0.02f, 0.0f, -0.2f), new Vector3(0.008f, 0.008f, 0.18f), gunMetalLight);
        Part(t, "StockBarR", PrimitiveType.Cube, new Vector3(0.02f, 0.0f, -0.2f), new Vector3(0.008f, 0.008f, 0.18f), gunMetalLight);
        Part(t, "ButtPlate", PrimitiveType.Cube, new Vector3(0, -0.01f, -0.29f), new Vector3(0.05f, 0.07f, 0.012f), glove);

        // Holo sight (dot at y=0.085).
        Part(t, "SightBase", PrimitiveType.Cube, new Vector3(0, 0.062f, 0.0f), new Vector3(0.03f, 0.015f, 0.05f), gunMetal);
        Part(t, "HoloTop", PrimitiveType.Cube, new Vector3(0, 0.107f, 0.01f), new Vector3(0.042f, 0.006f, 0.02f), gunMetal);
        Part(t, "HoloL", PrimitiveType.Cube, new Vector3(-0.019f, 0.085f, 0.01f), new Vector3(0.005f, 0.044f, 0.02f), gunMetal);
        Part(t, "HoloR", PrimitiveType.Cube, new Vector3(0.019f, 0.085f, 0.01f), new Vector3(0.005f, 0.044f, 0.02f), gunMetal);
        Part(t, "Dot", PrimitiveType.Sphere, new Vector3(0, 0.085f, 0.02f), Vector3.one * 0.003f, greenDot);

        var mag = Pivot(t, "Magazine", new Vector3(0, -0.03f, 0.06f), Vector3.zero);
        Part(mag, "MagBody", PrimitiveType.Cube, new Vector3(0, -0.085f, 0), new Vector3(0.032f, 0.17f, 0.045f), gunPolymer);
        Part(mag, "MagBase", PrimitiveType.Cube, new Vector3(0, -0.172f, 0), new Vector3(0.036f, 0.01f, 0.05f), gunMetal);
        var bolt = Pivot(t, "Bolt", new Vector3(-0.032f, 0.03f, 0.05f), Vector3.zero);
        Part(bolt, "Handle", PrimitiveType.Cube, Vector3.zero, new Vector3(0.018f, 0.012f, 0.025f), gunMetalLight);
        var port = Pivot(t, "EjectionPort", new Vector3(0.035f, 0.025f, 0.03f), Vector3.zero);
        var muzzle = Pivot(t, "Muzzle", new Vector3(0, 0.015f, 0.43f), Vector3.zero);

        Part(t, "RightHand", PrimitiveType.Cube, new Vector3(0.002f, -0.06f, -0.07f), new Vector3(0.055f, 0.08f, 0.08f), glove, new Vector3(-15, 0, 0));
        Limb(t, "RightForearm", new Vector3(0.015f, -0.1f, -0.11f), new Vector3(0.11f, -0.34f, -0.4f), 0.075f, sleeve);
        Part(t, "LeftHand", PrimitiveType.Cube, new Vector3(-0.004f, -0.075f, 0.12f), new Vector3(0.06f, 0.07f, 0.065f), glove);
        Limb(t, "LeftForearm", new Vector3(-0.02f, -0.1f, 0.1f), new Vector3(-0.16f, -0.36f, -0.05f), 0.065f, sleeve);

        var gun = t.gameObject.AddComponent<Gun>();
        gun.displayName = "SMG";
        gun.shortName = "SMG";
        gun.sound = Gun.ShotSound.Smg;
        gun.automatic = true;
        gun.fireRate = 15f;
        gun.damage = 14f;
        gun.magazineSize = 35;
        gun.reserveAmmo = 210;
        gun.maxReserve = 420;
        gun.ammoPerPickup = 70;
        gun.reloadTime = 1.8f;
        gun.hipSpread = 2f;
        gun.adsSpread = 0.5f;
        gun.spreadPerShot = 0.25f;
        gun.maxShotSpread = 3f;
        gun.recoilUp = 0.35f;
        gun.recoilSide = 0.3f;
        gun.kickBack = 0.02f;
        gun.kickRotation = 2f;
        gun.shake = 0.03f;
        gun.adsPosition = new Vector3(0, -0.085f, 0.28f);
        gun.adsFovMultiplier = 0.8f;
        gun.adsSpeed = 10f;
        gun.muzzle = muzzle;
        gun.ejectionPort = port;
        gun.magazine = mag;
        gun.bolt = bolt;
        gun.muzzleFlash = BuildFlash(muzzle, 0.5f);
        return gun;
    }

    // ---------------- Sniper rifle (bolt action, scoped) ----------------
    static Gun BuildSniper(Transform holder)
    {
        var t = GunRoot(holder, "Sniper", new Vector3(0.17f, -0.2f, 0.4f)).transform;

        Part(t, "Stock", PrimitiveType.Cube, new Vector3(0, -0.03f, -0.1f), new Vector3(0.06f, 0.08f, 0.5f), olive);
        Part(t, "CheekRest", PrimitiveType.Cube, new Vector3(0, 0.025f, -0.27f), new Vector3(0.05f, 0.03f, 0.14f), olive);
        Part(t, "ButtPad", PrimitiveType.Cube, new Vector3(0, -0.035f, -0.355f), new Vector3(0.062f, 0.12f, 0.015f), glove);
        Part(t, "Grip", PrimitiveType.Cube, new Vector3(0, -0.08f, -0.12f), new Vector3(0.045f, 0.09f, 0.05f), olive, new Vector3(-20, 0, 0));
        Part(t, "Forestock", PrimitiveType.Cube, new Vector3(0, -0.02f, 0.22f), new Vector3(0.055f, 0.06f, 0.26f), olive);
        Part(t, "Receiver", PrimitiveType.Cube, new Vector3(0, 0.03f, 0.03f), new Vector3(0.05f, 0.05f, 0.22f), gunMetal);
        Tube(t, "Barrel", 0.035f, 0.14f, 0.85f, 0.028f, gunMetal);
        Tube(t, "MuzzleBrake", 0.035f, 0.84f, 0.9f, 0.04f, gunMetalLight);
        Part(t, "TriggerGuard", PrimitiveType.Cube, new Vector3(0, -0.065f, -0.05f), new Vector3(0.012f, 0.01f, 0.06f), gunMetal);
        Part(t, "BipodL", PrimitiveType.Cube, new Vector3(-0.012f, -0.015f, 0.42f), new Vector3(0.008f, 0.008f, 0.16f), gunMetal);
        Part(t, "BipodR", PrimitiveType.Cube, new Vector3(0.012f, -0.015f, 0.42f), new Vector3(0.008f, 0.008f, 0.16f), gunMetal);

        // Scope.
        Part(t, "RingRear", PrimitiveType.Cube, new Vector3(0, 0.075f, -0.02f), new Vector3(0.05f, 0.05f, 0.02f), gunMetal);
        Part(t, "RingFront", PrimitiveType.Cube, new Vector3(0, 0.075f, 0.12f), new Vector3(0.05f, 0.05f, 0.02f), gunMetal);
        Tube(t, "ScopeTube", 0.105f, -0.1f, 0.2f, 0.042f, gunMetal);
        Tube(t, "Objective", 0.105f, 0.2f, 0.27f, 0.062f, gunMetal);
        Tube(t, "Eyepiece", 0.105f, -0.15f, -0.09f, 0.054f, gunMetal);
        Tube(t, "LensFront", 0.105f, 0.268f, 0.272f, 0.055f, lensMat);
        Part(t, "Turret", PrimitiveType.Cylinder, new Vector3(0, 0.135f, 0.05f), new Vector3(0.025f, 0.012f, 0.025f), gunMetalLight);

        var bolt = Pivot(t, "Bolt", new Vector3(0.028f, 0.04f, -0.03f), Vector3.zero);
        Part(bolt, "Shaft", PrimitiveType.Cube, new Vector3(0.025f, 0, 0), new Vector3(0.05f, 0.01f, 0.01f), gunMetalLight);
        Part(bolt, "Knob", PrimitiveType.Sphere, new Vector3(0.052f, 0, 0), Vector3.one * 0.022f, gunMetal);
        var mag = Pivot(t, "Magazine", new Vector3(0, -0.055f, 0.06f), Vector3.zero);
        Part(mag, "MagBody", PrimitiveType.Cube, new Vector3(0, -0.015f, 0), new Vector3(0.04f, 0.04f, 0.075f), gunMetal);
        var port = Pivot(t, "EjectionPort", new Vector3(0.035f, 0.04f, 0.04f), Vector3.zero);
        var muzzle = Pivot(t, "Muzzle", new Vector3(0, 0.035f, 0.91f), Vector3.zero);

        Part(t, "RightHand", PrimitiveType.Cube, new Vector3(0.002f, -0.08f, -0.12f), new Vector3(0.056f, 0.08f, 0.085f), glove, new Vector3(-20, 0, 0));
        Limb(t, "RightForearm", new Vector3(0.015f, -0.115f, -0.16f), new Vector3(0.11f, -0.34f, -0.45f), 0.075f, sleeve);
        Part(t, "LeftHand", PrimitiveType.Cube, new Vector3(-0.01f, -0.05f, 0.24f), new Vector3(0.08f, 0.055f, 0.1f), glove, new Vector3(0, 0, -15));
        Limb(t, "LeftForearm", new Vector3(-0.03f, -0.075f, 0.22f), new Vector3(-0.17f, -0.37f, 0.08f), 0.065f, sleeve);

        var gun = t.gameObject.AddComponent<Gun>();
        gun.displayName = "Sniper Rifle";
        gun.shortName = "SNIPER";
        gun.sound = Gun.ShotSound.Sniper;
        gun.automatic = false;
        gun.fireRate = 0.9f;
        gun.damage = 110f;
        gun.pierce = 2;
        gun.range = 400f;
        gun.impactForce = 20f;
        gun.hipSpread = 4f;
        gun.adsSpread = 0f;
        gun.moveSpreadBonus = 3f;
        gun.spreadPerShot = 0f;
        gun.magazineSize = 5;
        gun.reserveAmmo = 25;
        gun.maxReserve = 40;
        gun.ammoPerPickup = 8;
        gun.reloadTime = 2.6f;
        gun.boltAction = true;
        gun.cycleDelay = 0.25f;
        gun.cycleDuration = 0.65f;
        gun.recoilUp = 4f;
        gun.recoilSide = 0.5f;
        gun.kickBack = 0.08f;
        gun.kickRotation = 8f;
        gun.shake = 0.3f;
        gun.adsPosition = new Vector3(0, -0.105f, 0.22f);
        gun.adsFovMultiplier = 0.25f;
        gun.adsSpeed = 6f;
        gun.scope = true;
        gun.muzzle = muzzle;
        gun.ejectionPort = port;
        gun.magazine = mag;
        gun.bolt = bolt;
        gun.muzzleFlash = BuildFlash(muzzle, 1.8f);
        return gun;
    }

    // ---------------- Rocket launcher ----------------
    static Gun BuildRocketLauncher(Transform holder)
    {
        var t = GunRoot(holder, "RocketLauncher", new Vector3(0.21f, -0.21f, 0.36f)).transform;

        Tube(t, "Tube", 0.03f, -0.45f, 0.5f, 0.13f, olive);
        Tube(t, "RearFlare", 0.03f, -0.5f, -0.45f, 0.155f, gunMetal);
        Tube(t, "FrontRing", 0.03f, 0.48f, 0.52f, 0.14f, gunMetal);
        Tube(t, "MidBand", 0.03f, 0.05f, 0.09f, 0.135f, gunMetal);
        Part(t, "Grip", PrimitiveType.Cube, new Vector3(0, -0.08f, -0.05f), new Vector3(0.04f, 0.1f, 0.05f), gunPolymer, new Vector3(-10, 0, 0));
        Part(t, "FrontGrip", PrimitiveType.Cube, new Vector3(0, -0.08f, 0.18f), new Vector3(0.035f, 0.09f, 0.04f), gunPolymer);
        Part(t, "ShoulderPad", PrimitiveType.Cube, new Vector3(0, -0.04f, -0.2f), new Vector3(0.08f, 0.04f, 0.16f), glove);
        Part(t, "HazardBand", PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0.38f), new Vector3(0.132f, 0.02f, 0.132f), hazardMat, new Vector3(90, 0, 0));

        // Side sight (dot at x=-0.085, y=0.1 -> ADS alignment).
        Part(t, "SightArm", PrimitiveType.Cube, new Vector3(-0.07f, 0.06f, 0.05f), new Vector3(0.03f, 0.01f, 0.02f), gunMetal);
        Part(t, "SightPost", PrimitiveType.Cube, new Vector3(-0.085f, 0.08f, 0.05f), new Vector3(0.008f, 0.04f, 0.008f), gunMetal);
        Part(t, "SightFrame", PrimitiveType.Cube, new Vector3(-0.085f, 0.1f, 0.05f), new Vector3(0.03f, 0.03f, 0.004f), gunMetal);
        Part(t, "SightDot", PrimitiveType.Sphere, new Vector3(-0.085f, 0.1f, 0.047f), Vector3.one * 0.004f, greenDot);

        var round = Pivot(t, "LoadedRound", new Vector3(0, 0.03f, 0.5f), Vector3.zero);
        Part(round, "Warhead", PrimitiveType.Sphere, new Vector3(0, 0, 0.03f), new Vector3(0.1f, 0.1f, 0.16f), rocketBodyMat);
        Part(round, "Tip", PrimitiveType.Sphere, new Vector3(0, 0, 0.1f), Vector3.one * 0.03f, explosiveMat);

        Part(t, "RightHand", PrimitiveType.Cube, new Vector3(0.002f, -0.08f, -0.05f), new Vector3(0.055f, 0.08f, 0.08f), glove, new Vector3(-10, 0, 0));
        Limb(t, "RightForearm", new Vector3(0.015f, -0.12f, -0.08f), new Vector3(0.1f, -0.36f, -0.36f), 0.075f, sleeve);
        Part(t, "LeftHand", PrimitiveType.Cube, new Vector3(-0.004f, -0.085f, 0.18f), new Vector3(0.06f, 0.075f, 0.07f), glove);
        Limb(t, "LeftForearm", new Vector3(-0.02f, -0.12f, 0.16f), new Vector3(-0.18f, -0.38f, -0.02f), 0.065f, sleeve);

        var muzzle = Pivot(t, "Muzzle", new Vector3(0, 0.03f, 0.56f), Vector3.zero);
        var backblast = Pivot(t, "Backblast", new Vector3(0, 0.03f, -0.52f), new Vector3(0, 180, 0));

        var gun = t.gameObject.AddComponent<Gun>();
        gun.displayName = "Rocket Launcher";
        gun.shortName = "ROCKET";
        gun.sound = Gun.ShotSound.Rocket;
        gun.automatic = false;
        gun.fireRate = 1f;
        gun.damage = 140f;
        gun.projectileSpeed = 32f;
        gun.projectileKind = Projectile.Kind.Rocket;
        gun.splashRadius = 5.5f;
        gun.hipSpread = 0.5f;
        gun.adsSpread = 0f;
        gun.spreadPerShot = 0f;
        gun.magazineSize = 1;
        gun.reserveAmmo = 6;
        gun.maxReserve = 12;
        gun.ammoPerPickup = 2;
        gun.reloadTime = 2.2f;
        gun.recoilUp = 3f;
        gun.recoilSide = 0.5f;
        gun.kickBack = 0.12f;
        gun.kickRotation = 6f;
        gun.shake = 0.4f;
        gun.adsPosition = new Vector3(0.085f, -0.1f, 0.3f);
        gun.adsFovMultiplier = 0.8f;
        gun.adsSpeed = 6f;
        gun.muzzle = muzzle;
        gun.backblast = backblast;
        gun.loadedRound = round.gameObject;
        gun.muzzleFlash = BuildFlash(muzzle, 2f);
        return gun;
    }

    /// Star-shaped flash from additive soft quads + a point light.
    static GameObject BuildFlash(Transform muzzle, float size)
    {
        var flash = new GameObject("MuzzleFlash");
        flash.transform.SetParent(muzzle, false);
        Part(flash.transform, "Front", PrimitiveType.Quad, new Vector3(0, 0, 0.01f), Vector3.one * 0.13f * size, flashMat);
        Part(flash.transform, "SideA", PrimitiveType.Quad, new Vector3(0, 0, 0.1f * size), new Vector3(0.24f, 0.08f, 1f) * size, flashMat, new Vector3(0, 90, 0));
        Part(flash.transform, "SideB", PrimitiveType.Quad, new Vector3(0, 0, 0.1f * size), new Vector3(0.24f, 0.08f, 1f) * size, flashMat, new Vector3(0, 90, 90));
        Part(flash.transform, "Burst", PrimitiveType.Quad, new Vector3(0, 0, 0.02f), new Vector3(0.28f, 0.035f, 1f) * size, flashMat, new Vector3(0, 0, 45));
        Part(flash.transform, "Burst2", PrimitiveType.Quad, new Vector3(0, 0, 0.02f), new Vector3(0.28f, 0.035f, 1f) * size, flashMat, new Vector3(0, 0, -45));
        var light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(flash.transform, false);
        light.transform.localPosition = new Vector3(0, 0, 0.1f);
        light.type = LightType.Point;
        light.range = 7f;
        light.intensity = 4f;
        light.color = new Color(1f, 0.7f, 0.35f);
        light.shadows = LightShadows.None;
        flash.SetActive(false);
        return flash;
    }
}
