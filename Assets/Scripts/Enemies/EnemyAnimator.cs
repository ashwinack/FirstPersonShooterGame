using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Procedural animation for primitive-built robots: walk cycle, idle breathing,
/// hit flinch, attack windup/strike and spawn rise. No animation clips needed.
/// </summary>
public class EnemyAnimator : MonoBehaviour
{
    [Header("Rig")]
    public Transform model;      // root of all visuals (spawn rise / flinch lean)
    public Transform hips;       // bobs with the walk
    public Transform torso;      // leans and twists
    public Transform head;
    public Transform leftArm, rightArm;   // pivots at shoulders
    public Transform leftLeg, rightLeg;   // pivots at hips

    [Header("Walk")]
    public float strideFrequency = 1.6f;  // cycles per second per m/s (scaled by speed)
    public float legSwing = 35f;
    public float armSwing = 25f;
    public float bobHeight = 0.06f;
    public float leanForward = 10f;

    [Header("Style")]
    [Tooltip("Gun arm stays raised and aims instead of swinging.")]
    public bool rangedPose;

    [Header("Hover (drones)")]
    [Tooltip("Floats and banks instead of walking. Only 'model' is required.")]
    public bool hoverMode;
    public Transform[] rotors;

    NavMeshAgent agent;
    Vector3 hipsRest, modelRest, lastPos, smoothedVel;
    Quaternion torsoRest, headRest, lArmRest, rArmRest, lLegRest, rLegRest;
    float phase, speedBlend, spawnT, strikeT = -1f;
    Vector3 flinch, flinchVel;
    int lastStep;
    float windup;

