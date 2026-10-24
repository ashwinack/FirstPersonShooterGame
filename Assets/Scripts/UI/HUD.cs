using System.Collections.Generic;
using UnityEngine;

/// <summary>Immediate-mode HUD: crosshair, health, ammo, waves, menus. No UI assets required.</summary>
public class HUD : MonoBehaviour
{
    public static HUD Instance { get; private set; }

    struct Message
    {
        public string text;
        public Color color;
        public float time;
    }

    readonly List<Message> messages = new List<Message>();
    float hitmarkerTime = -10f;
    bool hitmarkerKill, hitmarkerHead;
    string banner;
    float bannerTime = -10f;
    float lastHealth = -1f, damageFlash, scale;
    string unlockText;
    float unlockTime = -10f;
    bool showSettings;
    Texture2D vignette, scopeTex;
    GUIStyle label, big, small, button;

    static GameManager GM => GameManager.Instance;

    void Awake()
    {
        Instance = this;
        vignette = MakeVignette(256);
        scopeTex = MakeScope(512);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (vignette != null) Destroy(vignette);
        if (scopeTex != null) Destroy(scopeTex);
    }

    /// Black square with a clear circle and a dark rim.
    static Texture2D MakeScope(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x / (size - 1f)) * 2f - 1f, dy = (y / (size - 1f)) * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.SmoothStep(0f, 1f, (d - 0.88f) / 0.06f);
            float shade = Mathf.SmoothStep(0f, 0.35f, (d - 0.7f) / 0.2f);
            px[y * size + x] = new Color(0f, 0f, 0f, Mathf.Max(a, shade));
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    public void ShowHitmarker(bool kill, bool head)
    {
        hitmarkerTime = Time.unscaledTime;
        hitmarkerKill = kill;
        hitmarkerHead = head;
    }

    public void ShowBanner(string text)
    {
        banner = text;
        bannerTime = Time.unscaledTime;
    }

    public void AddMessage(string text, Color color)
    {
        messages.Add(new Message { text = text, color = color, time = Time.unscaledTime });
        if (messages.Count > 5) messages.RemoveAt(0);
    }

    readonly List<(Vector3 source, float time)> damageSources = new List<(Vector3, float)>();
    bool subscribed;

    [Header("References (filled from GameManager if left empty)")]
    public PlayerController player;
    public PlayerHealth health;
    public WeaponManager weapons;

    /// <summary>Hide gameplay HUD (e.g. while a full-screen menu is open).</summary>
    public bool Hidden { get; set; }

    void Start()
    {
        if (GM != null)
        {
            if (player == null) player = GM.player;
            if (health == null) health = GM.playerHealth;
            if (weapons == null) weapons = GM.weapons;
        }
        if (health != null)
            health.Damaged += src =>
            {
                damageSources.Add((src, Time.unscaledTime));
                if (damageSources.Count > 6) damageSources.RemoveAt(0);
            };
    }

    void Update()
    {
        if (health == null) return;
        float hp = health.Current;
        if (lastHealth >= 0f && hp < lastHealth) damageFlash = Mathf.Min(1f, damageFlash + (lastHealth - hp) / 30f);
        lastHealth = hp;
        damageFlash = Mathf.MoveTowards(damageFlash, 0f, Time.deltaTime * 1.5f);
    }

    void OnGUI()
    {
        if (weapons == null) return;
        scale = Screen.height / 1080f;
        EnsureStyles();

        bool gameOver = GM != null && GM.CurrentState == GameManager.State.GameOver;
        bool scoped = weapons.Current.IsScoped && !gameOver && !Hidden;
        if (scoped) DrawScope();
        if (health != null) DrawVignette();
        if (!gameOver && !Hidden)
        {
            if (!scoped) DrawCrosshair();
            DrawHitmarker();
            if (health != null)
            {
                DrawDamageIndicators();
                DrawHealth();
            }
            DrawAmmo();
            DrawWeaponBar();
            DrawMessages();
            DrawBanner();
            if (GM != null)
            {
                DrawTopBar();
                DrawBossBar();
                DrawUnlock();
                DrawHints();
            }
        }

        if (GM == null) return;
        if (GM.CurrentState == GameManager.State.Paused) DrawPauseMenu();
        else if (gameOver) DrawGameOver();
    }

