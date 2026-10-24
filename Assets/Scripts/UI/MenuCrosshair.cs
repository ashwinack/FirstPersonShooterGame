using UnityEngine;

/// <summary>
/// Menu pointer driven by the hardware gun: aim X/Y moves a crosshair in screen space, P1 clicks the
/// button under it. While the gun is active the system cursor is hidden; in mouse mode (F1) the menu
/// works with the mouse as before. Separate from in-game aim (its own sensitivity, reads the gun's raw X/Y).
///
/// Menus route their buttons through <see cref="Button"/>, which falls back to a normal GUI.Button
/// when there is no active crosshair.
/// </summary>
[DefaultExecutionOrder(-50)] // after HardwareInput (-100) has processed this frame's packets
public class MenuCrosshair : MonoBehaviour
{
    public static MenuCrosshair Current { get; private set; }

    [Tooltip("Relative (delta) gun modes only: pixels (at 1080p) per gun sensor unit. Separate from in-game aim sensitivity. " +
             "In absolute mode the gun's 0-100 range spans the whole screen.")]
    [SerializeField] float sensitivity = 1.5f;
    [SerializeField] float size = 34f;
    [SerializeField] Color color = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] Color hoverColor = new Color(1f, 0.8f, 0.35f, 1f);

    /// <summary>Crosshair position in GUI coordinates (origin top-left).</summary>
    public Vector2 Position { get; private set; }
    /// <summary>True while the gun drives the menu (hardware mode on and receiving).</summary>
    public bool Active { get; private set; }

    bool clickPending, hoveringAny, hoveredLastFrame;

    void OnEnable()
    {
        Current = this;
        Position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
        Cursor.visible = true;
    }

    void Update()
    {
        var hw = HardwareInput.Instance;
        Active = hw != null && hw.Active;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = !Active;

        // A click only lives for the frame P1 arrived in; the hovered button consumes it in OnGUI.
        clickPending = Active && hw.FirePressedThisFrame;
        if (!Active) return;

        if (hw.AbsoluteAim)
        {
            // Absolute gun (x/y 0-100): the whole range spans the screen.
            if (!hw.HasAbsoluteAim) return;
            Vector2 n = hw.AimNormalized;
            Position = new Vector2((n.x + 1f) * 0.5f * Screen.width, (1f - (n.y + 1f) * 0.5f) * Screen.height);
            return;
        }

        Vector2 d = hw.RawAimThisFrame * sensitivity * (Screen.height / 1080f);
        Position = new Vector2(
            Mathf.Clamp(Position.x + d.x, 0f, Screen.width),
            Mathf.Clamp(Position.y - d.y, 0f, Screen.height)); // +Y (aim up) moves up the screen
    }

    /// <summary>
    /// Drop-in for GUI.Button: with an active crosshair, highlights while hovered and returns true
    /// when P1 arrives over it. Otherwise a normal mouse GUI.Button.
    /// </summary>
    public static bool Button(Rect r, string text, GUIStyle style)
    {
        var c = Current;
        if (c == null || !c.isActiveAndEnabled || !c.Active) return GUI.Button(r, text, style);

        bool hover = r.Contains(c.Position);
        if (Event.current.type == EventType.Repaint)
        {
            style.Draw(r, new GUIContent(text), hover, hover, false, false);
            if (hover) DrawOutline(r, c.hoverColor, 3f);
        }
        if (!hover) return false;
        c.hoveringAny = true;
        if (!c.clickPending) return false;
        c.clickPending = false;
        return true;
    }

    void OnGUI()
    {
        if (!Active) return;
        GUI.depth = -1000; // on top of the menu
        if (Event.current.type != EventType.Repaint) return;

        float s = Screen.height / 1080f;
        float half = size * 0.5f * s, gap = 6f * s, t = Mathf.Max(2f, 3f * s);
        Color col = hoveredLastFrame ? hoverColor : color;
        Vector2 p = Position;

        Fill(new Rect(p.x - half - 1, p.y - t * 0.5f - 1, half - gap + 2, t + 2), new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(p.x + gap - 1, p.y - t * 0.5f - 1, half - gap + 2, t + 2), new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(p.x - t * 0.5f - 1, p.y - half - 1, t + 2, half - gap + 2), new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(p.x - t * 0.5f - 1, p.y + gap - 1, t + 2, half - gap + 2), new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(p.x - half, p.y - t * 0.5f, half - gap, t), col);
        Fill(new Rect(p.x + gap, p.y - t * 0.5f, half - gap, t), col);
        Fill(new Rect(p.x - t * 0.5f, p.y - half, t, half - gap), col);
        Fill(new Rect(p.x - t * 0.5f, p.y + gap, t, half - gap), col);
        Fill(new Rect(p.x - t * 0.5f, p.y - t * 0.5f, t, t), col);
    }

    void LateUpdate()
    {
        // Hover state from the previous GUI pass (OnGUI runs after LateUpdate), used for the crosshair colour.
        hoveredLastFrame = hoveringAny;
        hoveringAny = false;
    }

    static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    static void DrawOutline(Rect r, Color c, float w)
    {
        Fill(new Rect(r.x, r.y, r.width, w), c);
        Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
        Fill(new Rect(r.x, r.y, w, r.height), c);
        Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        Fill(r, new Color(c.r, c.g, c.b, 0.15f));
    }
}
