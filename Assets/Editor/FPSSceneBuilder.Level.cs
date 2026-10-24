using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Level layout: a 124x124m arena with distinct zones.</summary>
public static partial class FPSSceneBuilder
{
    public struct LevelInfo
    {
        public Vector3 playerStart;
        public Transform[] spawnPoints;
    }

    const float Half = 62f;
    const float WallHeight = 7f;

    static LevelInfo BuildLevel()
    {
        var env = new GameObject("Environment").transform;
        var rng = new System.Random(7);

        // ---------- Shell ----------
        Box("Ground", new Vector3(0, -0.5f, 0), new Vector3(Half * 2, 1, Half * 2), groundMat, env);
        Box("Wall_N", new Vector3(0, WallHeight / 2, Half + 0.5f), new Vector3(Half * 2 + 2, WallHeight, 1), wallMat, env);
        Box("Wall_S", new Vector3(0, WallHeight / 2, -Half - 0.5f), new Vector3(Half * 2 + 2, WallHeight, 1), wallMat, env);
        Box("Wall_E", new Vector3(Half + 0.5f, WallHeight / 2, 0), new Vector3(1, WallHeight, Half * 2 + 2), wallMat, env);
        Box("Wall_W", new Vector3(-Half - 0.5f, WallHeight / 2, 0), new Vector3(1, WallHeight, Half * 2 + 2), wallMat, env);
        Strip(new Vector3(0, WallHeight - 1.2f, Half - 0.05f), new Vector3(Half * 2, 0.14f, 0.08f), stripMat, env);
        Strip(new Vector3(0, WallHeight - 1.2f, -Half + 0.05f), new Vector3(Half * 2, 0.14f, 0.08f), stripMat, env);
        Strip(new Vector3(Half - 0.05f, WallHeight - 1.2f, 0), new Vector3(0.08f, 0.14f, Half * 2), stripCoolMat, env);
        Strip(new Vector3(-Half + 0.05f, WallHeight - 1.2f, 0), new Vector3(0.08f, 0.14f, Half * 2), stripCoolMat, env);

        BuildCenterTower(env);
        BuildWestBridge(env);
        BuildContainerYard(env, rng);
        BuildWarehouse(env, rng);
        BuildSouthPlaza(env);
        BuildScatter(env, rng);
        var spawns = BuildSpawnGates(env);

        // Entrance: the south end behind the plaza sandbags, facing north into the arena (the 180-degree front).
        return new LevelInfo { playerStart = new Vector3(0, 0.05f, -50f), spawnPoints = spawns };
    }

    // ---------- Center: raised tower with two ramps and pillars ----------
    static void BuildCenterTower(Transform env)
    {
        var root = new GameObject("CenterTower").transform;
        root.SetParent(env, false);
        Box("Platform", new Vector3(0, 1.5f, 0), new Vector3(14, 3, 14), platformMat, root);
        Ramp("RampS", new Vector3(0, 0, -15.5f), new Vector3(0, 3, -7.2f), 4f, platformMat, root);
        Ramp("RampN", new Vector3(0, 0, 15.5f), new Vector3(0, 3, 7.2f), 4f, platformMat, root);
        EdgeStrips(new Vector3(0, 2.95f, 0), 14f, 14f, stripMat, root);
        // Waist-high cover on top.
        Box("TopCover", new Vector3(-5, 3.5f, 0), new Vector3(1, 1, 5), coverMat, root);
        Box("TopCover", new Vector3(5, 3.5f, 0), new Vector3(1, 1, 5), coverMat, root);
        Box("TopCover", new Vector3(-3.5f, 3.5f, 5.5f), new Vector3(3, 1, 0.8f), coverMat, root);
        Box("TopCover", new Vector3(3.5f, 3.5f, -5.5f), new Vector3(3, 1, 0.8f), coverMat, root);
        PointLight(new Vector3(0, 7f, 0), new Color(1f, 0.65f, 0.35f), 16f, 3f, root);

        foreach (var p in new[] { new Vector3(12, 2.5f, 12), new Vector3(-12, 2.5f, 12), new Vector3(12, 2.5f, -12), new Vector3(-12, 2.5f, -12) })
        {
            Box("Pillar", p, new Vector3(2, 5, 2), coverMat, root);
            Strip(p + Vector3.up * 1.5f, new Vector3(2.06f, 0.12f, 2.06f), stripMat, root);
        }
    }

