using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Enemy prefabs: configurable walking robots plus a hovering drone.</summary>
public static partial class FPSSceneBuilder
{
    enum ArmStyle { Fists, Claws, Gun, Hammers }

    class RobotSpec
    {
        public string name;
        public float scale = 1f, bulk = 1f;
        public Material body, joint, glow;
        public Color glowColor;
        public ArmStyle arms = ArmStyle.Fists;
        public bool hunched, backTank, antenna, horns;
        public float agentRadius = 0.45f, agentHeight = 2f;
        public float headMultiplier = 2f;
        public Action<Enemy, EnemyAnimator> configure;
    }

    static void BuildEnemyPrefabs()
    {
        var joint = LitMat("EnemyJoint", new Color(0.12f, 0.12f, 0.13f), smoothness: 0.6f, metallic: 0.7f);
        var glowOrange = LitMat("GruntGlow", Color.black, emission: new Color(4f, 2.6f, 0.3f));
        var glowCyan = LitMat("GunnerGlow", Color.black, emission: new Color(0.4f, 2.5f, 4f));
        var glowGreen = LitMat("ExploderGlow", Color.black, emission: new Color(0.8f, 5f, 0.6f));
        var glowRed = LitMat("BruteGlow", Color.black, emission: new Color(6f, 0.5f, 0.3f));
        var glowYellow = LitMat("RunnerGlow", Color.black, emission: new Color(5f, 4f, 0.5f));

        BuildRobot(new RobotSpec
        {
            name = "Grunt", bulk = 1.12f, hunched = true, arms = ArmStyle.Claws,
            body = LitMat("Grunt", new Color(0.72f, 0.16f, 0.12f), smoothness: 0.45f, metallic: 0.2f), joint = joint, glow = glowOrange,
            glowColor = new Color(1f, 0.7f, 0.15f),
            configure = (e, a) =>
            {
                e.displayName = "Grunt";
                e.maxHealth = 60f; e.moveSpeed = 4.8f; e.scoreValue = 100;
                e.attackType = Enemy.AttackType.Melee; e.attackRange = 2f; e.attackDamage = 12f; e.attackCooldown = 1f; e.attackWindup = 0.4f;
                a.legSwing = 38f; a.strideFrequency = 1.7f;
            },
        });

        BuildRobot(new RobotSpec
        {
            name = "Gunner", bulk = 0.9f, arms = ArmStyle.Gun, antenna = true,
            body = LitMat("Gunner", new Color(0.42f, 0.24f, 0.72f), smoothness: 0.45f, metallic: 0.2f), joint = joint, glow = glowCyan,
            glowColor = new Color(0.3f, 0.8f, 1f),
            configure = (e, a) =>
            {
                e.displayName = "Gunner";
                e.maxHealth = 80f; e.moveSpeed = 3.5f; e.scoreValue = 150;
                e.attackType = Enemy.AttackType.Hitscan; e.attackRange = 28f; e.preferredDistance = 14f;
                e.attackDamage = 7f; e.attackCooldown = 1.4f; e.attackWindup = 0.5f; e.rangedSpread = 3f;
                a.rangedPose = true; a.legSwing = 30f; a.strideFrequency = 1.4f;
            },
        });

        BuildRobot(new RobotSpec
        {
            name = "Runner", scale = 0.78f, bulk = 0.85f, arms = ArmStyle.Claws, hunched = true, horns = true,
            agentRadius = 0.35f, agentHeight = 1.6f,
            body = LitMat("Runner", new Color(0.85f, 0.62f, 0.12f), smoothness: 0.5f, metallic: 0.3f), joint = joint, glow = glowYellow,
            glowColor = new Color(1f, 0.9f, 0.3f),
            configure = (e, a) =>
            {
                e.displayName = "Runner";
                e.maxHealth = 35f; e.moveSpeed = 7.5f; e.scoreValue = 120; e.dropChance = 0.15f; e.voicePitch = 1.6f;
                e.attackType = Enemy.AttackType.Melee; e.attackRange = 1.7f; e.attackDamage = 7f; e.attackCooldown = 0.6f; e.attackWindup = 0.2f;
                a.legSwing = 48f; a.strideFrequency = 2.4f; a.leanForward = 22f; a.armSwing = 40f;
            },
        });

        BuildRobot(new RobotSpec
        {
            name = "Exploder", scale = 0.9f, bulk = 1.05f, arms = ArmStyle.Fists, backTank = true,
            body = LitMat("Exploder", new Color(0.25f, 0.55f, 0.2f), smoothness: 0.45f, metallic: 0.2f), joint = joint, glow = glowGreen,
            glowColor = new Color(0.4f, 1f, 0.3f),
            configure = (e, a) =>
            {
                e.displayName = "Exploder";
                e.maxHealth = 30f; e.moveSpeed = 6f; e.scoreValue = 130; e.dropChance = 0.1f; e.voicePitch = 1.2f;
                e.attackType = Enemy.AttackType.Explode; e.attackRange = 2.6f; e.attackDamage = 55f; e.attackWindup = 0.8f; e.splashRadius = 4.5f;
                e.bloodColor = new Color(0.3f, 0.8f, 0.2f);
                a.legSwing = 42f; a.strideFrequency = 2f; a.leanForward = 15f;
            },
        });

        BuildRobot(new RobotSpec
        {
            name = "Brute", scale = 1.65f, bulk = 1.35f, arms = ArmStyle.Hammers, hunched = true, horns = true,
            agentRadius = 0.9f, agentHeight = 3.3f, headMultiplier = 2.5f,
            body = LitMat("Brute", new Color(0.32f, 0.32f, 0.36f), smoothness: 0.6f, metallic: 0.6f), joint = joint, glow = glowRed,
            glowColor = new Color(1f, 0.25f, 0.15f),
            configure = (e, a) =>
            {
                e.displayName = "Brute";
                e.maxHealth = 500f; e.moveSpeed = 2.6f; e.scoreValue = 600; e.dropChance = 1f; e.voicePitch = 0.6f;
                e.bodyDamageMultiplier = 0.6f;
                e.attackType = Enemy.AttackType.Slam; e.attackRange = 3.2f; e.attackDamage = 35f; e.attackCooldown = 2.2f;
                e.attackWindup = 0.9f; e.splashRadius = 5f;
                a.legSwing = 25f; a.strideFrequency = 1f; a.bobHeight = 0.1f;
            },
        });

        BuildDrone(LitMat("Drone", new Color(0.78f, 0.8f, 0.84f), smoothness: 0.7f, metallic: 0.5f), joint, glowCyan);
    }

