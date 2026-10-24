using UnityEngine;

/// <summary>Shared IMGUI settings controls (pause menu and main menu).</summary>
public static class SettingsPanel
{
    static string portText, portMessage, comText, baudText, shownCom;
    static int shownPort = -1;

    public static void Draw(Rect area, GUIStyle label, GUIStyle button, float scale, bool showDifficulty, bool showFixedPosition = false)
    {
        float row = 56f * scale, labelW = area.width * 0.42f, sliderW = area.width * 0.42f, valueW = area.width * 0.14f;
        var right = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
        var left = new GUIStyle(label) { alignment = TextAnchor.MiddleLeft };
        float y = area.y;

        GameSettings.Sensitivity = Slider(area.x, ref y, "MOUSE SENSITIVITY", GameSettings.Sensitivity, 0.02f, 0.4f, $"{GameSettings.Sensitivity * 10f:0.0}");
        GameSettings.Fov = Mathf.Round(Slider(area.x, ref y, "FIELD OF VIEW", GameSettings.Fov, 60f, 100f, $"{GameSettings.Fov:0}"));
        GameSettings.Volume = Slider(area.x, ref y, "VOLUME", GameSettings.Volume, 0f, 1f, $"{GameSettings.Volume * 100f:0}%");

        if (showDifficulty)
        {
            Label(new Rect(area.x, y, labelW, row), "DIFFICULTY", right);
            float bw = (sliderW + valueW) / 3f - 6f * scale;
            for (int i = 0; i < 3; i++)
            {
                var d = (Difficulty)i;
                var r = new Rect(area.x + labelW + 20f * scale + i * (bw + 6f * scale), y + 8f * scale, bw, row - 16f * scale);
                var prev = GUI.color;
                if (GameSettings.Difficulty == d) GUI.color = new Color(1f, 0.8f, 0.35f);
                if (MenuCrosshair.Button(r, d.ToString().ToUpper(), button))
                {
                    GameSettings.Difficulty = d;
                    ProceduralAudio.Play2D(ProceduralAudio.UIClick, 0.6f, 0f);
                }
                GUI.color = prev;
            }
            y += row;
        }

        if (showFixedPosition)
        {
            Label(new Rect(area.x, y, labelW, row), "FIXED POSITION", right);
            bool on = GameSettings.FixedPosition;
            var r = new Rect(area.x + labelW + 20f * scale, y + 8f * scale, 150f * scale, row - 16f * scale);
            var prev = GUI.color;
            GUI.color = on ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f);
            if (MenuCrosshair.Button(r, on ? "ON" : "OFF", button))
            {
                GameSettings.FixedPosition = !on;
                ProceduralAudio.Play2D(ProceduralAudio.UIClick, 0.6f, 0f);
            }
            GUI.color = prev;
            Label(new Rect(r.xMax + 14f * scale, y, valueW + sliderW * 0.5f, row), on ? "look & shoot only" : "free movement", left);
            y += row;
        }