    // ---------- West: two platforms joined by a catwalk bridge ----------
    static void BuildWestBridge(Transform env)
    {
        var root = new GameObject("WestBridge").transform;
        root.SetParent(env, false);
        const float x = -44f;
        foreach (float z in new[] { -22f, 22f })
        {
            Box("Platform", new Vector3(x, 1.5f, z), new Vector3(12, 3, 12), platformMat, root);
            Ramp("Ramp", new Vector3(x + 14.5f, 0, z), new Vector3(x + 6.2f, 3, z), 4f, platformMat, root);
            EdgeStrips(new Vector3(x, 2.95f, z), 12f, 12f, stripCoolMat, root);
            Box("Crate", new Vector3(x - 3.5f, 3.6f, z + (z > 0 ? 3.5f : -3.5f)), Vector3.one * 1.2f, crateMat, root);
            Box("Cover", new Vector3(x + 4.5f, 3.5f, z + (z > 0 ? -2f : 2f)), new Vector3(0.8f, 1f, 4f), coverMat, root);
            PointLight(new Vector3(x, 6.5f, z), new Color(0.4f, 0.7f, 1f), 14f, 2.5f, root);
        }

        // Catwalk deck, railings and side supports (keeps the space under it walkable).
        Box("Bridge", new Vector3(x, 2.8f, 0), new Vector3(4, 0.4f, 32.2f), platformMat, root);
        Box("Railing", new Vector3(x - 2.05f, 3.5f, 0), new Vector3(0.15f, 1f, 32f), postMat, root);
        Box("Railing", new Vector3(x + 2.05f, 3.5f, 0), new Vector3(0.15f, 1f, 32f), postMat, root);
        Strip(new Vector3(x - 2.05f, 4.02f, 0), new Vector3(0.18f, 0.05f, 32f), stripCoolMat, root);
        Strip(new Vector3(x + 2.05f, 4.02f, 0), new Vector3(0.18f, 0.05f, 32f), stripCoolMat, root);
        foreach (float z in new[] { -7f, 7f })
        {
            Box("Support", new Vector3(x - 1.75f, 1.3f, z), new Vector3(0.5f, 2.6f, 0.5f), postMat, root);
            Box("Support", new Vector3(x + 1.75f, 1.3f, z), new Vector3(0.5f, 2.6f, 0.5f), postMat, root);
        }
    }

    // ---------- East: shipping container maze ----------
    static void BuildContainerYard(Transform env, System.Random rng)
    {
        var root = new GameObject("ContainerYard").transform;
        root.SetParent(env, false);
        var layout = new (float x, float z, float yaw, bool stacked)[]
        {
            (30, -32, 0, false), (37, -30, 0, true), (50, -28, 90, false),
            (30, -18, 0, true), (42, -16, 90, false), (53, -12, 0, true),
            (31, -4, 0, false), (38, -2, 0, true), (50, 2, 90, false),
            (30, 10, 0, false), (42, 12, 0, false), (53, 18, 90, true),
            (34, 24, 0, true), (45, 28, 0, false), (54, 36, 0, false), (36, 38, 90, false),
        };
        int i = 0;
        foreach (var (x, z, yaw, stacked) in layout)
        {
            Container(root, new Vector3(x, 0, z), yaw, containerMats[i++ % containerMats.Length]);
            if (stacked) Container(root, new Vector3(x, 2.6f, z), yaw + (float)(rng.NextDouble() * 6 - 3), containerMats[(i + 1) % containerMats.Length]);
        }
        PointLight(new Vector3(40, 6f, -10), new Color(1f, 0.75f, 0.5f), 18f, 2.5f, root);
        PointLight(new Vector3(42, 6f, 20), new Color(1f, 0.75f, 0.5f), 18f, 2.5f, root);
    }

