using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>Aim = "1(x,y)"; ButtonDown/Up = trigger "P1"/"R1"; SwitchWeapon = "S1"; Reload = "B1".</summary>
public enum HardwarePacketKind { Aim, ButtonDown, ButtonUp, SwitchWeapon, Reload }

/// <summary>One parsed message from the gun.</summary>
public readonly struct HardwarePacket
{
    public readonly HardwarePacketKind Kind;
    /// <summary>Aim: the device/player id before the bracket (0 if none). Buttons: the number after P/R.</summary>
    public readonly int Id;
    public readonly float X, Y;

    public HardwarePacket(HardwarePacketKind kind, int id, float x, float y)
    {
        Kind = kind;
        Id = id;
        X = x;
        Y = y;
    }

    public override string ToString() => Kind == HardwarePacketKind.Aim
        ? $"Aim id={Id} x={X.ToString(CultureInfo.InvariantCulture)} y={Y.ToString(CultureInfo.InvariantCulture)}"
        : $"{Kind} {Id}";
}

/// <summary>
/// Parses gun messages. Never throws; malformed input just returns false.
/// Accepted:
///   aim:    "1(12,-6)"   "1( 12 , -6 )"   "(12,-6)"   "[12, -3]"   "12,-3"   "X = 0 Y = 8"   (decimals allowed)
///   button: "P1" = pressed, "R1" = released (any number; case-insensitive)
///   action: "S1" = switch weapon, "B1" = reload (one-shot events)
/// </summary>
public static class HardwarePacketParser
{
    static readonly char[] Open = { '(', '[' };
    static readonly char[] Close = { ')', ']' };
    static readonly char[] Separators = { ',', ';', ' ', '\t' };
    static readonly char[] TrimChars = { ' ', '\t', '\r', '\n', '\0' };

    static readonly Regex LabelledXY = new Regex(
        @"^[Xx]\s*[=:]\s*([-+]?\d+(?:\.\d+)?)\s*[,;]?\s*[Yy]\s*[=:]\s*([-+]?\d+(?:\.\d+)?)$", RegexOptions.Compiled);

    // One message: "1(x,y)" / "(x,y)" / "[x,y]", or "P1" / "R1".
    // The button number is lazy and stops where the next "id(" or button begins, so "P11(0,1)" = "P1" + "1(0,1)".
    static readonly Regex MessagePattern = new Regex(
        @"\d*\s*[\(\[][^\)\]\(\[]*[\)\]]|[PpRrSsBb]\s*\d+?(?=\d*\s*[\(\[]|[PpRrSsBb]|\s|$)", RegexOptions.Compiled);

    /// <summary>
    /// Splits a datagram into messages. Handles back-to-back messages with no separator
    /// ("1(1,1)1(0,1)P1"); anything else (one message, bare "x,y", junk) is passed through whole
    /// so TryParse can judge it.
    /// </summary>
    public static void Split(string datagram, List<string> output)
    {
        output.Clear();
        if (string.IsNullOrEmpty(datagram)) return;
        var matches = MessagePattern.Matches(datagram);
        if (matches.Count <= 1)
        {
            output.Add(datagram);
            return;
        }
        foreach (Match m in matches) output.Add(m.Value);
    }

    public static bool TryParse(string raw, out HardwarePacket packet)
    {
        packet = default;
        if (string.IsNullOrEmpty(raw)) return false;
        string s = raw.Trim(TrimChars);
        if (s.Length == 0) return false;

        char c = s[0];
        HardwarePacketKind? button = char.ToUpperInvariant(c) switch
        {
            'P' => HardwarePacketKind.ButtonDown,
            'R' => HardwarePacketKind.ButtonUp,
            'S' => HardwarePacketKind.SwitchWeapon,
            'B' => HardwarePacketKind.Reload,
            _ => (HardwarePacketKind?)null,
        };
        if (button.HasValue)
        {
            int number = 0;
            if (s.Length > 1 && !int.TryParse(s.Substring(1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return false;
            packet = new HardwarePacket(button.Value, number, 0f, 0f);
            return true;
        }

        // Serial format: "X = 0 Y = 8" (also "X=0,Y=8", "x: 1 y: -2").
        if (c == 'X' || c == 'x')
        {
            var m = LabelledXY.Match(s);
            if (!m.Success) return false;
            if (!float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float lx)) return false;
            if (!float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float ly)) return false;
            packet = new HardwarePacket(HardwarePacketKind.Aim, 0, lx, ly);
            return true;
        }

        int id = 0;
        string body = s;
        int open = s.IndexOfAny(Open);
        if (open >= 0)
        {
            string prefix = s.Substring(0, open).Trim();
            if (prefix.Length > 0 && !int.TryParse(prefix, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                return false;
            int close = s.IndexOfAny(Close, open + 1);
            body = close > open ? s.Substring(open + 1, close - open - 1) : s.Substring(open + 1);
        }

        string[] parts = body.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return false;

        packet = new HardwarePacket(HardwarePacketKind.Aim, id, x, y);
        return true;
    }
}
