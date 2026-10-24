using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Synthesizes all game sounds in code so the project needs no audio assets.
/// Clips are generated lazily on first use and cached. Swap for real clips any time.
/// </summary>
public static class ProceduralAudio
{
    const int SampleRate = 44100;

    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static readonly Dictionary<string, AudioClip[]> variantCache = new Dictionary<string, AudioClip[]>();
    static AudioSource[] pool;
    static int poolIndex;

    // ---------- Weapons ----------
    public static AudioClip Rifle => Get("Rifle", () => Gunshot(seed: 11, crack: 0.9f, bodyLp: 0.45f, bodyDecay: 16f, thumpHz: 95f, thumpGain: 0.9f, mech: 0.25f, tail: 0.7f, gain: 0.85f));
    public static AudioClip Shotgun => Get("Shotgun", () => Gunshot(seed: 21, crack: 1f, bodyLp: 0.28f, bodyDecay: 8f, thumpHz: 58f, thumpGain: 1.2f, mech: 0f, tail: 1.1f, gain: 1f));
    public static AudioClip EnemyShot => Get("EnemyShot", () => Gunshot(seed: 31, crack: 0.5f, bodyLp: 0.7f, bodyDecay: 20f, thumpHz: 180f, thumpGain: 0.5f, mech: 0f, tail: 0.6f, gain: 0.6f));

    public static AudioClip PistolShot => Get("Pistol", () => Gunshot(seed: 41, crack: 1f, bodyLp: 0.55f, bodyDecay: 22f, thumpHz: 130f, thumpGain: 0.6f, mech: 0.35f, tail: 0.55f, gain: 0.75f));
    public static AudioClip SmgShot => Get("Smg", () => Gunshot(seed: 51, crack: 0.25f, bodyLp: 0.3f, bodyDecay: 30f, thumpHz: 150f, thumpGain: 0.45f, mech: 0.45f, tail: 0.25f, gain: 0.6f));
    public static AudioClip SniperShot => Get("Sniper", () => Gunshot(seed: 61, crack: 1.4f, bodyLp: 0.4f, bodyDecay: 6f, thumpHz: 55f, thumpGain: 1.3f, mech: 0f, tail: 1.6f, gain: 1f));
    public static AudioClip RocketLaunch => Get("RocketLaunch", () => ToClip("RocketLaunch", Reverb(Seq(0.8f, (0f, Thud(0.25f, 1.2f, 300)), (0f, Whoosh(0.7f, 1.1f))), 0.5f)));
    public static AudioClip ExplosionBoom => Pick("Explosion", 2, v => ToClip("Explosion", SoftClip(Reverb(Boom(1.4f, 310 + v), 1.4f))));
    public static AudioClip PlasmaShot => Get("PlasmaShot", () => ToClip("PlasmaShot", Reverb(Sweep(0.25f, 1400f, 300f, 0.35f), 0.2f)));
    public static AudioClip PlasmaImpact => Get("PlasmaImpact", () => ToClip("PlasmaImpact", Seq(0.3f, (0f, Grit(0.2f, 0.5f, 320)), (0f, Sweep(0.15f, 600f, 150f, 0.25f)))));
    public static AudioClip Beep => Get("Beep", () => ToClip("Beep", Tone(0.09f, 1900f, 0.4f, 8f)));
    public static AudioClip Slam => Get("Slam", () => ToClip("Slam", SoftClip(Reverb(Seq(1f, (0f, Boom(0.6f, 330)), (0f, Metal(0.4f, 300f, 0.4f))), 0.8f))));
    public static AudioClip BossRoar => Get("BossRoar", () => ToClip("BossRoar", Reverb(Buzz(1.4f, 45f, 0.5f), 0.8f)));
    public static AudioClip RunnerScreech => Pick("RunnerScreech", 2, v => ToClip("RunnerScreech", Buzz(0.35f, 260f + v * 40f, 0.25f)));
    public static AudioClip DroneHum => Get("DroneHum", () => ToClip("DroneHum", HumLoop(1f, 0.25f)));
    public static AudioClip BarrelHit => Get("BarrelHit", () => ToClip("BarrelHit", Metal(0.3f, 520f, 0.35f)));
    public static AudioClip UIClick => Get("UIClick", () => ToClip("UIClick", Seq(0.06f, (0f, Clack(2600f, 0.5f)))));
    public static AudioClip Unlock => Get("Unlock", () => ToClip("Unlock", Reverb(Seq(0.6f, (0f, Tone(0.15f, 523f, 0.3f, 8f)), (0.1f, Tone(0.15f, 659f, 0.3f, 8f)), (0.2f, Tone(0.15f, 784f, 0.3f, 8f)), (0.3f, Tone(0.3f, 1046f, 0.35f, 5f))), 0.4f)));

