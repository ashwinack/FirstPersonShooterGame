using System;

/// <summary>
/// A background message source for the hardware gun (UDP or serial). Messages are queued on a
/// worker thread and drained on the main thread with TryDequeue.
/// </summary>
public interface IHardwareReceiver : IDisposable
{
    /// <summary>True while the socket is listening / the COM port is open.</summary>
    bool IsRunning { get; }
    long Received { get; }
    long Dropped { get; }
    int Queued { get; }
    /// <summary>Last error, or null. For display only.</summary>
    string LastError { get; }
    /// <summary>Human-readable source, e.g. "UDP :8888" or "COM3 @ 115200".</summary>
    string Endpoint { get; }
    bool TryDequeue(out string message);
    void Stop();
}
