using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hardware gun input (ESP32 + MPU over USB serial or UDP). Owns the receiver, parser and aim controller,
/// and exposes per-frame look degrees and fire state that PlayerController / WeaponManager read
/// when hardware mode is on. Created automatically at startup if no instance exists in the scene.
///
/// Hotkeys: F1 = mouse/hardware toggle, F2 = tuning panel, F3 = debug overlay.
/// Messages: "1(x,y)" aim (absolute position, x/y 0-100, by default), "P1" trigger pressed, "R1" trigger released,
/// "S1" switch weapon, "B1" reload. "X = 0 Y = 8" is accepted too. Receive only.
/// </summary>
public enum HardwareSource { Serial, Udp }

/// <summary>See HardwareSource / the class summary below.</summary>
[DefaultExecutionOrder(-100)] // process packets before the player and weapons read them
[DisallowMultipleComponent]
public class HardwareInput : MonoBehaviour
{
    public static HardwareInput Instance { get; private set; }

    [Header("Input source")]
    [Tooltip("Off = mouse aims and fires. On = the hardware gun does.")]
    [SerializeField] bool useHardware;
    [SerializeField] Key toggleKey = Key.F1;
    [SerializeField] Key panelKey = Key.F2;
    [SerializeField] Key debugKey = Key.F3;

    [Header("Connection")]
    [Tooltip("Serial = USB cable (COM port). UDP = Wi-Fi.")]
    [SerializeField] HardwareSource source = HardwareSource.Serial;

    [Header("Serial")]
    [Tooltip("COM port of the ESP32. Close Protokol Bridge / Hercules / Serial Monitor while playing: only one program can open a COM port.")]
    [SerializeField] string serialPort = "COM3";
    [SerializeField] int baudRate = 115200;
    [Tooltip("Assert DTR when opening. Some ESP32 boards (native USB) only send while DTR is on.")]
    [SerializeField] bool serialDtr = true;

    [Header("UDP")]
    [Tooltip("Port Unity listens on (receive only). The ESP32 sends here. Only one program can listen on a port.")]
    [SerializeField] int port = 8888;
    [Tooltip("Aim packets look like 1(x,y); only this id is used. 0 = accept any id.")]
    [SerializeField] int deviceId = 1;
    [Tooltip("P<n> / R<n> number that fires.")]
    [SerializeField] int fireButton = 1;

    [Header("Aim")]
    [SerializeField] HardwareAimSettings aim = new HardwareAimSettings();

    [Header("Debug")]
    [SerializeField] bool showDebug = true;

    /// <summary>True when the gun is driving aim and fire (hardware mode on and the socket is listening).</summary>
    public bool Active => useHardware && receiver != null && receiver.IsRunning;
    public bool UseHardware => useHardware;
    public HardwareSource Source => source;
    /// <summary>UDP port currently configured (saved between sessions).</summary>
    public int Port => port;
    public string SerialPortName => serialPort;
    public int BaudRate => baudRate;
    /// <summary>True while the UDP socket is listening / the COM port is open.</summary>
    public bool Listening => receiver != null && receiver.IsRunning;
    public string LastError => receiver?.LastError;
    public string Endpoint => receiver?.Endpoint ?? "-";
    public long ReceivedCount => receiver?.Received ?? 0;

    /// <summary>Trigger is down (also true on the frame of a very short tap).</summary>
    public bool FireHeld => Active && (held || pressedThisFrame);
    public bool FirePressedThisFrame => Active && pressedThisFrame;
    public bool FireReleasedThisFrame => Active && releasedThisFrame;

    /// <summary>
    /// Sum of this frame's aim X/Y in sensor units (dead zone and invert applied, no in-game sensitivity),
    /// +X right, +Y up. For screen-space pointers such as the menu crosshair; reading it doesn't affect in-game aim.
    /// </summary>
    public Vector2 RawAimThisFrame => Active ? rawAimThisFrame : Vector2.zero;
    Vector2 rawAimThisFrame;

    /// <summary>The gun sends its absolute position (x/y 0-100) rather than movement.</summary>
    public bool AbsoluteAim => aim.mode == HardwareAimMode.Absolute;
    /// <summary>Absolute mode and at least one position has arrived.</summary>
    public bool HasAbsoluteAim => Active && AbsoluteAim && aimController.HasAbsolute;
    /// <summary>Absolute mode: smoothed gun position this frame, -1..1 per axis (0,0 = straight ahead, +X right, +Y up).</summary>
    public Vector2 AimNormalized => HasAbsoluteAim ? aimNormalized : Vector2.zero;
    Vector2 aimNormalized;