    public static AudioClip MagOut => Get("MagOut", () => ToClip("MagOut", Seq(0.25f, (0f, Clack(2200f, 0.6f)), (0.06f, Slide(0.08f, 0.25f)))));
    public static AudioClip MagIn => Get("MagIn", () => ToClip("MagIn", Seq(0.25f, (0f, Slide(0.05f, 0.25f)), (0.06f, Clack(1600f, 0.9f)), (0.08f, Clack(3000f, 0.4f)))));
    public static AudioClip BoltRack => Get("BoltRack", () => ToClip("BoltRack", Seq(0.3f, (0f, Clack(1800f, 0.7f)), (0.02f, Slide(0.07f, 0.3f)), (0.12f, Clack(2600f, 1f)))));
    public static AudioClip PumpBack => Get("PumpBack", () => ToClip("PumpBack", Seq(0.18f, (0f, Slide(0.08f, 0.45f)), (0.08f, Clack(1300f, 0.9f)))));
    public static AudioClip PumpForward => Get("PumpForward", () => ToClip("PumpForward", Seq(0.18f, (0f, Slide(0.06f, 0.4f)), (0.06f, Clack(1700f, 1f)))));
    public static AudioClip ShellInsert => Get("ShellInsert", () => ToClip("ShellInsert", Seq(0.15f, (0f, Slide(0.04f, 0.2f)), (0.04f, Clack(2400f, 0.7f)))));
    public static AudioClip Empty => Get("Empty", () => ToClip("Empty", Seq(0.08f, (0f, Clack(3200f, 0.6f)))));
    public static AudioClip WeaponSwitch => Get("Switch", () => ToClip("Switch", Seq(0.2f, (0f, Slide(0.06f, 0.2f)), (0.07f, Clack(2000f, 0.5f)))));

    public static AudioClip ShellTink => Pick("ShellTink", 3, v => ToClip("ShellTink", Metal(0.25f, 2900f + v * 450f, 0.35f)));
    public static AudioClip ShotgunShellDrop => Pick("ShellDrop", 2, v => ToClip("ShellDrop", Seq(0.2f, (0f, Thud(0.06f, 0.25f, 12 + v)), (0.05f, Metal(0.08f, 1400f + v * 200f, 0.12f)))));

    // ---------- Player ----------
    public static AudioClip Footstep => Pick("Footstep", 4, v => ToClip("Footstep", Seq(0.14f, (0f, Thud(0.09f, 0.5f, 40 + v)), (0.005f, Grit(0.05f, 0.18f, 50 + v)))));
    public static AudioClip Land => Get("Land", () => ToClip("Land", Seq(0.3f, (0f, Thud(0.18f, 0.9f, 60)), (0.01f, Grit(0.12f, 0.3f, 61)))));
    public static AudioClip Jump => Get("Jump", () => ToClip("Jump", Seq(0.15f, (0f, Grit(0.08f, 0.2f, 70)))));
    public static AudioClip Hurt => Pick("Hurt", 3, v => ToClip("Hurt", Seq(0.35f, (0f, Thud(0.15f, 0.9f, 80 + v)), (0f, Tone(0.25f, 110f + v * 15f, 0.35f, 10f)))));

    // ---------- Feedback ----------
    public static AudioClip Hit => Get("Hit", () => ToClip("Hit", Tone(0.07f, 1500f, 0.4f, 55f)));
    public static AudioClip Headshot => Get("Headshot", () => ToClip("Headshot", Metal(0.4f, 2100f, 0.45f)));
    public static AudioClip Kill => Get("Kill", () => ToClip("Kill", Seq(0.3f, (0f, Thud(0.15f, 0.8f, 90)), (0f, Tone(0.2f, 900f, 0.25f, 20f)), (0.05f, Tone(0.2f, 1350f, 0.2f, 18f)))));
    public static AudioClip Pickup => Get("Pickup", () => ToClip("Pickup", Seq(0.3f, (0f, Tone(0.1f, 660f, 0.3f, 15f)), (0.07f, Tone(0.1f, 880f, 0.3f, 15f)), (0.14f, Tone(0.15f, 1320f, 0.3f, 12f)))));