    static void Container(Transform parent, Vector3 basePos, float yaw, Material mat)
    {
        var c = BoxRot("Container", basePos + Vector3.up * 1.3f, new Vector3(2.5f, 2.6f, 6.1f), yaw, mat, parent);
        // Door end, frame rails (decor only; the body collider is enough).
        Part(c.transform, "Doors", PrimitiveType.Cube, new Vector3(0, 0, 0.501f), new Vector3(0.96f, 0.94f, 0.01f), crateTrimMat, castShadows: false);
        Part(c.transform, "Bar", PrimitiveType.Cube, new Vector3(-0.15f, 0, 0.51f), new Vector3(0.02f, 0.9f, 0.01f), gunMetalLight);
        Part(c.transform, "Bar", PrimitiveType.Cube, new Vector3(0.15f, 0, 0.51f), new Vector3(0.02f, 0.9f, 0.01f), gunMetalLight);
        Part(c.transform, "TopRail", PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(1.02f, 0.03f, 1.01f), postMat, castShadows: true);
        Part(c.transform, "BottomRail", PrimitiveType.Cube, new Vector3(0, -0.49f, 0), new Vector3(1.02f, 0.03f, 1.01f), postMat);
    }

    // ---------- North: enclosed warehouse with doors, shelves and lights ----------
    static void BuildWarehouse(Transform env, System.Random rng)
    {
        var root = new GameObject("Warehouse").transform;
        root.SetParent(env, false);
        const float x0 = -20f, x1 = 20f, z0 = 30f, z1 = 54f, t = 0.8f, h = WallHeight;
        const float doorW = 4.5f, doorH = 4.5f;

        WallWithDoors(root, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), h, t, new[] { -10f, 10f }, doorW, doorH);
        WallWithDoors(root, new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), h, t, new float[0], doorW, doorH);
        WallWithDoors(root, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), h, t, new[] { 42f }, doorW, doorH);
        WallWithDoors(root, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), h, t, new[] { 42f }, doorW, doorH);
        Box("Roof", new Vector3(0, h + 0.25f, (z0 + z1) / 2), new Vector3(x1 - x0 + t, 0.5f, z1 - z0 + t), roofMat, root);

        // Door frames glow so entrances read from afar.
        foreach (float dx in new[] { -10f, 10f })
            DoorFrame(root, new Vector3(dx, 0, z0 - t * 0.5f - 0.02f), 0f, doorW, doorH);
        DoorFrame(root, new Vector3(x0 - t * 0.5f - 0.02f, 0, 42f), 90f, doorW, doorH);
        DoorFrame(root, new Vector3(x1 + t * 0.5f + 0.02f, 0, 42f), 90f, doorW, doorH);

        // Shelving aisles.
        foreach (var (sx, sz) in new[] { (-9f, 37f), (7f, 37f), (-9f, 44f), (7f, 44f), (-1f, 50.5f) })
        {
            var shelf = Box("Shelf", new Vector3(sx, 1.6f, sz), new Vector3(9f, 3.2f, 1.4f), shelfMat, root);
            for (int i = 0; i < 3; i++)
                Part(shelf.transform, "Board", PrimitiveType.Cube, new Vector3(0, -0.35f + i * 0.33f, 0), new Vector3(1.01f, 0.03f, 1.05f), crateTrimMat, castShadows: true);
            for (int i = 0; i < 4; i++)
                Part(shelf.transform, "Box", PrimitiveType.Cube, new Vector3(-0.35f + i * 0.23f, -0.2f + (i % 2) * 0.33f, 0), new Vector3(0.12f, 0.2f, 0.7f), crateMat, castShadows: true);
        }
        Box("Crate", new Vector3(-15f, 0.6f, 33f), Vector3.one * 1.2f, crateMat, root);
        Box("Crate", new Vector3(16f, 0.75f, 49f), Vector3.one * 1.5f, crateMat, root);
        Box("Crate", new Vector3(15.8f, 2.1f, 49.2f), Vector3.one * 1.2f, crateMat, root);

        // Ceiling lights.
        foreach (var lp in new[] { new Vector3(-10, h - 0.5f, 40), new Vector3(10, h - 0.5f, 40), new Vector3(-10, h - 0.5f, 48), new Vector3(10, h - 0.5f, 48) })
        {
            Strip(lp + Vector3.up * 0.3f, new Vector3(4f, 0.1f, 0.3f), lampMat, root);
            PointLight(lp, new Color(1f, 0.9f, 0.75f), 13f, 3.5f, root);
        }
    }

    static void WallWithDoors(Transform parent, Vector3 a, Vector3 b, float height, float thickness, float[] doorCenters, float doorW, float doorH)
    {
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float start = alongX ? a.x : a.z, end = alongX ? b.x : b.z;
        var cuts = new List<float>(doorCenters);
        cuts.Sort();
        float cursor = start;

        void Segment(float s0, float s1, float y0, float y1)
        {
            if (s1 - s0 < 0.01f) return;
            float mid = (s0 + s1) * 0.5f, len = s1 - s0;
            Vector3 pos = alongX ? new Vector3(mid, (y0 + y1) / 2, a.z) : new Vector3(a.x, (y0 + y1) / 2, mid);
            Vector3 size = alongX ? new Vector3(len, y1 - y0, thickness) : new Vector3(thickness, y1 - y0, len);
            Box("WarehouseWall", pos, size, warehouseMat, parent);
        }

        foreach (float c in cuts)
        {
            Segment(cursor, c - doorW / 2, 0, height);
            Segment(c - doorW / 2, c + doorW / 2, doorH, height);
            cursor = c + doorW / 2;
        }
        Segment(cursor, end + (alongX ? thickness / 2 : 0), 0, height);
    }

    static void DoorFrame(Transform parent, Vector3 bottomCenter, float yaw, float w, float h)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        Strip(bottomCenter + rot * new Vector3(-w / 2, h / 2, 0), new Vector3(0.12f, h, 0.06f), stripRedMat, parent).transform.localRotation = rot;
        Strip(bottomCenter + rot * new Vector3(w / 2, h / 2, 0), new Vector3(0.12f, h, 0.06f), stripRedMat, parent).transform.localRotation = rot;
        Strip(bottomCenter + rot * new Vector3(0, h, 0), new Vector3(w, 0.12f, 0.06f), stripRedMat, parent).transform.localRotation = rot;
    }

    // ---------- South: player start with sandbag lines ----------
    static void BuildSouthPlaza(Transform env)
    {
        var root = new GameObject("SouthPlaza").transform;
        root.SetParent(env, false);
        foreach (var (pos, size) in new[]
                 {
                     (new Vector3(-9, 0.5f, -40), new Vector3(6, 1, 0.9f)), (new Vector3(9, 0.5f, -40), new Vector3(6, 1, 0.9f)),
                     (new Vector3(0, 0.5f, -33), new Vector3(5, 1, 0.9f)), (new Vector3(-18, 0.5f, -28), new Vector3(0.9f, 1, 5)),
                     (new Vector3(18, 0.5f, -28), new Vector3(0.9f, 1, 5)),
                 })
        {
            var bag = Box("Sandbags", pos, size, sandbagMat, root);
            Part(bag.transform, "Top", PrimitiveType.Cube, new Vector3(0, 0.5f, 0), new Vector3(0.96f, 0.08f, 0.9f), sandbagMat, castShadows: true);
        }
        foreach (var p in new[] { new Vector3(-14, 0, -46), new Vector3(14, 0, -46), new Vector3(-14, 0, -24), new Vector3(14, 0, -24) })
            LampPost(root, p);
    }

    // ---------- Scattered cover, barrels and lamp posts ----------
    static void BuildScatter(Transform env, System.Random rng)
    {
        var root = new GameObject("Props").transform;
        root.SetParent(env, false);

        var crates = new (Vector3 pos, float size)[]
        {
            (new Vector3(-8, 0.6f, -12), 1.2f), (new Vector3(-6.7f, 0.6f, -12.3f), 1.2f), (new Vector3(-7.3f, 1.8f, -12.1f), 1.2f),
            (new Vector3(9, 0.6f, -18), 1.2f), (new Vector3(10.2f, 0.6f, -18.1f), 1.2f), (new Vector3(16, 0.75f, -6), 1.5f),
            (new Vector3(-17, 0.75f, 6), 1.5f), (new Vector3(-12, 0.5f, 20), 1f), (new Vector3(8, 1, 22), 2f),
            (new Vector3(-26, 0.6f, -6), 1.2f), (new Vector3(22, 0.6f, -26), 1.2f), (new Vector3(-30, 0.75f, 40), 1.5f),
            (new Vector3(-24, 0.6f, -44), 1.2f), (new Vector3(26, 0.6f, -44), 1.2f), (new Vector3(-38, 0.6f, -2), 1.2f),
            (new Vector3(-52, 0.75f, 40), 1.5f), (new Vector3(-52, 0.75f, -40), 1.5f),
        };
        foreach (var c in crates)
        {
            var crate = Box("Crate", c.pos, Vector3.one * c.size, crateMat, root);
            crate.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 30f - 15f, 0);
            foreach (var corner in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                Part(crate.transform, "Trim", PrimitiveType.Cube, new Vector3(corner.x * 0.47f, 0, corner.y * 0.47f), new Vector3(0.08f, 1.01f, 0.08f), crateTrimMat, castShadows: true);
        }

        foreach (var p in new[] { new Vector3(3, 0.55f, -10), new Vector3(-14, 0.55f, -4), new Vector3(14, 0.55f, 8), new Vector3(-6, 0.55f, 24), new Vector3(25, 0.55f, 30) })
            Barrel(root, p, false);
        foreach (var p in new[]
                 {
                     new Vector3(4.2f, 0.55f, -10.4f), new Vector3(-20, 0.55f, 14), new Vector3(-21, 0.55f, 14.8f), new Vector3(27, 0.55f, -10),
                     new Vector3(33.5f, 0.55f, 4), new Vector3(-12, 0.55f, 33), new Vector3(12, 0.55f, 40.5f), new Vector3(-40, 0.55f, 0),
                     new Vector3(0, 0.55f, -22), new Vector3(46, 0.55f, -22),
                 })
            Barrel(root, p, true);

        foreach (var p in new[] { new Vector3(-24, 0, 24), new Vector3(24, 0, -14), new Vector3(-30, 0, -30), new Vector3(22, 0, 44), new Vector3(-26, 0, 50), new Vector3(52, 0, -48) })
            LampPost(root, p);
    }

    static void Barrel(Transform parent, Vector3 pos, bool explosive)
    {
        var b = Box(explosive ? "ExplosiveBarrel" : "Barrel", pos, new Vector3(0.7f, 0.55f, 0.7f), explosive ? explosiveMat : barrelMat, parent, PrimitiveType.Cylinder);
        if (explosive)
            Part(b.transform, "Hazard", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.02f, 0.25f, 1.02f), hazardMat, castShadows: true);
        Part(b.transform, "Band", PrimitiveType.Cylinder, new Vector3(0, 0.6f, 0), new Vector3(1.03f, 0.04f, 1.03f), barrelBandMat, castShadows: true);
        Part(b.transform, "Band", PrimitiveType.Cylinder, new Vector3(0, -0.6f, 0), new Vector3(1.03f, 0.04f, 1.03f), barrelBandMat, castShadows: true);
        var rb = b.AddComponent<Rigidbody>();
        rb.mass = 20f;
        b.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var obstacle = b.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.carving = true;
        if (explosive) b.AddComponent<ExplosiveBarrel>();
    }

    static void LampPost(Transform parent, Vector3 basePos)
    {
        Box("LampPost", basePos + Vector3.up * 2.25f, new Vector3(0.22f, 4.5f, 0.22f), postMat, parent);
        Strip(basePos + new Vector3(0, 4.55f, 0), new Vector3(0.6f, 0.15f, 0.6f), lampMat, parent);
        PointLight(basePos + Vector3.up * 4.2f, new Color(1f, 0.8f, 0.55f), 11f, 1.8f, parent);
    }

    // ---------- Spawn gates around the edge ----------
    static Transform[] BuildSpawnGates(Transform env)
    {
        var root = new GameObject("SpawnPoints").transform;
        root.SetParent(env, false);
        var positions = new[]
        {
            new Vector3(-56, 0, 56), new Vector3(0, 0, 58), new Vector3(56, 0, 56), new Vector3(-30, 0, 58), new Vector3(30, 0, 58),
            new Vector3(-58, 0, 0), new Vector3(58, 0, 0), new Vector3(-58, 0, -38), new Vector3(58, 0, -40),
            new Vector3(-56, 0, -56), new Vector3(56, 0, -56), new Vector3(-44, 3f, 22),
        };
        var points = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var sp = new GameObject($"Spawn_{i}").transform;
            sp.SetParent(root, false);
            sp.position = positions[i];
            Vector3 toCenter = -positions[i];
            toCenter.y = 0f;
            sp.rotation = Quaternion.LookRotation(toCenter);
            points[i] = sp;
            if (positions[i].y > 0.5f) continue; // platform spawn has no gate

            // Decorative arch (no colliders so it never blocks the navmesh).
            var gate = new GameObject("Gate").transform;
            gate.SetParent(sp, false);
            Part(gate, "PostL", PrimitiveType.Cube, new Vector3(-2.3f, 2f, 0), new Vector3(0.5f, 4f, 0.5f), postMat, castShadows: true);
            Part(gate, "PostR", PrimitiveType.Cube, new Vector3(2.3f, 2f, 0), new Vector3(0.5f, 4f, 0.5f), postMat, castShadows: true);
            Part(gate, "Beam", PrimitiveType.Cube, new Vector3(0, 4.15f, 0), new Vector3(5.1f, 0.4f, 0.5f), postMat, castShadows: true);
            Part(gate, "Glow", PrimitiveType.Cube, new Vector3(0, 3.9f, 0), new Vector3(4.2f, 0.1f, 0.52f), stripRedMat);
            Part(gate, "GlowL", PrimitiveType.Cube, new Vector3(-2.3f, 2f, 0), new Vector3(0.52f, 3.2f, 0.1f), stripRedMat);
            Part(gate, "GlowR", PrimitiveType.Cube, new Vector3(2.3f, 2f, 0), new Vector3(0.52f, 3.2f, 0.1f), stripRedMat);
            PointLight(positions[i] + Vector3.up * 3f, new Color(1f, 0.25f, 0.2f), 8f, 2f, gate);
        }
        return points;
    }

    static void EdgeStrips(Vector3 topCenter, float sizeX, float sizeZ, Material mat, Transform parent)
    {
        Strip(topCenter + new Vector3(0, 0, sizeZ / 2 + 0.02f), new Vector3(sizeX, 0.08f, 0.06f), mat, parent);
        Strip(topCenter + new Vector3(0, 0, -sizeZ / 2 - 0.02f), new Vector3(sizeX, 0.08f, 0.06f), mat, parent);
        Strip(topCenter + new Vector3(sizeX / 2 + 0.02f, 0, 0), new Vector3(0.06f, 0.08f, sizeZ), mat, parent);
        Strip(topCenter + new Vector3(-sizeX / 2 - 0.02f, 0, 0), new Vector3(0.06f, 0.08f, sizeZ), mat, parent);
    }
}
