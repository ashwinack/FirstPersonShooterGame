using System;
using UnityEngine;

public enum HardwareAimMode
{
    /// <summary>Each packet is a small movement, like a mouse delta. Deltas are summed, so the result is
    /// already frame-rate independent (deltaTime is only used by smoothing).</summary>
    PerPacketDelta,
    /// <summary>Each packet is an angular speed (units per second). The frame's average is multiplied by deltaTime.</summary>
    RatePerSecond,
    /// <summary>Each packet is the gun's absolute position (e.g. x and y 0-100). The range maps onto the
    /// whole view: the middle of the range looks straight ahead, the ends are the yaw/pitch limits.</summary>
    Absolute,
}

[Serializable]
public class HardwareAimSettings
{
    public HardwareAimMode mode = HardwareAimMode.Absolute;
    [Tooltip("Absolute mode: the values the gun sends at its two ends (same for X and Y).")]
    public float absoluteMin = 0f;
    public float absoluteMax = 100f;
    [Tooltip("Degrees of rotation per sensor unit (PerPacketDelta) or per unit-second (RatePerSecond).")]
    public float sensitivity = 0.02f;
    [Tooltip("Per-axis values at or below this are treated as zero (sensor noise). Applied per packet.")]
    public float deadZone = 1f;
    [Tooltip("Seconds of exponential smoothing on the output (0 = off). Small values (0.02-0.05) remove jitter.")]
    public float smoothing = 0.03f;
    public bool invertX;
    public bool invertY;
}

/// <summary>
/// Turns raw gun X/Y values into yaw/pitch degrees for one frame.
/// Relative modes: packets are accumulated as they arrive (Add) and converted once per frame (Consume).
/// Absolute mode: the latest position (SetAbsolute) is smoothed and read as -1..1 (UpdateAbsolute).
/// +X turns right, +Y looks up (same convention as a mouse); use the invert flags to flip.
/// </summary>
public sealed class HardwareAimController
{
    public HardwareAimSettings Settings;

    float sumX, sumY;
    int count;
    Vector2 smoothedRate;

    public HardwareAimController(HardwareAimSettings settings) => Settings = settings;

    public int PendingPackets => count;

    public void Add(float x, float y)
    {
        sumX += ApplyDeadZone(x, Settings.deadZone);
        sumY += ApplyDeadZone(y, Settings.deadZone);
        count++;
    }

    /// <summary>Soft dead zone: no jump at the threshold.</summary>
    static float ApplyDeadZone(float v, float dz)
    {
        float a = Mathf.Abs(v);
        return a <= dz ? 0f : Mathf.Sign(v) * (a - dz);
    }

    /// <summary>Degrees to rotate this frame (x = yaw right, y = pitch up). Clears the accumulated input.</summary>
    public Vector2 Consume(float deltaTime)
    {
        float dt = Mathf.Max(deltaTime, 1e-4f);
        Vector2 degrees = Settings.mode == HardwareAimMode.PerPacketDelta
            ? new Vector2(sumX, sumY) * Settings.sensitivity
            : count > 0 ? new Vector2(sumX, sumY) / count * Settings.sensitivity * dt : Vector2.zero;
        sumX = sumY = 0f;
        count = 0;

        if (Settings.invertX) degrees.x = -degrees.x;
        if (Settings.invertY) degrees.y = -degrees.y;

        // Smooth the rotation speed rather than the angle, so total rotation is preserved.
        Vector2 rate = degrees / dt;
        if (Settings.smoothing > 0f)
        {
            float a = 1f - Mathf.Exp(-dt / Settings.smoothing);
            smoothedRate = Vector2.Lerp(smoothedRate, rate, a);
        }
        else smoothedRate = rate;
        return smoothedRate * dt;
    }

    /// <summary>Drop accumulated relative input (no one is looking: paused, menu open, mouse mode).
    /// The absolute position is kept: it's where the gun points, not movement.</summary>
    public void Reset()
    {
        sumX = sumY = 0f;
        count = 0;
        smoothedRate = Vector2.zero;
    }

    // ---------- Absolute mode ----------

    Vector2 absTarget, absSmoothed;

    /// <summary>At least one absolute position has arrived.</summary>
    public bool HasAbsolute { get; private set; }

    /// <summary>Latest absolute packet. Mapped to -1..1 per axis (+X right, +Y up), clamped to the range.</summary>
    public void SetAbsolute(float x, float y)
    {
        float min = Settings.absoluteMin, range = Mathf.Max(1e-3f, Settings.absoluteMax - Settings.absoluteMin);
        float nx = Mathf.Clamp((x - min) / range * 2f - 1f, -1f, 1f);
        float ny = Mathf.Clamp((y - min) / range * 2f - 1f, -1f, 1f);
        if (Settings.invertX) nx = -nx;
        if (Settings.invertY) ny = -ny;
        absTarget = new Vector2(nx, ny);
        if (!HasAbsolute) absSmoothed = absTarget;
        HasAbsolute = true;
    }

    /// <summary>Smoothed absolute aim this frame, -1..1 per axis.</summary>
    public Vector2 UpdateAbsolute(float deltaTime)
    {
        if (Settings.smoothing > 0f)
            absSmoothed = Vector2.Lerp(absSmoothed, absTarget, 1f - Mathf.Exp(-Mathf.Max(deltaTime, 1e-4f) / Settings.smoothing));
        else absSmoothed = absTarget;
        return absSmoothed;
    }
}