    static void BuildRobot(RobotSpec s)
    {
        var root = new GameObject(s.name);
        var t = root.transform;
        float bulk = s.bulk;
        bool gunArm = s.arms == ArmStyle.Gun;

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = s.agentRadius;
        agent.height = s.agentHeight;
        agent.baseOffset = 0f;
        agent.acceleration = 30f;
        agent.angularSpeed = 540f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        var model = Pivot(t, "Model", Vector3.zero, Vector3.zero);
        var bodyRoot = Pivot(model, "Body", Vector3.zero, Vector3.zero);
        bodyRoot.localScale = Vector3.one * s.scale * 1.04f;
        var hips = Pivot(bodyRoot, "Hips", new Vector3(0, 0.95f, 0), Vector3.zero);

        var breakables = new List<Transform>();
        GameObject AddMesh(Transform parent, string n, Vector3 pos, Vector3 size, Material m, bool collider = true, PrimitiveType type = PrimitiveType.Cube, bool breakable = true)
        {
            var go = Part(parent, n, type, pos, size, m, castShadows: true, keepCollider: collider);
            if (breakable) breakables.Add(go.transform);
            return go;
        }

        AddMesh(hips, "Pelvis", Vector3.zero, new Vector3(0.42f * bulk, 0.2f, 0.28f), s.joint);

        Transform Leg(float side)
        {
            var leg = Pivot(hips, side < 0 ? "LegL" : "LegR", new Vector3(side * 0.15f * bulk, -0.05f, 0), Vector3.zero);
            AddMesh(leg, "Thigh", new Vector3(0, -0.24f, 0), new Vector3(0.19f * bulk, 0.46f, 0.21f), s.body);
            AddMesh(leg, "Knee", new Vector3(0, -0.48f, 0.02f), new Vector3(0.13f, 0.12f, 0.13f), s.joint, false, PrimitiveType.Sphere, false);
            var shin = AddMesh(leg, "Shin", new Vector3(0, -0.68f, 0.0f), new Vector3(0.16f * bulk, 0.38f, 0.18f), s.joint);
            Decor(shin.transform, "Foot", new Vector3(0, -0.22f, 0.06f), new Vector3(0.2f * bulk, 0.08f, 0.32f), s.body);
            return leg;
        }
        var legL = Leg(-1f);
        var legR = Leg(1f);

        var torso = Pivot(hips, "Torso", new Vector3(0, 0.1f, 0), Vector3.zero);
        AddMesh(torso, "Abdomen", new Vector3(0, 0.12f, 0), new Vector3(0.36f * bulk, 0.2f, 0.26f), s.joint, true, PrimitiveType.Cube, false);
        var chest = AddMesh(torso, "Chest", new Vector3(0, 0.42f, s.hunched ? -0.03f : 0f), new Vector3(0.68f * bulk, 0.48f, 0.42f * bulk), s.body);
        Decor(chest.transform, "Core", new Vector3(0, 0.02f, 0.215f * bulk), new Vector3(0.16f, 0.16f, 0.04f), s.glow);
        Decor(chest.transform, "Vent", new Vector3(0, -0.17f, 0.215f * bulk), new Vector3(0.4f * bulk, 0.04f, 0.03f), s.joint);
        Decor(chest.transform, "PadL", new Vector3(-0.38f * bulk, 0.2f, 0), new Vector3(0.22f, 0.12f, 0.36f), s.joint);
        Decor(chest.transform, "PadR", new Vector3(0.38f * bulk, 0.2f, 0), new Vector3(0.22f, 0.12f, 0.36f), s.joint);
        if (s.hunched) Decor(chest.transform, "Hump", new Vector3(0, 0.2f, -0.15f), new Vector3(0.45f * bulk, 0.18f, 0.2f), s.body);
        if (s.backTank)
        {
            // Volatile glowing canister: the thing that explodes.
            var tank = AddMesh(torso, "Tank", new Vector3(0, 0.45f, -0.32f * bulk), new Vector3(0.3f, 0.25f, 0.3f), s.glow, true, PrimitiveType.Cylinder);
            Decor(tank.transform, "TankCapTop", new Vector3(0, 0.26f, 0), new Vector3(0.32f, 0.04f, 0.32f), s.joint, PrimitiveType.Cylinder);
            Decor(tank.transform, "TankCapBottom", new Vector3(0, -0.26f, 0), new Vector3(0.32f, 0.04f, 0.32f), s.joint, PrimitiveType.Cylinder);
        }

        var head = Pivot(torso, "Head", new Vector3(0, 0.7f, s.hunched ? 0.06f : 0.02f), Vector3.zero);
        AddMesh(head, "Neck", new Vector3(0, 0.02f, 0), new Vector3(0.12f, 0.1f, 0.12f), s.joint, false, PrimitiveType.Cylinder, false);
        var headMesh = AddMesh(head, "HeadMesh", new Vector3(0, 0.17f, 0), gunArm ? new Vector3(0.28f, 0.3f, 0.3f) : new Vector3(0.34f, 0.26f, 0.34f), s.body);
        headMesh.AddComponent<Hitbox>().damageMultiplier = s.headMultiplier;
        Decor(headMesh.transform, "Visor", new Vector3(0, 0.02f, gunArm ? 0.15f : 0.17f), new Vector3(gunArm ? 0.22f : 0.28f, 0.06f, 0.03f), s.glow);
        if (s.antenna) Decor(headMesh.transform, "Antenna", new Vector3(0.1f, 0.2f, -0.05f), new Vector3(0.015f, 0.18f, 0.015f), s.joint);
        if (!gunArm) Decor(headMesh.transform, "Jaw", new Vector3(0, -0.12f, 0.05f), new Vector3(0.26f, 0.06f, 0.24f), s.joint);
        if (s.horns)
        {
            Decor(headMesh.transform, "HornL", new Vector3(-0.16f, 0.14f, 0f), new Vector3(0.05f, 0.16f, 0.05f), s.glow);
            Decor(headMesh.transform, "HornR", new Vector3(0.16f, 0.14f, 0f), new Vector3(0.05f, 0.16f, 0.05f), s.glow);
        }

        Transform Arm(float side, bool isGunArm)
        {
            var arm = Pivot(torso, side < 0 ? "ArmL" : "ArmR", new Vector3(side * 0.44f * bulk, 0.58f, 0), isGunArm ? new Vector3(-85, 0, 0) : Vector3.zero);
            AddMesh(arm, "UpperArm", new Vector3(0, -0.19f, 0), new Vector3(0.15f * bulk, 0.38f, 0.15f * bulk), s.body);
            AddMesh(arm, "Elbow", new Vector3(0, -0.4f, 0), new Vector3(0.12f, 0.12f, 0.12f), s.joint, false, PrimitiveType.Sphere, false);
            if (isGunArm)
            {
                var gunMesh = AddMesh(arm, "ArmGun", new Vector3(0, -0.62f, 0), new Vector3(0.13f, 0.48f, 0.15f), s.joint);
                Decor(gunMesh.transform, "GunGlow", new Vector3(0, -0.1f, 0.078f), new Vector3(0.05f, 0.2f, 0.01f), s.glow);
                Decor(gunMesh.transform, "Barrel", new Vector3(0, -0.29f, 0), new Vector3(0.06f, 0.1f, 0.06f), s.joint);
                return arm;
            }
            var fore = AddMesh(arm, "Forearm", new Vector3(0, -0.6f, 0.03f), new Vector3(0.17f * bulk, 0.36f, 0.17f * bulk), s.joint);
            switch (s.arms)
            {
                case ArmStyle.Claws:
                    Decor(fore.transform, "Fist", new Vector3(0, -0.24f, 0.02f), new Vector3(0.2f * bulk, 0.16f, 0.2f * bulk), s.body);
                    for (int c = -1; c <= 1; c++)
                        Decor(fore.transform, "Claw", new Vector3(c * 0.06f, -0.36f, 0.06f), new Vector3(0.025f, 0.12f, 0.025f), s.glow);
                    break;
                case ArmStyle.Hammers:
                    Decor(fore.transform, "Hammer", new Vector3(0, -0.3f, 0.02f), new Vector3(0.34f, 0.26f, 0.34f), s.body);
                    Decor(fore.transform, "HammerGlow", new Vector3(0, -0.44f, 0.02f), new Vector3(0.36f, 0.04f, 0.36f), s.glow);
                    break;
                default:
                    Decor(fore.transform, "Fist", new Vector3(0, -0.24f, 0.02f), new Vector3(0.2f * bulk, 0.16f, 0.2f * bulk), s.body);
                    break;
            }
            return arm;
        }
        var armL = Arm(-1f, false);
        var armR = Arm(1f, gunArm);

        var anim = root.AddComponent<EnemyAnimator>();
        anim.model = model;
        anim.hips = hips;
        anim.torso = torso;
        anim.head = head;
        anim.leftArm = armL;
        anim.rightArm = armR;
        anim.leftLeg = legL;
        anim.rightLeg = legR;

        var e = root.AddComponent<Enemy>();
        e.eye = head;
        e.weaponMuzzle = gunArm ? Pivot(armR, "Muzzle", new Vector3(0, -0.98f, 0), new Vector3(90, 0, 0)) : null;
        e.glowColor = s.glowColor;
        e.bloodColor = new Color(0.1f, 0.09f, 0.08f);
        e.renderers = CollectTintable(root, s.body, s.joint);
        e.breakableParts = breakables.ToArray();
        s.configure?.Invoke(e, anim);

        SaveEnemyPrefab(root, s.name);
    }