    /// <summary>"S1" arrived this frame: switch to the next weapon.</summary>
    public bool SwitchWeaponPressedThisFrame => Active && switchPressedThisFrame;
    /// <summary>"B1" arrived this frame: reload.</summary>
    public bool ReloadPressedThisFrame => Active && reloadPressedThisFrame;
    bool switchPressedThisFrame, reloadPressedThisFrame;

    static bool bootstrapping;

    IHardwareReceiver receiver;
    HardwareAimController aimController;
    bool held, pressedThisFrame, releasedThisFrame, consumedThisFrame, panelOpen, previousRunInBackground;

    // Debug state.
    string lastRaw = "-", lastParsed = "-", lastFire = "-", lastUnknown = "-";
    Vector2 lastXY, lastFrameDegrees;
    int malformed, packetsThisSecond, packetRate;
    float rateWindowStart;
    string portField, comField, baudField;
    GUIStyle box, label, button;

    // ======================= Lifecycle =======================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null || FindAnyObjectByType<HardwareInput>() != null) return;
        bootstrapping = true;
        new GameObject("HardwareInput").AddComponent<HardwareInput>();
        bootstrapping = false;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        if (bootstrapping || transform.parent == null && GetComponents<Component>().Length == 2)
            DontDestroyOnLoad(gameObject); // our own object: survive scene changes so the socket stays open

