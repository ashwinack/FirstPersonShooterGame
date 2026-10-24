using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Title screen: slow orbit over the arena, play / difficulty / settings / controls / quit.</summary>
public class MainMenu : MonoBehaviour
{
    public const string GameScene = "Arena";

    public Camera menuCamera;
    public Vector3 orbitCenter = Vector3.zero;
    public float orbitRadius = 34f;
    public float orbitHeight = 13f;
    public float orbitSpeed = 3f;

    enum Panel { Main, Settings, Controls }
    Panel panel = Panel.Main;
    GUIStyle title, subtitle, label, small, button;
    float scale, angle, fadeIn;
    Texture2D gradient;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameSettings.Apply();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Gun-driven menu pointer (hides the system cursor while the gun is active; mouse still works in mouse mode).
        if (GetComponent<MenuCrosshair>() == null) gameObject.AddComponent<MenuCrosshair>();
        angle = Random.Range(0f, 360f);

        // Left-side darkening so the menu reads over the scene.
        gradient = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 256; x++) gradient.SetPixel(x, 0, new Color(0f, 0f, 0f, Mathf.Lerp(0.8f, 0f, x / 255f)));
        gradient.Apply();
    }

    void OnDestroy()
    {
        if (gradient != null) Destroy(gradient);
    }

    void Update()
    {
        angle += orbitSpeed * Time.deltaTime;
        fadeIn = Mathf.MoveTowards(fadeIn, 1f, Time.deltaTime * 0.8f);
        if (menuCamera == null) return;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, orbitHeight + Mathf.Sin(Time.time * 0.2f) * 2f, -orbitRadius);
        menuCamera.transform.position = orbitCenter + offset;
        menuCamera.transform.LookAt(orbitCenter + Vector3.up * 2f);
    }

    void OnGUI()
    {
        scale = Screen.height / 1080f;
        EnsureStyles();

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, Screen.width * 0.65f, Screen.height), gradient);
        // Fade from black on load.
        GUI.color = new Color(0f, 0f, 0f, 1f - fadeIn);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float x = 110f * scale;
        Text(new Rect(x, 120f * scale, 1200f * scale, 140f * scale), "IRON SURGE", title, new Color(1f, 0.8f, 0.35f));
        Text(new Rect(x + 6f * scale, 245f * scale, 1200f * scale, 40f * scale), "WAVE SURVIVAL  ·  HOW LONG CAN YOU LAST?", subtitle, new Color(1f, 1f, 1f, 0.75f));

        switch (panel)
        {
            case Panel.Main: DrawMain(x); break;
            case Panel.Settings: DrawSettings(x); break;
            case Panel.Controls: DrawControls(x); break;
        }

        var foot = new GUIStyle(small) { alignment = TextAnchor.LowerRight };
        Text(new Rect(0, Screen.height - 50f * scale, Screen.width - 30f * scale, 40f * scale), "Made with Unity  ·  all art & audio procedurally generated", foot, new Color(1f, 1f, 1f, 0.4f));
    }

    void DrawMain(float x)
    {
        float bw = 360f * scale, bh = 64f * scale, y = 340f * scale, gap = bh * 1.22f;
        if (Button(new Rect(x, y, bw, bh), "PLAY SURVIVAL")) SceneManager.LoadScene(GameScene);
        if (Button(new Rect(x, y + gap, bw, bh), $"DIFFICULTY: {GameSettings.Difficulty.ToString().ToUpper()}"))
            GameSettings.Difficulty = (Difficulty)(((int)GameSettings.Difficulty + 1) % 3);
        if (Button(new Rect(x, y + gap * 2, bw, bh), "SETTINGS")) panel = Panel.Settings;
        if (Button(new Rect(x, y + gap * 3, bw, bh), "CONTROLS")) panel = Panel.Controls;
        if (Button(new Rect(x, y + gap * 4, bw, bh), "QUIT")) Quit();

        var d = GameSettings.Difficulty;
        string records = $"BEST ({d.ToString().ToUpper()})\nScore  {GameSettings.GetBestScore(d)}\nWave   {GameSettings.GetBestWave(d)}";
        Text(new Rect(x + bw + 60f * scale, y, 400f * scale, 160f * scale), records, label, new Color(1f, 1f, 1f, 0.85f));
        string diffInfo = d switch
        {
            Difficulty.Easy => "Weaker, fewer enemies.\nGood for learning the weapons.",
            Difficulty.Hard => "Tougher, harder-hitting,\nmore numerous enemies.",
            _ => "The intended experience.",
        };
        Text(new Rect(x + bw + 60f * scale, y + 150f * scale, 500f * scale, 80f * scale), diffInfo, small, new Color(1f, 1f, 1f, 0.6f));
    }

    void DrawSettings(float x)
    {
        float y = 360f * scale;
        Text(new Rect(x, y - 60f * scale, 600f * scale, 50f * scale), "SETTINGS", label, Color.white);
        SettingsPanel.Draw(new Rect(x - 60f * scale, y, 760f * scale, 260f * scale), small, button, scale, true);
        if (Button(new Rect(x, y + 414f * scale, 360f * scale, 64f * scale), "BACK")) panel = Panel.Main;
    }

    void DrawControls(float x)
    {
        float y = 360f * scale;
        Text(new Rect(x, y - 60f * scale, 600f * scale, 50f * scale), "CONTROLS", label, Color.white);
        string keys = "Mouse / gun\nLeft click / trigger\nRight click\nR\n1 - 6 / Scroll\nQ\nF1\nEsc";
        string actions = "Look (180° front)\nFire\nAim / scope\nReload\nSwitch weapon\nLast weapon\nMouse / gun\nPause";
        var col = new GUIStyle(small) { fontSize = Mathf.RoundToInt(24 * scale) };
        Text(new Rect(x, y, 220f * scale, 420f * scale), keys, col, new Color(1f, 0.85f, 0.4f, 0.9f));
        Text(new Rect(x + 220f * scale, y, 300f * scale, 420f * scale), actions, col, new Color(1f, 1f, 1f, 0.85f));
        string tips = "You hold the entrance - enemies come from the front.\nLook, aim and shoot.\nNew weapons unlock as you survive waves.\nRed barrels explode. Green robots explode too.\nEvery 5th wave brings a Brute.";
        Text(new Rect(x + 520f * scale, y, 600f * scale, 200f * scale), tips, small, new Color(1f, 0.85f, 0.4f, 0.85f));
        if (Button(new Rect(x, y + 420f * scale, 360f * scale, 64f * scale), "BACK")) panel = Panel.Main;
    }

    bool Button(Rect r, string text)
    {
        if (!MenuCrosshair.Button(r, text, button)) return false;
        ProceduralAudio.Play2D(ProceduralAudio.UIClick, 0.6f, 0f);
        return true;
    }

    void EnsureStyles()
    {
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            subtitle = new GUIStyle(title);
            label = new GUIStyle(title);
            small = new GUIStyle(title);
            button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        }
        title.fontSize = Mathf.RoundToInt(120 * scale);
        subtitle.fontSize = Mathf.RoundToInt(26 * scale);
        label.fontSize = Mathf.RoundToInt(32 * scale);
        small.fontSize = Mathf.RoundToInt(20 * scale);
        button.fontSize = Mathf.RoundToInt(26 * scale);
        button.padding = new RectOffset(Mathf.RoundToInt(24 * scale), 0, 0, 0);
    }

    static void Text(Rect r, string text, GUIStyle style, Color color)
    {
        var shadow = new Rect(r.x + 3, r.y + 3, r.width, r.height);
        style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.7f);
        GUI.Label(shadow, text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
