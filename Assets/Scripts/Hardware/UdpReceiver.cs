using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// Listens for UDP datagrams on a background thread and hands them to the main thread
/// through a thread-safe queue. Plain C# (no Unity API) so it is safe off the main thread.
/// </summary>
public sealed class UdpReceiver : IHardwareReceiver
{
    /// <summary>Oldest messages are dropped past this, so a stalled main thread can't grow memory forever.</summary>
    const int MaxQueued = 4096;

    readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
    UdpClient client;
    Thread thread;
    volatile bool running;
    long received, dropped;

    public int Port { get; private set; }
    public string Endpoint => $"UDP :{Port}";
    public bool IsRunning => running;
    public long Received => Interlocked.Read(ref received);
    public long Dropped => Interlocked.Read(ref dropped);
    public int Queued => queue.Count;
    /// <summary>Last start/receive error (null if none). Written by either thread; read for display only.</summary>
    public string LastError { get; private set; }

    public bool Start(int port)
    {
        Stop();
        Port = port;
        LastError = null;
        try
        {
            // Exclusive bind: if another program (e.g. Protokol Bridge) already listens here we fail loudly
            // instead of silently sharing the port and losing packets to it.
            client = new UdpClient(AddressFamily.InterNetwork) { ExclusiveAddressUse = true };
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse || e.SocketErrorCode == SocketError.AccessDenied)
        {
            LastError = $"Port {port} is in use by another program (close Protokol Bridge or stop its connection)";
            client?.Close();
            client = null;
            return false;
        }
        catch (Exception e)
        {
            LastError = $"Could not listen on UDP {port}: {e.Message}";
            client?.Close();
            client = null;
            return false;
        }

        running = true;
        thread = new Thread(ReceiveLoop) { IsBackground = true, Name = $"UdpReceiver:{port}" };
        thread.Start();
        return true;
    }

    void ReceiveLoop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = client.Receive(ref remote);
                if (data == null || data.Length == 0) continue;
                queue.Enqueue(Encoding.ASCII.GetString(data));
                Interlocked.Increment(ref received);
                while (queue.Count > MaxQueued && queue.TryDequeue(out _)) Interlocked.Increment(ref dropped);
            }
            catch (ObjectDisposedException)
            {
                break; // socket closed by Stop()
            }
            catch (SocketException e)
            {
                if (!running) break; // Close() interrupts the blocking Receive with an exception
                LastError = $"Receive error: {e.SocketErrorCode}";
                Thread.Sleep(5);
            }
            catch (Exception e)
            {
                if (!running) break;
                LastError = $"Receive error: {e.Message}";
                Thread.Sleep(5);
            }
        }
    }

    public bool TryDequeue(out string message) => queue.TryDequeue(out message);

    public void Stop()
    {
        running = false;
        try { client?.Close(); } catch { /* already closed */ }
        client = null;
        if (thread != null && thread.IsAlive && !thread.Join(250))
            LastError = "Receive thread did not stop in time";
        thread = null;
        while (queue.TryDequeue(out _)) { }
    }

    public void Dispose() => Stop();
}