    public System.Action Footstep;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        modelRest = model.localPosition;
        lastPos = transform.position;
        if (!hoverMode)
        {
            hipsRest = hips.localPosition;
            torsoRest = torso.localRotation;
            headRest = head.localRotation;
            lArmRest = leftArm.localRotation;
            rArmRest = rightArm.localRotation;
            lLegRest = leftLeg.localRotation;
            rLegRest = rightLeg.localRotation;
        }
        model.localScale = Vector3.one * 0.01f;
    }

    void HoverUpdate(float dt)
    {
        // Velocity from movement (works with or without an agent).
        Vector3 vel = (transform.position - lastPos) / dt;
        lastPos = transform.position;
        smoothedVel = Vector3.Lerp(smoothedVel, vel, dt * 5f);
        Vector3 local = transform.InverseTransformDirection(smoothedVel);

        flinchVel += (-flinch * 120f - flinchVel * 8f) * dt;
        flinch += flinchVel * dt;

        float windT = windup > 0f ? windup : 0f;
        float recoil = 0f;
        if (strikeT >= 0f)
        {
            strikeT += dt / 0.25f;
            recoil = Mathf.Sin(Mathf.Clamp01(strikeT) * Mathf.PI);
            if (strikeT >= 1f) strikeT = -1f;
        }

        float bob = Mathf.Sin(Time.time * 2.3f + GetInstanceHash()) * 0.15f;
        model.localPosition = modelRest + Vector3.up * bob - Vector3.forward * recoil * 0.3f;
        model.localRotation = Quaternion.Euler(
            local.z * 4f - windT * 10f - recoil * 15f + flinch.x,
            flinch.y + Mathf.Sin(Time.time * 0.9f) * 6f,
            -local.x * 5f + flinch.z);

        if (rotors != null)
            foreach (var r in rotors)
                if (r != null) r.Rotate(0f, 1400f * dt, 0f, Space.Self);
    }

    /// <summary>0..1 while winding up an attack, negative otherwise.</summary>
    public void SetWindup(float t) => windup = t;

    public void PlayStrike() => strikeT = 0f;

    /// <summary>Replay the rise-from-the-floor intro (respawning training bots).</summary>
    public void PlaySpawn()
    {
        spawnT = 0f;
        model.localScale = Vector3.one * 0.01f;
        lastPos = transform.position;
    }

    /// <summary>Kick the body away from a hit. dir = direction the bullet travelled.</summary>
    public void Flinch(Vector3 worldDir, float strength)
    {
        Vector3 local = transform.InverseTransformDirection(worldDir);
        // Lean away: pitch from forward/back component, roll from sideways.
        flinchVel += new Vector3(local.z, 0f, -local.x) * strength * 400f;
        flinchVel.y += Random.Range(-1f, 1f) * strength * 200f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Spawn: rise out of the floor with an overshoot.
        if (spawnT < 1f)
        {
            spawnT = Mathf.Min(1f, spawnT + dt / 0.5f);
            float s = 1f + Mathf.Sin(spawnT * Mathf.PI) * 0.15f * (1f - spawnT);
            model.localScale = new Vector3(s, Mathf.SmoothStep(0.01f, 1f, spawnT), s);
        }

        if (hoverMode)
        {
            HoverUpdate(dt);
            return;
        }

        // No agent (training bots): derive speed from actual movement.
        float measured = (transform.position - lastPos).magnitude / dt;
        lastPos = transform.position;
        float speed = agent != null && agent.enabled ? agent.velocity.magnitude : measured;
        float speed01 = Mathf.Clamp01(speed / 4f);
        speedBlend = Mathf.Lerp(speedBlend, speed01, dt * 8f);
        phase += dt * Mathf.PI * 2f * strideFrequency * Mathf.Max(speed / 2.5f, 0.0f);

        int step = Mathf.FloorToInt(phase / Mathf.PI);
        if (step != lastStep)
        {
            lastStep = step;
            if (speedBlend > 0.3f) Footstep?.Invoke();
        }

        float sin = Mathf.Sin(phase), cos = Mathf.Cos(phase);
        float idle = Mathf.Sin(Time.time * 2.2f + GetInstanceHash()) * (1f - speedBlend);

        // Hips bob twice per cycle.
        hips.localPosition = hipsRest + Vector3.up * (Mathf.Abs(sin) * bobHeight * speedBlend - bobHeight * 0.5f * speedBlend + idle * 0.01f);

        // Legs.
        leftLeg.localRotation = lLegRest * Quaternion.Euler(sin * legSwing * speedBlend, 0f, 0f);
        rightLeg.localRotation = rLegRest * Quaternion.Euler(-sin * legSwing * speedBlend, 0f, 0f);

        // Flinch spring.
        flinchVel += (-flinch * 180f - flinchVel * 12f) * dt;
        flinch += flinchVel * dt;

        // Torso lean + twist with stride + attack windup.
        float windT = windup > 0f ? windup : 0f;
        float attackLean = rangedPose ? -windT * 5f : -windT * 15f;
        float strikeLean = 0f;
        if (strikeT >= 0f)
        {
            strikeT += dt / 0.3f;
            strikeLean = Mathf.Sin(Mathf.Clamp01(strikeT) * Mathf.PI) * (rangedPose ? -8f : 30f);
            if (strikeT >= 1f) strikeT = -1f;
        }
        torso.localRotation = torsoRest * Quaternion.Euler(
            leanForward * speedBlend + attackLean + strikeLean + flinch.x + idle * 2f,
            cos * 8f * speedBlend + flinch.y,
            flinch.z);
        head.localRotation = headRest * Quaternion.Euler(-leanForward * 0.6f * speedBlend - flinch.x * 0.5f, -cos * 4f * speedBlend, 0f);

        // Arms.
        if (rangedPose)
        {
            // Left arm swings; gun arm stays raised and kicks on fire.
            leftArm.localRotation = lArmRest * Quaternion.Euler(-sin * armSwing * speedBlend, 0f, 0f);
            float kick = strikeT >= 0f ? Mathf.Sin(Mathf.Clamp01(strikeT) * Mathf.PI) * -25f : 0f;
            rightArm.localRotation = rArmRest * Quaternion.Euler(kick - windT * 5f, 0f, 0f);
        }
        else
        {
            // Melee: both arms rise overhead during windup, then slam forward.
            float raise = -150f * windT;
            if (strikeT >= 0f) raise = Mathf.Lerp(-150f, 20f, Mathf.Clamp01(strikeT * 2f));
            float swingL = -sin * armSwing * speedBlend * (1f - windT);
            leftArm.localRotation = lArmRest * Quaternion.Euler(swingL + raise, 0f, -windT * 15f);
            rightArm.localRotation = rArmRest * Quaternion.Euler(-swingL + raise, 0f, windT * 15f);
        }
    }

    float GetInstanceHash() => (transform.position.x * 13.7f + transform.position.z * 7.3f) % 6.28f;
}