    // ---------- Enemies ----------
    public static AudioClip EnemyDeath => Pick("EnemyDeath", 2, v => ToClip("EnemyDeath", Reverb(Seq(0.7f, (0f, Sweep(0.5f, 340f + v * 60f, 60f, 0.3f)), (0f, Grit(0.25f, 0.5f, 100 + v)), (0.02f, Metal(0.3f, 1800f + v * 300f, 0.25f))), 0.25f)));
    public static AudioClip EnemyHurt => Pick("EnemyHurt", 3, v => ToClip("EnemyHurt", Buzz(0.15f, 140f + v * 25f, 0.3f)));
    public static AudioClip EnemyGrowl => Pick("EnemyGrowl", 2, v => ToClip("EnemyGrowl", Reverb(Buzz(0.6f, 70f + v * 15f, 0.35f), 0.2f)));
    public static AudioClip EnemyStep => Pick("EnemyStep", 2, v => ToClip("EnemyStep", Seq(0.15f, (0f, Thud(0.1f, 0.7f, 120 + v)), (0f, Metal(0.06f, 900f + v * 100f, 0.1f)))));
    public static AudioClip Swipe => Get("Swipe", () => ToClip("Swipe", Whoosh(0.22f, 0.9f)));
    public static AudioClip MeleeHit => Get("MeleeHit", () => ToClip("MeleeHit", Seq(0.25f, (0f, Thud(0.15f, 1f, 130)), (0f, Grit(0.06f, 0.5f, 131)))));
    public static AudioClip ImpactWorld => Pick("ImpactWorld", 3, v => ToClip("ImpactWorld", Seq(0.12f, (0f, Grit(0.05f, 0.4f, 140 + v)), (0f, Metal(0.06f, 2500f + v * 700f, 0.08f)))));

    // ================= Synthesis building blocks =================

    static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