        LoadPrefs();
        aimController = new HardwareAimController(aim);
        StartReceiver();
        portField = port.ToString(CultureInfo.InvariantCulture);
        comField = serialPort;
        baudField = baudRate.ToString(CultureInfo.InvariantCulture);
        previousRunInBackground = Application.runInBackground;
        ApplyRunInBackground();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        receiver?.Stop();
        Application.runInBackground = previousRunInBackground;
        Instance = null;
    }

    void OnApplicationQuit() => receiver?.Stop();

    void OnApplicationFocus(bool focused)
    {
        if (!focused) ReleaseFire();
    }

    // ======================= Per frame =======================

    void Update()
    {
        HandleHotkeys();

        pressedThisFrame = releasedThisFrame = switchPressedThisFrame = reloadPressedThisFrame = false;
        rawAimThisFrame = Vector2.zero;
        int processed = 0;
        while (receiver != null && receiver.TryDequeue(out string datagram))
        {
            ProcessDatagram(datagram);
            if (++processed >= 4000) break; // never stall a frame
        }
        if (AbsoluteAim) aimNormalized = aimController.UpdateAbsolute(Time.unscaledDeltaTime);

        if (Time.unscaledTime - rateWindowStart >= 1f)
        {
            packetRate = packetsThisSecond;
            packetsThisSecond = 0;
            rateWindowStart = Time.unscaledTime;
        }
    }

    void LateUpdate()
    {
        // Nobody consumed the aim this frame (paused, menu, no player): drop it so it can't build up.
        if (!consumedThisFrame) aimController.Reset();
        consumedThisFrame = false;
    }

    /// <summary>
    /// Degrees to rotate this frame: x = yaw (right +), y = pitch (up +). Call once per frame.
    /// Returns zero when hardware mode is off.
    /// </summary>
    public Vector2 ConsumeLookDegrees()
    {
        consumedThisFrame = true;
        if (!Active) return Vector2.zero;
        lastFrameDegrees = aimController.Consume(Time.unscaledDeltaTime);
        return lastFrameDegrees;
    }

    readonly System.Collections.Generic.List<string> messages = new System.Collections.Generic.List<string>(8);

    void ProcessDatagram(string datagram)
    {
        // Usually one message per datagram, but back-to-back ones ("1(1,1)1(0,1)P1") are split too.
        HardwarePacketParser.Split(datagram, messages);
        foreach (string m in messages) ProcessMessage(m);
    }

    void ProcessMessage(string message)
    {
        packetsThisSecond++;
        lastRaw = message.Trim();
        if (!HardwarePacketParser.TryParse(message, out HardwarePacket p))
        {
            malformed++;
            lastUnknown = lastRaw;
            return;
        }
        lastParsed = p.ToString();

        switch (p.Kind)
        {
            case HardwarePacketKind.Aim:
                // Messages without an id ("X = 0 Y = 8", "[x,y]") always count; "2(x,y)" is ignored when deviceId is 1.
                if (deviceId != 0 && p.Id != 0 && p.Id != deviceId) return;
                lastXY = new Vector2(p.X, p.Y);
                if (!useHardware) return;
                if (AbsoluteAim)
                {
                    aimController.SetAbsolute(p.X, p.Y);
                    return;
                }
                aimController.Add(p.X, p.Y);
                float rx = Mathf.Abs(p.X) <= aim.deadZone ? 0f : p.X - Mathf.Sign(p.X) * aim.deadZone;
                float ry = Mathf.Abs(p.Y) <= aim.deadZone ? 0f : p.Y - Mathf.Sign(p.Y) * aim.deadZone;
                rawAimThisFrame += new Vector2(aim.invertX ? -rx : rx, aim.invertY ? -ry : ry);
                break;

            case HardwarePacketKind.ButtonDown:
                if (p.Id != fireButton) return;
                lastFire = $"P{p.Id} pressed at {Time.unscaledTime:0.00}s";
                if (!useHardware) return;
                held = true;
                pressedThisFrame = true;
                break;

            case HardwarePacketKind.ButtonUp:
                if (p.Id != fireButton) return;
                lastFire = $"R{p.Id} released at {Time.unscaledTime:0.00}s";
                if (!useHardware) return;
                if (held) releasedThisFrame = true;
                held = false;
                break;

            case HardwarePacketKind.SwitchWeapon:
            case HardwarePacketKind.Reload:
                if (p.Id != fireButton) return; // same number as the trigger ("S1"/"B1" with "P1")
                bool swap = p.Kind == HardwarePacketKind.SwitchWeapon;
                lastFire = $"{(swap ? "S" : "B")}{p.Id} ({(swap ? "switch weapon" : "reload")}) at {Time.unscaledTime:0.00}s";
                if (!useHardware) return;
                if (swap) switchPressedThisFrame = true;
                else reloadPressedThisFrame = true;
                break;
        }
    }

    // ======================= Controls =======================

    void HandleHotkeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[toggleKey].wasPressedThisFrame) SetUseHardware(!useHardware);
        if (kb[panelKey].wasPressedThisFrame) panelOpen = !panelOpen;
        if (kb[debugKey].wasPressedThisFrame)
        {
            showDebug = !showDebug;
            SavePrefs();
        }
    }

    public void SetUseHardware(bool on)
    {
        useHardware = on;
        ReleaseFire();
        aimController.Reset();
        ApplyRunInBackground();
        SavePrefs();
    }

    void ReleaseFire()
    {
        if (held) releasedThisFrame = true;
        held = false;
    }

    /// Keep listening when another window (e.g. the bridge) has focus while the gun is in use.
    void ApplyRunInBackground() => Application.runInBackground = useHardware || previousRunInBackground;

    /// <summary>(Re)start the receiver for the current source and settings.</summary>
    void StartReceiver()
    {
        receiver?.Stop();
        if (source == HardwareSource.Serial)
        {
            var serial = new SerialReceiver();
            serial.Start(serialPort, baudRate, serialDtr);
            receiver = serial;
        }
        else
        {
            var udp = new UdpReceiver();
            udp.Start(port);
            receiver = udp;
        }
        ReleaseFire();
        aimController.Reset();
    }

    public void SetSource(HardwareSource newSource)
    {
        if (newSource == source && receiver != null) return;
        source = newSource;
        StartReceiver();
        SavePrefs();
    }

    /// <summary>Use serial on this COM port / baud rate (opens in the background; see Listening/LastError).</summary>
    public void ConfigureSerial(string portName, int baud)
    {
        if (!string.IsNullOrWhiteSpace(portName)) serialPort = portName.Trim().ToUpperInvariant();
        if (baud > 0) baudRate = baud;
        comField = serialPort;
        baudField = baudRate.ToString(CultureInfo.InvariantCulture);
        source = HardwareSource.Serial;
        StartReceiver();
        SavePrefs();
    }

    /// <summary>Use UDP on another port (1-65535). Returns false if the port is invalid or busy.</summary>
    public bool RestartReceiver(int newPort)
    {
        if (newPort < 1 || newPort > 65535) return false;
        port = newPort;
        portField = port.ToString(CultureInfo.InvariantCulture);
        source = HardwareSource.Udp;
        StartReceiver();
        SavePrefs();
        return receiver.IsRunning;
    }

    // ======================= Persistence =======================

    const string Prefix = "HW_";

    void LoadPrefs()
    {
        useHardware = PlayerPrefs.GetInt(Prefix + "Use", useHardware ? 1 : 0) == 1;
        source = (HardwareSource)PlayerPrefs.GetInt(Prefix + "Source", (int)source);
        serialPort = PlayerPrefs.GetString(Prefix + "Com", serialPort);
        baudRate = PlayerPrefs.GetInt(Prefix + "Baud", baudRate);
        port = PlayerPrefs.GetInt(Prefix + "Port", port);
        deviceId = PlayerPrefs.GetInt(Prefix + "Device", deviceId);
        fireButton = PlayerPrefs.GetInt(Prefix + "FireButton", fireButton);
        aim.mode = (HardwareAimMode)PlayerPrefs.GetInt(Prefix + "Mode", (int)aim.mode);
        aim.sensitivity = PlayerPrefs.GetFloat(Prefix + "Sensitivity", aim.sensitivity);
        aim.deadZone = PlayerPrefs.GetFloat(Prefix + "DeadZone", aim.deadZone);
        aim.smoothing = PlayerPrefs.GetFloat(Prefix + "Smoothing", aim.smoothing);
        aim.invertX = PlayerPrefs.GetInt(Prefix + "InvertX", aim.invertX ? 1 : 0) == 1;
        aim.invertY = PlayerPrefs.GetInt(Prefix + "InvertY", aim.invertY ? 1 : 0) == 1;
        showDebug = PlayerPrefs.GetInt(Prefix + "Debug", showDebug ? 1 : 0) == 1;
        aim.absoluteMin = PlayerPrefs.GetFloat(Prefix + "AbsMin", aim.absoluteMin);
        aim.absoluteMax = PlayerPrefs.GetFloat(Prefix + "AbsMax", aim.absoluteMax);

        // Protocol v2 (x/y 0-100 absolute position, S1/B1 buttons): switch saved settings over once.
        if (PlayerPrefs.GetInt(Prefix + "Protocol", 1) < 2)
        {
            aim.mode = HardwareAimMode.Absolute;
            PlayerPrefs.SetInt(Prefix + "Protocol", 2);
            SavePrefs();
        }
    }

    void SavePrefs()
    {
        PlayerPrefs.SetInt(Prefix + "Use", useHardware ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "Source", (int)source);
        PlayerPrefs.SetString(Prefix + "Com", serialPort);
        PlayerPrefs.SetInt(Prefix + "Baud", baudRate);
        PlayerPrefs.SetInt(Prefix + "Port", port);
        PlayerPrefs.SetInt(Prefix + "Device", deviceId);
        PlayerPrefs.SetInt(Prefix + "FireButton", fireButton);
        PlayerPrefs.SetInt(Prefix + "Mode", (int)aim.mode);
        PlayerPrefs.SetFloat(Prefix + "Sensitivity", aim.sensitivity);
        PlayerPrefs.SetFloat(Prefix + "DeadZone", aim.deadZone);
        PlayerPrefs.SetFloat(Prefix + "Smoothing", aim.smoothing);
        PlayerPrefs.SetInt(Prefix + "InvertX", aim.invertX ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "InvertY", aim.invertY ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "Debug", showDebug ? 1 : 0);
        PlayerPrefs.SetFloat(Prefix + "AbsMin", aim.absoluteMin);
        PlayerPrefs.SetFloat(Prefix + "AbsMax", aim.absoluteMax);
        PlayerPrefs.Save();
    }

    void ResetToDefaults()
    {
        var d = new HardwareAimSettings();
        aim.mode = d.mode;
        aim.absoluteMin = d.absoluteMin;
        aim.absoluteMax = d.absoluteMax;
        aim.sensitivity = d.sensitivity;
        aim.deadZone = d.deadZone;
        aim.smoothing = d.smoothing;
        aim.invertX = d.invertX;
        aim.invertY = d.invertY;
        deviceId = 1;
        fireButton = 1;
        SavePrefs();
    }

    // ======================= Debug / tuning UI =======================

    void OnGUI()
    {
        if (!showDebug && !panelOpen) return;
        float s = Screen.height / 1080f;
        EnsureStyles(s);
        if (showDebug) DrawDebug(s);
        if (panelOpen) DrawPanel(s);
    }

    void DrawDebug(float s)
    {
        bool serial = source == HardwareSource.Serial;
        string status = receiver.IsRunning
            ? $"{receiver.Endpoint} {(serial ? "open" : "listening")}"
            : $"{receiver.Endpoint} {(serial ? "NOT OPEN (retrying)" : "NOT LISTENING")}";
        string noData = serial
            ? $"No data yet. Check: the ESP32 is on {serialPort}, baud is {baudRate}, and no other app (Protokol Bridge, Hercules, Arduino Serial Monitor) has the port open."
            : $"No packets yet. Check: Windows Firewall allows Unity on this network type, no other app is open on {port}, and the ESP32 sends to this PC's IP.";
        string text =
            $"<b>HARDWARE INPUT</b>  [{toggleKey}] {(useHardware ? "<color=#7CFF8A>HARDWARE</color>" : "<color=#FFD36A>MOUSE</color>")}" +
            (useHardware && !Active ? $"  <color=#FF7070>({(serial ? "port not open" : "no socket")} - using mouse)</color>" : "") + "\n" +
            $"{status}   {packetRate}/s   total {receiver.Received}   dropped {receiver.Dropped}   bad {malformed}\n" +
            (receiver.LastError != null ? $"<color=#FF7070>{receiver.LastError}</color>\n" : "") +
            (receiver.IsRunning && receiver.Received == 0 && Time.unscaledTime > 3f ? $"<color=#FFD36A>{noData}</color>\n" : "") +
            $"raw     {lastRaw}\n" +
            $"parsed  {lastParsed}\n" +
            (AbsoluteAim
                ? $"x/y     {lastXY.x:0.##}, {lastXY.y:0.##}   aim {aimNormalized.x:+0.00;-0.00}, {aimNormalized.y:+0.00;-0.00} (-1..1)\n"
                : $"x/y     {lastXY.x:0.##}, {lastXY.y:0.##}   frame yaw {lastFrameDegrees.x:+0.00;-0.00}°  pitch {lastFrameDegrees.y:+0.00;-0.00}°\n") +
            $"fire    {(held ? "<color=#FF7070>HELD</color>" : "up")}   last: {lastFire}\n" +
            (malformed > 0 ? $"last bad packet: {lastUnknown}\n" : "") +
            $"[{panelKey}] tuning   [{debugKey}] hide";
        var content = new GUIContent(text);
        float w = 560f * s;
        float h = label.CalcHeight(content, w - 20f * s) + 16f * s;
        var r = new Rect(16f * s, Screen.height * 0.24f, w, h);
        GUI.Box(r, GUIContent.none, box);
        GUI.Label(new Rect(r.x + 10f * s, r.y + 8f * s, w - 20f * s, h), content, label);
    }

    void DrawPanel(float s)
    {
        float w = 560f * s, row = 40f * s;
        var r = new Rect(16f * s, Screen.height * 0.24f + (showDebug ? 300f * s : 0f), w, row * 13.5f);
        GUI.Box(r, GUIContent.none, box);
        float x = r.x + 12f * s, y = r.y + 10f * s, lw = 200f * s, cw = w - lw - 36f * s;

        GUI.Label(new Rect(x, y, w, row), Cursor.lockState == CursorLockMode.Locked
            ? "<b>HARDWARE TUNING</b>  (press Esc to free the cursor, then click)"
            : "<b>HARDWARE TUNING</b>", label);
        y += row;

        GUI.Label(new Rect(x, y, lw, row), "Input source", label);
        if (GUI.Button(new Rect(x + lw, y, cw / 2f - 4f, row - 6f), useHardware ? "● Hardware" : "Hardware", button)) SetUseHardware(true);
        if (GUI.Button(new Rect(x + lw + cw / 2f, y, cw / 2f - 4f, row - 6f), !useHardware ? "● Mouse" : "Mouse", button)) SetUseHardware(false);
        y += row;

        GUI.Label(new Rect(x, y, lw, row), "Packet meaning", label);
        string modeText = aim.mode switch
        {
            HardwareAimMode.Absolute => $"Absolute position ({aim.absoluteMin:0}-{aim.absoluteMax:0})",
            HardwareAimMode.PerPacketDelta => "Delta per packet (like mouse)",
            _ => "Rate per second (x deltaTime)",
        };
        if (GUI.Button(new Rect(x + lw, y, cw, row - 6f), modeText, button))
        {
            aim.mode = (HardwareAimMode)(((int)aim.mode + 1) % 3);
            aimController.Reset();
            SavePrefs();
        }
        y += row;

        GUI.changed = false;
        float logSens = Mathf.Log10(Mathf.Max(aim.sensitivity, 0.0001f));
        logSens = SliderRow(ref y, x, lw, cw, row, $"Sensitivity  {aim.sensitivity:0.0000}", logSens, -4f, 0.5f);
        aim.sensitivity = Mathf.Pow(10f, logSens);
        aim.deadZone = SliderRow(ref y, x, lw, cw, row, $"Dead zone  {aim.deadZone:0.0}", aim.deadZone, 0f, 10f);
        aim.smoothing = SliderRow(ref y, x, lw, cw, row, $"Smoothing  {aim.smoothing * 1000f:0} ms", aim.smoothing, 0f, 0.2f);
        aim.invertX = GUI.Toggle(new Rect(x, y, lw, row), aim.invertX, " Invert X");
        aim.invertY = GUI.Toggle(new Rect(x + lw, y, lw, row), aim.invertY, " Invert Y");
        y += row;

        GUI.Label(new Rect(x, y, lw, row), "Device id (0 = any)", label);
        if (int.TryParse(GUI.TextField(new Rect(x + lw, y, 80f * s, row - 8f), deviceId.ToString()), out int dev)) deviceId = Mathf.Max(0, dev);
        GUI.Label(new Rect(x + lw + 100f * s, y, lw, row), "Fire button", label);
        if (int.TryParse(GUI.TextField(new Rect(x + lw + 220f * s, y, 60f * s, row - 8f), fireButton.ToString()), out int fb)) fireButton = Mathf.Max(0, fb);
        y += row;
        bool changed = GUI.changed;

        GUI.Label(new Rect(x, y, lw, row), "Connection", label);
        if (GUI.Button(new Rect(x + lw, y, cw / 2f - 4f, row - 6f), source == HardwareSource.Serial ? "● Serial (USB)" : "Serial (USB)", button)) SetSource(HardwareSource.Serial);
        if (GUI.Button(new Rect(x + lw + cw / 2f, y, cw / 2f - 4f, row - 6f), source == HardwareSource.Udp ? "● UDP (Wi-Fi)" : "UDP (Wi-Fi)", button)) SetSource(HardwareSource.Udp);
        y += row;

        if (source == HardwareSource.Serial)
        {
            GUI.Label(new Rect(x, y, lw, row), "COM / baud", label);
            comField = GUI.TextField(new Rect(x + lw, y, 90f * s, row - 8f), comField);
            baudField = GUI.TextField(new Rect(x + lw + 96f * s, y, 90f * s, row - 8f), baudField);
            if (GUI.Button(new Rect(x + lw + 192f * s, y, cw - 192f * s, row - 6f), "Apply / reopen", button))
                ConfigureSerial(comField, int.TryParse(baudField, out int b) ? b : baudRate);
        }
        else
        {
            GUI.Label(new Rect(x, y, lw, row), "UDP port", label);
            portField = GUI.TextField(new Rect(x + lw, y, 100f * s, row - 8f), portField);
            if (GUI.Button(new Rect(x + lw + 110f * s, y, cw - 110f * s, row - 6f), "Apply / restart socket", button)
                && int.TryParse(portField, out int newPort) && newPort > 0 && newPort < 65536)
                RestartReceiver(newPort);
        }
        y += row;

        if (GUI.Button(new Rect(x, y, cw / 2f + lw / 2f, row - 6f), "Reset tuning to defaults", button)) ResetToDefaults();
        if (GUI.Button(new Rect(x + cw / 2f + lw / 2f + 8f, y, w - cw / 2f - lw / 2f - 32f * s, row - 6f), $"Close  [{panelKey}]", button)) panelOpen = false;

        if (changed) SavePrefs();
    }

    float SliderRow(ref float y, float x, float lw, float cw, float row, string text, float value, float min, float max)
    {
        GUI.Label(new Rect(x, y, lw, row), text, label);
        float v = GUI.HorizontalSlider(new Rect(x + lw, y + row * 0.35f, cw, row * 0.4f), value, min, max);
        y += row;
        return v;
    }

    void EnsureStyles(float s)
    {
        if (box == null)
        {
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.03f, 0.05f, 0.08f, 0.82f));
            bg.Apply();
            box = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
            label = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            label.normal.textColor = new Color(0.9f, 0.95f, 1f);
            button = new GUIStyle(GUI.skin.button);
        }
        label.fontSize = Mathf.Max(11, Mathf.RoundToInt(17f * s));
        button.fontSize = Mathf.Max(11, Mathf.RoundToInt(16f * s));
    }
}