    // ---------- Elements ----------

    void DrawVignette()
    {
        var h = health;
        float frac = h.Current / h.maxHealth;
        float low = frac < 0.35f ? (1f - frac / 0.35f) * 0.55f * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
        float a = Mathf.Max(damageFlash * 0.8f, low);
        if (h.IsDead) a = 0.7f;
        if (a <= 0.01f) return;
        GUI.color = new Color(0.8f, 0f, 0f, a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette);
        GUI.color = Color.white;
    }

    /// <summary>Crosshair position in GUI coordinates: follows the gun in fixed-camera mode, else screen centre.</summary>
    Vector2 AimPoint => player != null && player.FixedCamera
        ? new Vector2(player.CrosshairScreen.x, Screen.height - player.CrosshairScreen.y)
        : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

    void DrawCrosshair()
    {
        var gun = weapons.Current;
        float aim = gun.AimAmount;
        Vector2 c = AimPoint;
        float thick = Mathf.Max(2f, 2f * scale);
        if (player != null && player.FixedCamera)
        {
            DrawGunCrosshair(c, gun);
            return;
        }

        if (aim > 0.9f)
        {
            Box(new Rect(c.x - thick, c.y - thick, thick * 2, thick * 2), new Color(1f, 1f, 1f, 0.9f));
            return;
        }

        float fov = weapons.cam.fieldOfView;
        float gap = Mathf.Tan(gun.CurrentSpread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Screen.height * 0.5f;
        gap = Mathf.Max(gap, 5f * scale);
        float len = 10f * scale;
        var col = new Color(1f, 1f, 1f, 1f - aim);

        Box(new Rect(c.x - thick / 2, c.y - gap - len, thick, len), col);
        Box(new Rect(c.x - thick / 2, c.y + gap, thick, len), col);
        Box(new Rect(c.x - gap - len, c.y - thick / 2, len, thick), col);
        Box(new Rect(c.x + gap, c.y - thick / 2, len, thick), col);
    }

    [Header("Gun crosshair (fixed camera)")]
    [Tooltip("Normal colour of the moving crosshair.")]
    public Color gunCrosshairColor = new Color(0.25f, 1f, 0.45f, 1f);
    [Tooltip("Colour while the crosshair is over an enemy.")]
    public Color gunCrosshairEnemyColor = new Color(1f, 0.2f, 0.15f, 1f);
    [Tooltip("Size multiplier for the moving crosshair.")]
    public float gunCrosshairSize = 1f;

    Texture2D ringTex;
    bool crosshairOnEnemy;
    float onEnemyCheckFrame = -1;

    /// Big, outlined light-gun style crosshair: ring + four ticks + centre dot, red over an enemy.
    void DrawGunCrosshair(Vector2 c, Gun gun)
    {
        if (ringTex == null) ringTex = MakeRingTexture(128);

        // Over an enemy? One raycast per frame, along the same ray the gun shoots.
        if (onEnemyCheckFrame != Time.frameCount)
        {
            onEnemyCheckFrame = Time.frameCount;
            int mask = ~LayerMask.GetMask("Player", "Ignore Raycast");
            crosshairOnEnemy = Physics.Raycast(player.AimRay, out RaycastHit hit, 300f, mask, QueryTriggerInteraction.Ignore)
                               && hit.collider.GetComponentInParent<Enemy>() != null;
        }

        float s = scale * Mathf.Max(0.3f, gunCrosshairSize);
        Color col = crosshairOnEnemy ? gunCrosshairEnemyColor : gunCrosshairColor;
        var outline = new Color(0f, 0f, 0f, 0.85f);

        // Ring (texture has its own dark border); pulses a little when over an enemy.
        float ringR = 30f * s * (crosshairOnEnemy ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 18f) : 1f);
        GUI.color = col;
        GUI.DrawTexture(new Rect(c.x - ringR, c.y - ringR, ringR * 2f, ringR * 2f), ringTex);
        GUI.color = Color.white;

        // Ticks: start just inside the ring (or wider with weapon spread) and reach well outside it.
        float fov = weapons.cam.fieldOfView;
        float spreadGap = Mathf.Tan(gun.CurrentSpread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Screen.height * 0.5f;
        float inner = Mathf.Max(ringR * 0.55f, spreadGap);
        float len = 26f * s, t = Mathf.Max(3f, 5f * s), o = Mathf.Max(1.5f, 2f * s);
        OutlinedBox(new Rect(c.x - t / 2, c.y - inner - len, t, len), col, outline, o);
        OutlinedBox(new Rect(c.x - t / 2, c.y + inner, t, len), col, outline, o);
        OutlinedBox(new Rect(c.x - inner - len, c.y - t / 2, len, t), col, outline, o);
        OutlinedBox(new Rect(c.x + inner, c.y - t / 2, len, t), col, outline, o);

        // Centre dot.
        float dot = Mathf.Max(4f, 7f * s);
        OutlinedBox(new Rect(c.x - dot / 2, c.y - dot / 2, dot, dot), col, outline, o);
    }

    static void OutlinedBox(Rect r, Color fill, Color outline, float o)
    {
        Box(new Rect(r.x - o, r.y - o, r.width + o * 2f, r.height + o * 2f), outline);
        Box(r, fill);
    }

    /// White anti-aliased ring with a dark border on both sides (tinted by GUI.color, the border stays dark).
    static Texture2D MakeRingTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float c = (size - 1) * 0.5f, rCore = size * 0.40f, halfCore = size * 0.035f, border = size * 0.03f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - rCore);
            float core = Mathf.Clamp01(halfCore + 0.5f - d);               // white band
            float edge = Mathf.Clamp01(halfCore + border + 0.5f - d) * 0.85f; // dark band around it
            float a = Mathf.Max(core, edge);
            float v = a > 0f ? core / a : 0f;
            px[y * size + x] = new Color(v, v, v, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    void DrawHitmarker()
    {
        float age = Time.unscaledTime - hitmarkerTime;
        if (age > 0.18f) return;
        Vector2 c = AimPoint;
        Color col = hitmarkerKill ? new Color(1f, 0.2f, 0.2f) : hitmarkerHead ? new Color(1f, 0.85f, 0.2f) : Color.white;
        col.a = 1f - age / 0.18f;
        float thick = Mathf.Max(2f, 2.5f * scale), len = 9f * scale, off = 7f * scale;

        var saved = GUI.matrix;
        for (int i = 0; i < 4; i++)
        {
            GUIUtility.RotateAroundPivot(45f + 90f * i, c);
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - thick / 2, c.y - off - len, thick, len), Texture2D.whiteTexture);
            GUI.matrix = saved;
        }
        GUI.color = Color.white;
    }

    /// Red wedges around the crosshair pointing toward recent attackers.
    void DrawDamageIndicators()
    {
        var playerT = player.transform;
        Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float radius = 120f * scale;
        var saved = GUI.matrix;
        for (int i = damageSources.Count - 1; i >= 0; i--)
        {
            float age = Time.unscaledTime - damageSources[i].time;
            if (age > 1.2f)
            {
                damageSources.RemoveAt(i);
                continue;
            }
            Vector3 to = damageSources[i].source - playerT.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) continue;
            float angle = Vector3.SignedAngle(playerT.forward, to, Vector3.up);
            float a = 1f - age / 1.2f;
            GUIUtility.RotateAroundPivot(angle, c);
            for (int k = -3; k <= 3; k++)
            {
                float w = (14f - Mathf.Abs(k) * 3f) * scale;
                Box(new Rect(c.x + k * 9f * scale - w * 0.5f, c.y - radius - (3 - Mathf.Abs(k)) * 2f * scale, w, 6f * scale),
                    new Color(1f, 0.15f, 0.1f, a * 0.85f));
            }
            GUI.matrix = saved;
        }
    }

    void DrawHealth()
    {
        var h = health;
        float frac = h.Current / h.maxHealth;
        float x = 40 * scale, y = Screen.height - 80 * scale, w = 320 * scale, bh = 22 * scale;

        Text(new Rect(x, y - 34 * scale, w, 30 * scale), "HEALTH", small, new Color(1f, 1f, 1f, 0.7f));
        Box(new Rect(x - 2, y - 2, w + 4, bh + 4), new Color(0f, 0f, 0f, 0.55f));
        Box(new Rect(x, y, w * frac, bh), Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.3f, 0.9f, 0.4f), frac));
        var style = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
        Text(new Rect(x, y, w, bh), Mathf.CeilToInt(h.Current).ToString(), style, Color.white);
    }

    void DrawAmmo()
    {
        var gun = weapons.Current;
        float right = Screen.width - 40 * scale;
        float y = Screen.height - 130 * scale;

        var rightBig = new GUIStyle(big) { alignment = TextAnchor.LowerRight };
        var rightSmall = new GUIStyle(small) { alignment = TextAnchor.LowerRight };
        var rightLabel = new GUIStyle(label) { alignment = TextAnchor.LowerRight };

        Text(new Rect(right - 400 * scale, y - 40 * scale, 400 * scale, 36 * scale), gun.displayName.ToUpper(), rightSmall, new Color(1f, 1f, 1f, 0.7f));

        Color ammoColor = gun.AmmoInMag <= gun.magazineSize * 0.25f ? new Color(1f, 0.35f, 0.3f) : Color.white;
        string reserve = gun.ReserveDisplay < 0 ? "/ ∞" : $"/ {gun.ReserveDisplay}";
        float reserveW = 110 * scale;
        Text(new Rect(right - reserveW, y, reserveW, 80 * scale), reserve, rightLabel, new Color(1f, 1f, 1f, 0.7f));
        Text(new Rect(right - reserveW - 200 * scale, y, 195 * scale, 90 * scale), gun.AmmoInMag.ToString(), rightBig, ammoColor);

        var center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        float cy = Screen.height * 0.5f + 60 * scale;
        if (gun.IsReloading)
        {
            float w = 160 * scale;
            Rect r = new Rect(Screen.width * 0.5f - w / 2, cy, w, 6 * scale);
            Box(r, new Color(0f, 0f, 0f, 0.5f));
            Box(new Rect(r.x, r.y, w * gun.ReloadProgress, r.height), Color.white);
            Text(new Rect(r.x - 100, cy + 8 * scale, w + 200, 30 * scale), "RELOADING", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, Color.white);
        }
        else if (gun.AmmoInMag == 0 && gun.ReserveDisplay == 0)
            Text(new Rect(0, cy, Screen.width, 40 * scale), "NO AMMO - SWITCH WEAPON", center, new Color(1f, 0.3f, 0.3f));
        else if (gun.AmmoInMag <= gun.magazineSize * 0.25f)
            Text(new Rect(0, cy, Screen.width, 40 * scale), "[R] RELOAD", center, new Color(1f, 0.85f, 0.3f));
    }

    void DrawTopBar()
    {
        var center = new GUIStyle(label) { alignment = TextAnchor.UpperCenter };
        var centerSmall = new GUIStyle(small) { alignment = TextAnchor.UpperCenter };
        float y = 24 * scale;

        if (GM.Wave > 0) Text(new Rect(0, y, Screen.width, 50 * scale), $"WAVE {GM.Wave}", center, Color.white);
        string sub = GM.IntermissionTimeLeft > 0f
            ? $"Next wave in {Mathf.CeilToInt(GM.IntermissionTimeLeft)}"
            : $"Enemies remaining: {GM.EnemiesRemaining}";
        Text(new Rect(0, y + 46 * scale, Screen.width, 36 * scale), sub, centerSmall, new Color(1f, 1f, 1f, 0.8f));

        var rightStyle = new GUIStyle(label) { alignment = TextAnchor.UpperRight };
        Text(new Rect(Screen.width - 440 * scale, y, 400 * scale, 50 * scale), $"SCORE {GM.Score}", rightStyle, Color.white);
        Text(new Rect(40 * scale, y, 400 * scale, 50 * scale), $"KILLS {GM.Kills}", label, Color.white);
    }

    /// Owned weapon slots along the bottom center.
    void DrawWeaponBar()
    {
        var wm = weapons;
        int count = wm.guns.Length;
        float w = 92f * scale, h = 40f * scale, gap = 6f * scale;
        float total = count * w + (count - 1) * gap;
        float x = Screen.width * 0.5f - total * 0.5f, y = Screen.height - 64f * scale;
        var style = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(16 * scale) };
        var keyStyle = new GUIStyle(small) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(12 * scale) };
        for (int i = 0; i < count; i++)
        {
            var r = new Rect(x + i * (w + gap), y, w, h);
            bool owned = wm.IsOwned(i), current = i == wm.CurrentIndex;
            Box(r, current ? new Color(1f, 0.75f, 0.2f, 0.35f) : new Color(0f, 0f, 0f, owned ? 0.45f : 0.2f));
            if (current) Box(new Rect(r.x, r.yMax - 3f * scale, r.width, 3f * scale), new Color(1f, 0.75f, 0.2f));
            Color col = owned ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            Text(new Rect(r.x + 4f * scale, r.y + 2f * scale, 20f * scale, 16f * scale), (i + 1).ToString(), keyStyle, col);
            Text(r, owned ? wm.guns[i].shortName : "LOCKED", style, col);
        }
    }

    void DrawBossBar()
    {
        var boss = GM.ActiveBoss;
        if (boss == null) return;
        float w = 600f * scale, h = 16f * scale, x = Screen.width * 0.5f - w * 0.5f, y = 150f * scale;
        var centerSmall = new GUIStyle(small) { alignment = TextAnchor.LowerCenter };
        Text(new Rect(0, y - 30f * scale, Screen.width, 28f * scale), boss.displayName.ToUpper(), centerSmall, new Color(1f, 0.6f, 0.4f));
        Box(new Rect(x - 2, y - 2, w + 4, h + 4), new Color(0f, 0f, 0f, 0.6f));
        Box(new Rect(x, y, w * Mathf.Clamp01(boss.Health / boss.maxHealth), h), new Color(0.9f, 0.25f, 0.15f));
    }

    void DrawScope()
    {
        float size = Screen.height;
        float x = (Screen.width - size) * 0.5f;
        // Slight drift so the scope feels hand-held.
        float drift = Mathf.Sin(Time.time * 1.3f) * 2f * scale;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, x + 1, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + size - 1, 0, x + 1, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x, drift, size, size), scopeTex);

        Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f + drift);
        var line = new Color(0f, 0f, 0f, 0.9f);
        float thin = Mathf.Max(1f, 1.5f * scale), thick = 4f * scale;
        Box(new Rect(c.x - size * 0.45f, c.y - thin / 2, size * 0.9f, thin), line);
        Box(new Rect(c.x - thin / 2, c.y - size * 0.45f, thin, size * 0.9f), line);
        Box(new Rect(c.x - size * 0.45f, c.y - thick / 2, size * 0.3f, thick), line);
        Box(new Rect(c.x + size * 0.15f, c.y - thick / 2, size * 0.3f, thick), line);
        Box(new Rect(c.x - thick / 2, c.y + size * 0.15f, thick, size * 0.3f), line);
        for (int i = 1; i <= 4; i++)
            Box(new Rect(c.x - 6f * scale, c.y + i * size * 0.03f, 12f * scale, thin), line);
        Box(new Rect(c.x - 2f * scale, c.y - 2f * scale, 4f * scale, 4f * scale), new Color(1f, 0.15f, 0.1f));
    }

    public void ShowUnlock(string weaponName, int slot)
    {
        unlockText = $"NEW WEAPON: {weaponName.ToUpper()}  [{slot}]";
        unlockTime = Time.unscaledTime;
    }

    void DrawUnlock()
    {
        float age = Time.unscaledTime - unlockTime;
        if (string.IsNullOrEmpty(unlockText) || age > 3.5f) return;
        float a = age < 0.2f ? age / 0.2f : 1f - Mathf.Clamp01((age - 2.8f) / 0.7f);
        var style = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        float w = 620f * scale, h = 56f * scale, y = Screen.height * 0.25f + 120f * scale;
        Box(new Rect(Screen.width * 0.5f - w * 0.5f, y, w, h), new Color(0f, 0f, 0f, 0.55f * a));
        Box(new Rect(Screen.width * 0.5f - w * 0.5f, y, w, 3f * scale), new Color(0.3f, 0.9f, 1f, a));
        Text(new Rect(0, y, Screen.width, h), unlockText, style, new Color(0.5f, 0.95f, 1f, a));
    }

    void DrawMessages()
    {
        float x = Screen.width * 0.5f + 40 * scale, y = Screen.height * 0.5f - 20 * scale;
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            float age = Time.unscaledTime - messages[i].time;
            if (age > 1.5f)
            {
                messages.RemoveAt(i);
                continue;
            }
            var col = messages[i].color;
            col.a = 1f - age / 1.5f;
            Text(new Rect(x, y - age * 30 * scale - (messages.Count - 1 - i) * 30 * scale, 300 * scale, 30 * scale), messages[i].text, small, col);
        }
    }

    void DrawBanner()
    {
        float age = Time.unscaledTime - bannerTime;
        if (string.IsNullOrEmpty(banner) || age > 2.5f) return;
        float a = age < 0.2f ? age / 0.2f : 1f - Mathf.Clamp01((age - 1.8f) / 0.7f);
        var style = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter };
        Text(new Rect(0, Screen.height * 0.25f, Screen.width, 120 * scale), banner, style, new Color(1f, 0.8f, 0.3f, a));
    }

    void DrawHints()
    {
        var style = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        if (GM.IsPlaying && Cursor.lockState != CursorLockMode.Locked)
            Text(new Rect(0, Screen.height * 0.5f - 120 * scale, Screen.width, 40 * scale), "CLICK TO CAPTURE MOUSE", style, Color.white);

        if (GM.SurvivalTime < 12f)
        {
            float a = Mathf.Clamp01((12f - GM.SurvivalTime) / 2f);
            Text(new Rect(Screen.width * 0.15f, Screen.height - 115 * scale, Screen.width * 0.7f, 40 * scale),
                player != null && player.fixedPosition
                    ? (player.FixedCamera
                        ? "Gun / mouse moves the crosshair  ·  Trigger / LMB fire  ·  B / R reload  ·  S / 1-6 / Scroll switch weapon  ·  Esc pause"
                        : "Mouse aim  ·  LMB fire  ·  RMB aim down sights  ·  R reload  ·  1-6/Q/Scroll switch weapon  ·  Esc pause")
                    : "WASD move  ·  Shift sprint  ·  Space jump  ·  Ctrl crouch  ·  LMB fire  ·  RMB aim  ·  R reload  ·  1-6/Q/Scroll switch  ·  Esc pause",
                style, new Color(1f, 1f, 1f, 0.8f * a));
        }
    }

    void DrawPauseMenu()
    {
        Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));
        var title = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter };
        Text(new Rect(0, Screen.height * 0.18f, Screen.width, 120 * scale), "PAUSED", title, Color.white);

        float bw = 280 * scale, bh = 56 * scale, x = Screen.width * 0.5f - bw / 2, y = Screen.height * 0.36f;
        if (showSettings)
        {
            SettingsPanel.Draw(new Rect(Screen.width * 0.5f - 330 * scale, y, 660 * scale, 300 * scale), small, button, scale, false);
            if (Button(new Rect(x, y + 354 * scale, bw, bh), "BACK")) showSettings = false;
            return;
        }
        if (Button(new Rect(x, y, bw, bh), "RESUME")) GM.SetPaused(false);
        if (Button(new Rect(x, y + bh * 1.3f, bw, bh), "SETTINGS")) showSettings = true;
        if (Button(new Rect(x, y + bh * 2.6f, bw, bh), "RESTART")) GM.Restart();
        if (Button(new Rect(x, y + bh * 3.9f, bw, bh), "MAIN MENU")) GM.LoadMainMenu();
        if (Button(new Rect(x, y + bh * 5.2f, bw, bh), "QUIT")) GM.Quit();
    }

    void DrawGameOver()
    {
        Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0.1f, 0f, 0f, 0.65f));
        var title = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter };
        var center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        Text(new Rect(0, Screen.height * 0.16f, Screen.width, 120 * scale), "YOU DIED", title, new Color(1f, 0.3f, 0.25f));
        if (GM.NewBest)
            Text(new Rect(0, Screen.height * 0.16f + 100 * scale, Screen.width, 50 * scale), "NEW BEST SCORE!", center, new Color(1f, 0.85f, 0.3f));

        int t = Mathf.FloorToInt(GM.SurvivalTime);
        var d = GameSettings.Difficulty;
        string stats = $"Difficulty: {d}\nWave reached: {GM.Wave}\nKills: {GM.Kills}\nTime: {t / 60}:{t % 60:00}\nScore: {GM.Score}\nBest: {GameSettings.GetBestScore(d)}";
        Text(new Rect(0, Screen.height * 0.32f, Screen.width, 300 * scale), stats, center, Color.white);

        float bw = 280 * scale, bh = 56 * scale, x = Screen.width * 0.5f - bw / 2, y = Screen.height * 0.64f;
        if (Button(new Rect(x, y, bw, bh), "RESTART")) GM.Restart();
        if (Button(new Rect(x, y + bh * 1.3f, bw, bh), "MAIN MENU")) GM.LoadMainMenu();
        if (Button(new Rect(x, y + bh * 2.6f, bw, bh), "QUIT")) GM.Quit();
    }

    bool Button(Rect r, string text)
    {
        if (!GUI.Button(r, text, button)) return false;
        ProceduralAudio.Play2D(ProceduralAudio.UIClick, 0.6f, 0f);
        return true;
    }

    // ---------- Helpers ----------

    void EnsureStyles()
    {
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            big = new GUIStyle(label);
            small = new GUIStyle(label);
            button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }
        label.fontSize = Mathf.RoundToInt(32 * scale);
        big.fontSize = Mathf.RoundToInt(72 * scale);
        small.fontSize = Mathf.RoundToInt(20 * scale);
        button.fontSize = Mathf.RoundToInt(24 * scale);
    }

    static void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static void Text(Rect r, string text, GUIStyle style, Color color)
    {
        var shadow = new Rect(r.x + 2, r.y + 2, r.width, r.height);
        style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.7f);
        GUI.Label(shadow, text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }

    static Texture2D MakeVignette(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x / (size - 1f)) * 2f - 1f, dy = (y / (size - 1f)) * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            px[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, (d - 0.5f) / 0.8f));
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