        var hw = HardwareInput.Instance;
        if (hw != null)
        {
            float fx = area.x + labelW + 20f * scale;
            var rich = new GUIStyle(left) { richText = true, wordWrap = true, fontSize = Mathf.RoundToInt(left.fontSize * 0.8f) };
            bool serial = hw.Source == HardwareSource.Serial;

            Label(new Rect(area.x, y, labelW, row), "GUN INPUT", right);
            for (int i = 0; i < 2; i++)
            {
                var src = i == 0 ? HardwareSource.Serial : HardwareSource.Udp;
                var r = new Rect(fx + i * 160f * scale, y + 8f * scale, 150f * scale, row - 16f * scale);
                var prev = GUI.color;
                if (hw.Source == src) GUI.color = new Color(1f, 0.8f, 0.35f);
                if (MenuCrosshair.Button(r, i == 0 ? "SERIAL (USB)" : "UDP (WI-FI)", button))
                {
                    ProceduralAudio.Play2D(ProceduralAudio.UIClick, 0.6f, 0f);
                    hw.SetSource(src);
                    portMessage = null;
                }
                GUI.color = prev;
            }
            y += row;

            if (serial)
            {
                Label(new Rect(area.x, y, labelW, row), "COM PORT / BAUD", right);
                if (comText == null || shownCom != hw.SerialPortName + hw.BaudRate)
                {
                    comText = hw.SerialPortName;
                    baudText = hw.BaudRate.ToString();
                    shownCom = hw.SerialPortName + hw.BaudRate;
                }
                string before = comText + baudText;
                comText = GUI.TextField(new Rect(fx, y + 10f * scale, 90f * scale, row - 20f * scale), comText, 8);
                baudText = GUI.TextField(new Rect(fx + 96f * scale, y + 10f * scale, 100f * scale, row - 20f * scale), baudText, 7);
                if (comText + baudText != before) portMessage = null;
                if (MenuCrosshair.Button(new Rect(fx + 204f * scale, y + 8f * scale, 100f * scale, row - 16f * scale), "APPLY", button))
                {
                    ProceduralAudio.Play2D(ProceduralAudio.UIClick, 0.6f, 0f);
                    if (int.TryParse(baudText, out int b) && b > 0 && !string.IsNullOrWhiteSpace(comText))
                    {
                        hw.ConfigureSerial(comText, b); // opens in the background; status below shows the result
                        portMessage = null;
                    }
                    else portMessage = "e.g. COM3 / 115200";
                    shownCom = null;
                }
                string ports = string.Join(" ", SerialReceiver.AvailablePorts());
                string aimBy = hw.UseHardware ? "gun aims (F1 = mouse)" : "mouse aims (F1 = gun)";
                string status = portMessage ?? (hw.Listening
                    ? $"<color=#7CFF8A>{hw.Endpoint} open, {hw.ReceivedCount} lines</color>  {aimBy}"
                    : $"<color=#FF7070>{hw.LastError ?? "opening..."}</color>  ports: {(ports.Length > 0 ? ports : "none")}");
                y += row;
                Label(new Rect(fx, y, area.xMax - fx, row), status, rich);
            }
            else
            {
                Label(new Rect(area.x, y, labelW, row), "GUN UDP PORT", right);
                if (portText == null || shownPort != hw.Port)
                {
                    portText = hw.Port.ToString();
                    shownPort = hw.Port;
                }
                string before = portText;
                portText = GUI.TextField(new Rect(fx, y + 10f * scale, 110f * scale, row - 20f * scale), portText, 5);
                if (portText != before) portMessage = null;
                if (MenuCrosshair.Button(new Rect(fx + 120f * scale, y + 8f * scale, 110f * scale, row - 16f * scale), "APPLY", button))
                {
                    ProceduralAudio.Play2D(ProceduralAudio.UIClick, 0.6f, 0f);
                    if (int.TryParse(portText, out int p) && p >= 1 && p <= 65535)
                    {
                        hw.RestartReceiver(p); // on failure the status below shows why (e.g. port in use by the bridge)
                        portMessage = null;
                    }
                    else portMessage = "1-65535 only";
                    shownPort = hw.Port;
                }
                string status = portMessage ?? (hw.Listening
                    ? $"<color=#7CFF8A>listening on {hw.Port}</color>"
                    : $"<color=#FF7070>{hw.LastError ?? "not listening"}</color>");
                y += row;
                Label(new Rect(fx, y, area.xMax - fx, row), status, rich);
            }
            y += row;
        }

        float Slider(float x, ref float yy, string name, float value, float min, float max, string display)
        {
            Label(new Rect(x, yy, labelW, row), name, right);
            float v = GUI.HorizontalSlider(new Rect(x + labelW + 20f * scale, yy + row * 0.42f, sliderW - 20f * scale, row * 0.3f), value, min, max);
            Label(new Rect(x + labelW + sliderW + 10f * scale, yy, valueW, row), display, left);
            yy += row;
            return v;
        }
    }

    static void Label(Rect r, string text, GUIStyle style)
    {
        var shadow = new Rect(r.x + 2, r.y + 2, r.width, r.height);
        var c = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(shadow, text, style);
        style.normal.textColor = Color.white;
        GUI.Label(r, text, style);
        style.normal.textColor = c;
    }
}