    static void BuildDrone(Material bodyMat, Material jointMat, Material glowMat)
    {
        var root = new GameObject("Drone");
        var t = root.transform;
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.5f;
        agent.height = 1f;
        agent.baseOffset = 0f;
        agent.acceleration = 14f;
        agent.angularSpeed = 300f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

        var model = Pivot(t, "Model", new Vector3(0, 2.8f, 0), Vector3.zero);
        var breakables = new List<Transform>();
        GameObject AddMesh(Transform parent, string n, PrimitiveType type, Vector3 pos, Vector3 size, Material m, Vector3 euler = default, bool collider = true)
        {
            var go = Part(parent, n, type, pos, size, m, euler, castShadows: true, keepCollider: collider);
            breakables.Add(go.transform);
            return go;
        }

        var body = AddMesh(model, "Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.7f, 0.5f, 0.75f), bodyMat);
        Decor(body.transform, "Belt", Vector3.zero, new Vector3(0.74f, 0.08f, 0.78f), jointMat, PrimitiveType.Cylinder);
        var eye = AddMesh(model, "Eye", PrimitiveType.Sphere, new Vector3(0, 0.02f, 0.3f), Vector3.one * 0.22f, glowMat);
        eye.AddComponent<Hitbox>().damageMultiplier = 2f;
        Part(model, "Antenna", PrimitiveType.Cube, new Vector3(0.1f, 0.35f, -0.1f), new Vector3(0.015f, 0.25f, 0.015f), jointMat, castShadows: true);

        var rotors = new List<Transform>();
        foreach (var dir in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
        {
            Vector3 end = new Vector3(dir.x * 0.55f, 0.08f, dir.y * 0.55f);
            var arm = AddMesh(model, "Arm", PrimitiveType.Cube, end * 0.5f, new Vector3(0.06f, 0.05f, 0.75f), jointMat, new Vector3(0, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0), false);
            Part(model, "Motor", PrimitiveType.Cylinder, end, new Vector3(0.12f, 0.06f, 0.12f), jointMat, castShadows: true);
            var rotor = Pivot(model, "Rotor", end + Vector3.up * 0.08f, Vector3.zero);
            Part(rotor, "BladeA", PrimitiveType.Cube, Vector3.zero, new Vector3(0.42f, 0.01f, 0.05f), jointMat);
            Part(rotor, "BladeB", PrimitiveType.Cube, Vector3.zero, new Vector3(0.05f, 0.01f, 0.42f), jointMat);
            Part(rotor, "Glow", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.44f, 0.002f, 0.44f), glowMat);
            rotors.Add(rotor);
        }

        var gun = AddMesh(model, "Cannon", PrimitiveType.Cube, new Vector3(0, -0.28f, 0.15f), new Vector3(0.12f, 0.12f, 0.4f), jointMat);
        Decor(gun.transform, "CannonGlow", new Vector3(0, 0, 0.2f), new Vector3(0.08f, 0.08f, 0.02f), glowMat);
        var muzzle = Pivot(model, "Muzzle", new Vector3(0, -0.28f, 0.4f), Vector3.zero);

        var anim = root.AddComponent<EnemyAnimator>();
        anim.model = model;
        anim.hoverMode = true;
        anim.rotors = rotors.ToArray();

        var e = root.AddComponent<Enemy>();
        e.displayName = "Drone";
        e.eye = model;
        e.weaponMuzzle = muzzle;
        e.glowColor = new Color(0.3f, 0.8f, 1f);
        e.bloodColor = new Color(0.2f, 0.2f, 0.22f);
        e.maxHealth = 45f;
        e.moveSpeed = 5f;
        e.scoreValue = 140;
        e.dropChance = 0.2f;
        e.voicePitch = 1.4f;
        e.humLoop = true;
        e.strafe = true;
        e.attackType = Enemy.AttackType.Projectile;
        e.attackRange = 32f;
        e.preferredDistance = 15f;
        e.attackDamage = 10f;
        e.attackCooldown = 1.6f;
        e.attackWindup = 0.5f;
        e.projectileSpeed = 20f;
        e.rangedSpread = 2f;
        e.renderers = CollectTintable(root, bodyMat, jointMat);
        e.breakableParts = breakables.ToArray();

        SaveEnemyPrefab(root, "Drone");
    }

    static Renderer[] CollectTintable(GameObject root, Material body, Material joint)
    {
        var list = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            if (r.sharedMaterial == body || r.sharedMaterial == joint) list.Add(r);
        return list.ToArray();
    }

    static void SaveEnemyPrefab(GameObject root, string name)
    {
        SetLayerRecursive(root, LayerMask.NameToLayer("Enemy"));
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(root);
    }
}
