using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

/// <summary>
/// Reads newline-terminated messages from a COM port (e.g. an ESP32 over USB) on a background thread
/// and hands them to the main thread through a thread-safe queue. Reconnects by itself if the cable
/// is unplugged or the port is busy. Plain C# (no Unity API), so it is safe off the main thread.
/// Requires the .NET Framework API compatibility level (System.IO.Ports).
/// </summary>
public sealed class SerialReceiver : IHardwareReceiver
{
    const int MaxQueued = 4096;
    const int ReconnectDelayMs = 1000;

    readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
    SerialPort port;
    Thread thread;
    volatile bool running, open;
    long received, dropped;

    public string PortName { get; private set; } = "COM3";
    public int BaudRate { get; private set; } = 115200;
    /// <summary>Some boards (ESP32 native USB CDC) only send while DTR is asserted.</summary>
    public bool DtrEnable { get; private set; } = true;

    public bool IsRunning => running && open;
    public long Received => Interlocked.Read(ref received);
    public long Dropped => Interlocked.Read(ref dropped);
    public int Queued => queue.Count;
    public string LastError { get; private set; }
    public string Endpoint => $"{PortName} @ {BaudRate}";

    public void Start(string portName, int baudRate, bool dtrEnable = true)
    {
        Stop();
        PortName = string.IsNullOrWhiteSpace(portName) ? "COM3" : portName.Trim().ToUpperInvariant();
        BaudRate = baudRate > 0 ? baudRate : 115200;
        DtrEnable = dtrEnable;
        LastError = null;
        running = true;
        thread = new Thread(ReadLoop) { IsBackground = true, Name = $"SerialReceiver:{PortName}" };
        thread.Start();
    }

    void ReadLoop()
    {
        var line = new StringBuilder(64);
        var buffer = new byte[512];
        while (running)
        {
            if (!open && !TryOpen())
            {
                Sleep(ReconnectDelayMs);
                continue;
            }

            try
            {
                // Read whatever is available and split on '\n' ourselves (more robust than ReadLine on Mono).
                int n = port.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < n; i++)
                {
                    char c = (char)buffer[i];
                    if (c == '\n')
                    {
                        Enqueue(line);
                        line.Clear();
                    }
                    else if (c != '\r')
                    {
                        line.Append(c);
                        if (line.Length > 256) line.Clear(); // garbage without newlines: drop it
                    }
                }
            }
            catch (TimeoutException)
            {
                // No data within ReadTimeout; just loop.
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                if (!running) break;
                LastError = $"{PortName} disconnected: {e.Message}";
                ClosePort();
                Sleep(ReconnectDelayMs);
            }
            catch (Exception e)
            {
                if (!running) break;
                LastError = $"{PortName} read error: {e.Message}";
                Sleep(50);
            }
        }
        ClosePort();
    }

    bool TryOpen()
    {
        try
        {
            port = new SerialPort(PortName, BaudRate, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = DtrEnable,
                RtsEnable = false,
                ReadTimeout = 100,
                Encoding = Encoding.ASCII,
            };
            port.Open();
            port.DiscardInBuffer();
            open = true;
            LastError = null;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            LastError = $"{PortName} is in use by another program (close Protokol Bridge / Hercules / Serial Monitor)";
        }
        catch (IOException e)
        {
            LastError = $"{PortName} not available: {e.Message}";
        }
        catch (Exception e)
        {
            LastError = $"Could not open {PortName}: {e.Message}";
        }
        ClosePort();
        return false;
    }

    void Enqueue(StringBuilder line)
    {
        if (line.Length == 0) return;
        queue.Enqueue(line.ToString());
        Interlocked.Increment(ref received);
        while (queue.Count > MaxQueued && queue.TryDequeue(out _)) Interlocked.Increment(ref dropped);
    }

    void Sleep(int ms)
    {
        // Sleep in small slices so Stop() is responsive.
        for (int t = 0; t < ms && running; t += 50) Thread.Sleep(50);
    }

    void ClosePort()
    {
        open = false;
        try { port?.Close(); } catch { /* already closed */ }
        try { port?.Dispose(); } catch { }
        port = null;
    }

    public bool TryDequeue(out string message) => queue.TryDequeue(out message);

    public void Stop()
    {
        running = false;
        var t = thread;
        thread = null;
        try { port?.Close(); } catch { } // unblocks a pending Read
        if (t != null && t.IsAlive && !t.Join(500)) LastError = "Serial thread did not stop in time";
        ClosePort();
        while (queue.TryDequeue(out _)) { }
    }

    public void Dispose() => Stop();

    public static string[] AvailablePorts()
    {
        try { return SerialPort.GetPortNames(); }
        catch { return new string[0]; }
    }
}
