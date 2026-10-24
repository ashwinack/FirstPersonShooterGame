using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>Editor tools for the hardware gun input: a self-test and a fake-gun packet sender.</summary>
public static class HardwareInputTools
{
    const int TestPort = 19900;

    [MenuItem("FPS/Hardware Input/Run Self Test")]
    public static void SelfTest()
    {
        int pass = 0, fail = 0;
        void Check(bool ok, string what)
        {
            if (ok) pass++;
            else
            {
                fail++;
                Debug.LogError("[HW TEST] FAIL: " + what);
            }
        }

        var parts0 = new System.Collections.Generic.List<string>();

        // ---------- Parser: formats from the Protokol Bridge screenshots plus the original spec ----------
        Check(HardwarePacketParser.TryParse("1(12,-6)", out var p) && p.Kind == HardwarePacketKind.Aim && p.Id == 1 && p.X == 12 && p.Y == -6, "1(12,-6)");
        Check(HardwarePacketParser.TryParse("1(0,-1)\r\n", out p) && p.X == 0 && p.Y == -1, "1(0,-1) with CRLF");
        Check(HardwarePacketParser.TryParse("1( 18 , -7 )", out p) && p.X == 18 && p.Y == -7, "spaces inside");
        Check(HardwarePacketParser.TryParse("[12, -3]", out p) && p.Id == 0 && p.X == 12 && p.Y == -3, "[12, -3]");
        Check(HardwarePacketParser.TryParse("12,-3", out p) && p.X == 12 && p.Y == -3, "bare 12,-3");
        Check(HardwarePacketParser.TryParse("2(1.5,-0.25)", out p) && p.Id == 2 && Mathf.Approximately(p.X, 1.5f) && Mathf.Approximately(p.Y, -0.25f), "decimals + id 2");
        Check(HardwarePacketParser.TryParse("P1", out p) && p.Kind == HardwarePacketKind.ButtonDown && p.Id == 1, "P1");
        Check(HardwarePacketParser.TryParse("R1", out p) && p.Kind == HardwarePacketKind.ButtonUp && p.Id == 1, "R1");
        Check(HardwarePacketParser.TryParse("p2\0", out p) && p.Kind == HardwarePacketKind.ButtonDown && p.Id == 2, "p2 with null byte");
        // Serial (Protokol Bridge COM3) format
        Check(HardwarePacketParser.TryParse("X = 0 Y = 8", out p) && p.Kind == HardwarePacketKind.Aim && p.Id == 0 && p.X == 0 && p.Y == 8, "X = 0 Y = 8");
        Check(HardwarePacketParser.TryParse("X = -1 Y = 8\r", out p) && p.X == -1 && p.Y == 8, "X = -1 Y = 8 with CR");
        Check(HardwarePacketParser.TryParse("X=0,Y=8", out p) && p.X == 0 && p.Y == 8, "X=0,Y=8");
        Check(HardwarePacketParser.TryParse("x: 1 y: -2", out p) && p.X == 1 && p.Y == -2, "x: 1 y: -2");
        HardwarePacketParser.Split("X = -1 Y = 8", parts0);
        Check(parts0.Count == 1 && parts0[0] == "X = -1 Y = 8", "serial line passes through Split");
        foreach (var bad in new[] { "X = Y = 8", "X = 1", "X = a Y = b", "X = 1 Z = 2" })
            Check(!HardwarePacketParser.TryParse(bad, out _), $"rejects '{bad}'");
        foreach (var bad in new[] { "", "   ", "abc", "1(12)", "1(12,-6,3)", "1(x,y)", "Px", "1(,)", "x(1,2)", "1(1e999,2)", "(NaN,1)" })
            Check(!HardwarePacketParser.TryParse(bad, out _), $"rejects '{bad}'");

        // ---------- Splitting back-to-back messages (as seen in Hercules) ----------
        var parts = new System.Collections.Generic.List<string>();
        HardwarePacketParser.Split("1(1,1)1(0,1)1(0,3)1(-4,-2)", parts);
        Check(parts.Count == 4 && parts[0] == "1(1,1)" && parts[3] == "1(-4,-2)", $"splits run-together aim ({string.Join(" | ", parts)})");
        HardwarePacketParser.Split("1(2,-3)P11(0,1)R1", parts);
        Check(parts.Count == 4 && parts[1] == "P1" && parts[3] == "R1", $"splits aim + buttons ({string.Join(" | ", parts)})");
        HardwarePacketParser.Split("1(12,-6)", parts);
        Check(parts.Count == 1 && parts[0] == "1(12,-6)", "single message passes through");
        HardwarePacketParser.Split("12,-3", parts);
        Check(parts.Count == 1 && parts[0] == "12,-3", "bare x,y passes through");
        int ok = 0;
        HardwarePacketParser.Split("1(-1,-3)1(-1,-3)1(0,-3)1(-1,-2)1(-2,-2)", parts);
        foreach (var part in parts) if (HardwarePacketParser.TryParse(part, out _)) ok++;
        Check(ok == 5, $"every split message parses ({ok}/5)");

        // ---------- Aim controller ----------
        var settings = new HardwareAimSettings { mode = HardwareAimMode.PerPacketDelta, sensitivity = 0.1f, deadZone = 1f, smoothing = 0f };
        var aim = new HardwareAimController(settings);
        aim.Add(12, -6);
        aim.Add(13, -6);
        Vector2 d = aim.Consume(1f / 60f);
        Check(Mathf.Approximately(d.x, (11 + 12) * 0.1f) && Mathf.Approximately(d.y, (-5 - 5) * 0.1f), $"delta mode sums with dead zone (got {d})");
        Check(aim.Consume(1f / 60f) == Vector2.zero, "consume clears");
        aim.Add(1, -1);
        Check(aim.Consume(1f / 60f) == Vector2.zero, "dead zone swallows +-1 noise");
        settings.invertX = true;
        settings.invertY = true;
        aim.Add(5, 5);
        d = aim.Consume(1f / 60f);
        Check(d.x < 0 && d.y < 0, "invert X/Y");
        settings.invertX = settings.invertY = false;

        // Frame-rate independence: same packets over 1 second at 30 fps vs 144 fps.
        float Total(int fps, HardwareAimMode mode)
        {
            var s = new HardwareAimSettings { mode = mode, sensitivity = 0.05f, deadZone = 0f, smoothing = 0.03f };
            var c = new HardwareAimController(s);
            float total = 0f, dt = 1f / fps;
            int packets = 200, sent = 0; // 200 Hz gun
            for (int f = 0; f < fps + 30; f++) // extra frames let smoothing settle
            {
                int due = Mathf.Min(packets, Mathf.RoundToInt((f + 1) * dt * 200f));
                for (; sent < due; sent++) c.Add(12, 0);
                total += c.Consume(dt).x;
            }
            return total;
        }
        float a30 = Total(30, HardwareAimMode.PerPacketDelta), a144 = Total(144, HardwareAimMode.PerPacketDelta);
        Check(Mathf.Abs(a30 - a144) < 0.05f * Mathf.Abs(a30), $"delta mode frame-rate independent ({a30:0.00} vs {a144:0.00} deg)");
        float r30 = Total(30, HardwareAimMode.RatePerSecond), r144 = Total(144, HardwareAimMode.RatePerSecond);
        Check(Mathf.Abs(r30 - r144) < 0.05f * Mathf.Abs(r30), $"rate mode frame-rate independent ({r30:0.00} vs {r144:0.00} deg)");

        // ---------- Protocol v2: absolute 0-100 aim, S1 switch weapon, B1 reload ----------
        Check(HardwarePacketParser.TryParse("1(100,39)", out p) && p.Kind == HardwarePacketKind.Aim && p.X == 100 && p.Y == 39, "1(100,39)");
        Check(HardwarePacketParser.TryParse("1( 0,42)", out p) && p.X == 0 && p.Y == 42, "1( 0,42) with space");
        Check(HardwarePacketParser.TryParse("S1", out p) && p.Kind == HardwarePacketKind.SwitchWeapon && p.Id == 1, "S1 = switch weapon");
        Check(HardwarePacketParser.TryParse("B1\r\n", out p) && p.Kind == HardwarePacketKind.Reload && p.Id == 1, "B1 = reload");
        Check(!HardwarePacketParser.TryParse("A1", out _), "A1 no longer used (rejected)");
        Check(!HardwarePacketParser.TryParse("Sx", out _) && !HardwarePacketParser.TryParse("abc", out _), "rejects 'Sx' / 'abc'");
        HardwarePacketParser.Split("1(50,50)S1B11(60,40)P1", parts);
        Check(parts.Count == 5 && parts[1] == "S1" && parts[2] == "B1" && parts[3] == "1(60,40)", $"splits aim + S1/B1 ({string.Join(" | ", parts)})");

        var abs = new HardwareAimController(new HardwareAimSettings { mode = HardwareAimMode.Absolute, smoothing = 0f });
        abs.SetAbsolute(50, 50);
        Check(abs.UpdateAbsolute(1f / 60f) == Vector2.zero, "absolute 50,50 = straight ahead");
        abs.SetAbsolute(100, 100);
        Check(abs.UpdateAbsolute(1f / 60f) == Vector2.one, "absolute 100,100 = right/up limit");
        abs.SetAbsolute(0, 0);
        Check(abs.UpdateAbsolute(1f / 60f) == -Vector2.one, "absolute 0,0 = left/down limit");
        abs.SetAbsolute(140, -20);
        Check(abs.UpdateAbsolute(1f / 60f) == new Vector2(1f, -1f), "absolute clamps out-of-range values");
        abs.Reset();
        Check(abs.HasAbsolute && abs.UpdateAbsolute(1f / 60f) == new Vector2(1f, -1f), "Reset keeps the absolute position");

        // ---------- UDP receiver: real loopback through the background thread ----------
        var rx = new UdpReceiver();
        bool started = rx.Start(TestPort);
        Check(started, $"receiver starts on {TestPort} ({rx.LastError})");
        if (started)
        {
            using (var tx = new UdpClient())
            {
                foreach (var msg in new[] { "1(12,-6)", "P1", "R1" })
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(msg);
                    tx.Send(bytes, bytes.Length, "127.0.0.1", TestPort);
                }
            }
            var got = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 100 && got.Count < 3; i++)
            {
                while (rx.TryDequeue(out string m)) got.Add(m);
                Thread.Sleep(10);
            }
            Check(got.Count == 3 && got[0] == "1(12,-6)" && got[1] == "P1" && got[2] == "R1", $"loopback received [{string.Join(", ", got)}]");
            rx.Stop();
            Check(!rx.IsRunning, "receiver stops");
            // Port must be reusable immediately after Stop (play mode restarts).
            Check(rx.Start(TestPort), "port reusable after stop");
            rx.Stop();
        }

        string summary = $"[HW TEST] {pass} passed, {fail} failed";
        if (fail == 0) Debug.Log(summary + " - all good");
        else Debug.LogError(summary);
    }

    /// <summary>
    /// Pretends to be the gun: sends a 1 s rightward sweep at 200 Hz to the port the game listens on
    /// (8888 by default) with a trigger pull in the middle. Background thread, so it works while playing.
    /// </summary>
    [MenuItem("FPS/Hardware Input/Send Test Sweep To Game Port")]
    public static void SendTestSweep()
    {
        int port = HardwareInput.Instance != null ? HardwareInput.Instance.Port : PlayerPrefs.GetInt("HW_Port", 8888);
        var t = new Thread(() =>
        {
            using var tx = new UdpClient();
            void Send(string msg)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(msg);
                tx.Send(bytes, bytes.Length, "127.0.0.1", port);
            }
            for (int i = 0; i < 200; i++)
            {
                Send("1(12,-6)");
                if (i == 80) Send("P1");
                if (i == 120) Send("R1");
                Thread.Sleep(5);
            }
        }) { IsBackground = true, Name = "HW test sweep" };
        t.Start();
        Debug.Log($"[HW TEST] Sending 200 aim packets (1(12,-6)) + P1/R1 to 127.0.0.1:{port} over ~1 s");
    }
}