    /// Layered gunshot: transient crack + filtered noise body + pitch-dropping thump + mechanism click + room tail.
    static AudioClip Gunshot(int seed, float crack, float bodyLp, float bodyDecay, float thumpHz, float thumpGain, float mech, float tail, float gain)
    {
        var rng = new System.Random(seed);
        var d = Buffer(0.5f);
        float lp = 0f, lp2 = 0f, hpPrev = 0f, hpOut = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            float n = (float)(rng.NextDouble() * 2 - 1);
            // Crack: high-passed noise, very short.
            hpOut = 0.7f * (hpOut + n - hpPrev);
            hpPrev = n;
            float crackS = hpOut * Mathf.Exp(-t * 400f) * crack;
            // Body: double low-passed noise.
            lp += (n - lp) * bodyLp;
            lp2 += (lp - lp2) * bodyLp;
            float body = lp2 * 2.2f * Mathf.Exp(-t * bodyDecay);
            // Thump with pitch drop.
            float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t * (1f - Mathf.Min(t * 3f, 0.6f))) * Mathf.Exp(-t * 28f) * thumpGain;
            d[i] = (crackS + body + thump) * gain;
        }
        if (mech > 0f) Mix(d, Clack(4200f, mech), 0.01f);
        return ToClip("Gunshot" + seed, SoftClip(Reverb(d, tail)));
    }

    static float[] Tone(float len, float hz, float gain, float decay)
    {
        var d = Buffer(len);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            d[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * gain * Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 400f);
        }
        return d;
    }

    static float[] Sweep(float len, float fromHz, float toHz, float gain)
    {
        var d = Buffer(len);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            float f = Mathf.Lerp(fromHz, toHz, Mathf.Sqrt(t / len));
            phase += 2f * Mathf.PI * f / SampleRate;
            float saw = (phase / Mathf.PI) % 2f - 1f;
            d[i] = (Mathf.Sin(phase) * 0.6f + saw * 0.3f) * gain * (1f - t / len);
        }
        return d;
    }

    /// Inharmonic partials = struck metal (shells, headshot ding).
    static float[] Metal(float len, float baseHz, float gain)
    {
        var d = Buffer(len);
        float[] ratios = { 1f, 1.47f, 2.09f, 2.56f, 3.71f };
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate, s = 0f;
            for (int k = 0; k < ratios.Length; k++)
                s += Mathf.Sin(2f * Mathf.PI * baseHz * ratios[k] * t) * Mathf.Exp(-t * (14f + k * 9f)) / (k + 1);
            d[i] = s * gain * Mathf.Min(1f, t * 2000f);
        }
        return d;
    }

    /// Low thud (footsteps, body hits).
    static float[] Thud(float len, float gain, int seed)
    {
        var rng = new System.Random(seed);
        var d = Buffer(len);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.08f;
            float sine = Mathf.Sin(2f * Mathf.PI * (75f - t * 120f) * t);
            d[i] = (lp * 2.5f + sine * 0.7f) * gain * Mathf.Exp(-t * 35f);
        }
        return d;
    }

    /// Brighter scuffy noise (grit underfoot, debris).
    static float[] Grit(float len, float gain, int seed)
    {
        var rng = new System.Random(seed);
        var d = Buffer(len);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.5f;
            float crackle = rng.NextDouble() < 0.02 ? 1f : 0.3f;
            d[i] = lp * crackle * gain * Mathf.Exp(-t * 40f);
        }
        return d;
    }

    /// Short metallic click.
    static float[] Clack(float hz, float gain)
    {
        var d = Buffer(0.06f);
        var rng = new System.Random((int)hz);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            float n = (float)(rng.NextDouble() * 2 - 1);
            d[i] = (n * 0.6f * Mathf.Exp(-t * 600f) + Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 120f) * 0.5f) * gain;
        }
        return d;
    }

    /// Metal sliding on metal.
    static float[] Slide(float len, float gain)
    {
        var d = Buffer(len);
        var rng = new System.Random((int)(len * 1000));
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.6f;
            d[i] = lp * gain * Mathf.Sin(Mathf.PI * t / len);
        }
        return d;
    }

    /// Distorted, vibrato'd sawtooth (robot vocalizations).
    static float[] Buzz(float len, float hz, float gain)
    {
        var d = Buffer(len);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            float f = hz * (1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 9f * t)) * (1f - 0.3f * t / len);
            phase += f / SampleRate;
            float saw = (phase % 1f) * 2f - 1f;
            float env = Mathf.Min(1f, t * 30f) * (1f - t / len);
            d[i] = (float)Math.Tanh(saw * 3f) * gain * env;
        }
        return d;
    }

    /// Explosion: noise burst with a sub-bass drop and crackle.
    static float[] Boom(float len, int seed)
    {
        var rng = new System.Random(seed);
        var d = Buffer(len);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            float n = (float)(rng.NextDouble() * 2 - 1);
            lp += (n - lp) * 0.12f;
            lp2 += (lp - lp2) * 0.12f;
            float rumble = lp2 * 4f * Mathf.Exp(-t * 3.5f);
            float sub = Mathf.Sin(2f * Mathf.PI * (55f - t * 25f) * t) * Mathf.Exp(-t * 5f) * 1.2f;
            float crack = n * Mathf.Exp(-t * 60f) * 0.8f;
            float crackle = rng.NextDouble() < 0.004 * Mathf.Exp(-t * 3f) ? (float)(rng.NextDouble() * 2 - 1) : 0f;
            d[i] = rumble + sub + crack + crackle * 0.6f;
        }
        return d;
    }

    /// Seamless loop: all partials complete whole cycles over the clip length.
    static float[] HumLoop(float len, float gain)
    {
        var d = Buffer(len);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            float am = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 12f * t);
            float s = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 333f * t) * 0.15f;
            d[i] = s * am * gain;
        }
        return d;
    }

    static float[] Whoosh(float len, float gain)
    {
        var d = Buffer(len);
        var rng = new System.Random(5);
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SampleRate;
            lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * Mathf.Lerp(0.03f, 0.35f, t / len);
            d[i] = lp * gain * 1.5f * Mathf.Sin(Mathf.PI * t / len);
        }
        return d;
    }

    // ---------- Processing ----------

    /// Places layers at time offsets into one buffer.
    static float[] Seq(float len, params (float at, float[] data)[] layers)
    {
        var d = Buffer(len);
        foreach (var (at, data) in layers) Mix(d, data, at);
        return d;
    }

    static void Mix(float[] dst, float[] src, float at)
    {
        int offset = Mathf.RoundToInt(at * SampleRate);
        for (int i = 0; i < src.Length && i + offset < dst.Length; i++) dst[i + offset] += src[i];
    }

    /// Small Schroeder reverb: 4 parallel combs + 2 allpasses. Extends the buffer by the tail.
    static float[] Reverb(float[] dry, float amount)
    {
        if (amount <= 0f) return dry;
        int tail = Mathf.RoundToInt(amount * SampleRate);
        var outBuf = new float[dry.Length + tail];
        int[] combDelays = { 1557, 1617, 1491, 1422 };
        float feedback = Mathf.Lerp(0.7f, 0.84f, Mathf.Clamp01(amount));
        var wet = new float[outBuf.Length];
        foreach (int delay in combDelays)
        {
            var buf = new float[delay];
            int idx = 0;
            float damp = 0f;
            for (int i = 0; i < outBuf.Length; i++)
            {
                float input = i < dry.Length ? dry[i] : 0f;
                float y = buf[idx];
                damp = y * 0.6f + damp * 0.4f;
                buf[idx] = input + damp * feedback;
                idx = (idx + 1) % delay;
                wet[i] += y * 0.25f;
            }
        }
        foreach (int delay in new[] { 225, 556 })
        {
            var buf = new float[delay];
            int idx = 0;
            for (int i = 0; i < wet.Length; i++)
            {
                float b = buf[idx];
                float y = -wet[i] + b;
                buf[idx] = wet[i] + b * 0.5f;
                idx = (idx + 1) % delay;
                wet[i] = y;
            }
        }
        float wetGain = 0.35f;
        for (int i = 0; i < outBuf.Length; i++)
            outBuf[i] = (i < dry.Length ? dry[i] : 0f) + wet[i] * wetGain;
        return outBuf;
    }

    static float[] SoftClip(float[] d)
    {
        for (int i = 0; i < d.Length; i++) d[i] = (float)Math.Tanh(d[i] * 1.2f);
        return d;
    }

    static AudioClip ToClip(string name, float[] data)
    {
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Get(string key, Func<AudioClip> make)
    {
        if (!cache.TryGetValue(key, out var clip) || clip == null) cache[key] = clip = make();
        return clip;
    }

    static AudioClip Pick(string key, int variants, Func<int, AudioClip> make)
    {
        if (!variantCache.TryGetValue(key, out var clips) || clips[0] == null)
        {
            clips = new AudioClip[variants];
            for (int v = 0; v < variants; v++) clips[v] = make(v);
            variantCache[key] = clips;
        }
        return clips[Random.Range(0, clips.Length)];
    }

    /// Generate everything up front so the first shot doesn't hitch.
    public static void Warmup()
    {
        var _ = new[]
        {
            Rifle, Shotgun, EnemyShot, MagOut, MagIn, BoltRack, PumpBack, PumpForward, ShellInsert, Empty, WeaponSwitch,
            ShellTink, ShotgunShellDrop, Footstep, Land, Jump, Hurt, Hit, Headshot, Kill, Pickup,
            EnemyDeath, EnemyHurt, EnemyGrowl, EnemyStep, Swipe, MeleeHit, ImpactWorld,
            PistolShot, SmgShot, SniperShot, RocketLaunch, ExplosionBoom, PlasmaShot, PlasmaImpact, Beep, Slam,
            BossRoar, RunnerScreech, DroneHum, BarrelHit, UIClick, Unlock,
        };
    }

    // ================= Playback =================

    /// Non-positional sound (player's own gun, UI, etc).
    public static void Play2D(AudioClip clip, float volume = 1f, float pitchJitter = 0.05f)
    {
        if (clip == null) return;
        EnsurePool();
        var source = pool[poolIndex];
        poolIndex = (poolIndex + 1) % pool.Length;
        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clip, volume);
    }

    /// 3D sound at a world position.
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float minDistance = 4f, float pitch = 1f)
    {
        if (clip == null) return;
        var go = new GameObject("OneShotAudio");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.volume = volume;
        source.minDistance = minDistance;
        source.maxDistance = 100f;
        source.dopplerLevel = 0f;
        source.pitch = pitch * (1f + Random.Range(-0.07f, 0.07f));
        source.Play();
        UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.1f, source.pitch) + 0.1f);
    }

    /// Looping 3D sound attached to a transform (drone hum).
    public static AudioSource AttachLoop(Transform parent, AudioClip clip, float volume, float minDistance = 3f)
    {
        var source = parent.gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 1f;
        source.volume = volume;
        source.minDistance = minDistance;
        source.maxDistance = 40f;
        source.dopplerLevel = 0.3f;
        source.time = Random.Range(0f, clip.length * 0.9f);
        source.Play();
        return source;
    }

    static void EnsurePool()
    {
        if (pool != null && pool[0] != null) return;
        var go = new GameObject("AudioPool");
        UnityEngine.Object.DontDestroyOnLoad(go);
        pool = new AudioSource[16];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = go.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
            pool[i].spatialBlend = 0f;
        }
    }
}
