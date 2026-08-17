using Genesis.Surface.Abstractions;

namespace Genesis.Windows.Ribosome;

/// <summary>
/// Runtime channel for a native WPF projection. Runtime messages provide
/// lifecycle/receipt semantics; the WPF projector owns the visual tree.
/// </summary>
public sealed class WpfSurfaceChannel : ISurfaceChannel, IDisposable
{
    private bool _disposed;
    private SurfaceSpec? _lastSpec;

    public WpfSurfaceChannel(string surfaceId) => SurfaceId = surfaceId;

    public string SurfaceId { get; }
    public event EventHandler<SurfaceMessage>? MessageReceived;

    public void SetProjectedSpec(SurfaceSpec spec)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _lastSpec = spec;
    }

    public Task SendAsync(SurfaceMessage message, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();
        MessageReceived?.Invoke(this, message);
        return Task.CompletedTask;
    }

    public void Dispose() => _disposed = true;
}
