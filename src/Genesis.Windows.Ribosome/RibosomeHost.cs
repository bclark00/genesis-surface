using Genesis.Surface.Abstractions;
using Genesis.Surface.Projectors;
using System.Windows;

namespace Genesis.Windows.Ribosome;

// ── RibosomeHost ──────────────────────────────────────────────────────────────
// RFC: SURFACE-CHANNEL-001 §RibosomeHost
//
// Multi-window manager and named surface registry.
// Surfaces are opened by name; a projector dispatches to the surface channel.
//
// Threading: all methods must be called on the WPF dispatcher thread.
// Cross-thread projection: use Application.Current.Dispatcher.InvokeAsync(
//   async () => await host.ProjectAsync(spec)).
//
// Projectors registered by default:
//   WebProjector — Block IR → HTML → WebView2 DOM (PostWebMessageAsJson)
//
// Adding projectors: RegisterProjector(projector) before ProjectAsync.

public sealed class RibosomeHost
{
    private readonly Dictionary<string, RibosomeWindow> _windows     = new();
    private readonly Dictionary<string, ISurfaceProjector> _projectors = new();
    private ControlPlaneSurfaceWindow? _controlPlane;
    private readonly string _baseUrl;

    /// <param name="baseUrl">
    /// Base URL for surface pages (e.g. ms-appx-web:///webapps/genesis/status.html
    /// or http://localhost:5643/surface).
    /// Surface id is appended as ?surface={id}.
    /// </param>
    public RibosomeHost(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('?').TrimEnd('/');

        // Default projectors
        RegisterProjector(new WebProjector());
        RegisterProjector(new CehProjector());
    }

    // ── Surface management ────────────────────────────────────────────────────

    /// <summary>Opens or focuses a named surface window.</summary>
    public RibosomeWindow OpenSurface(string surfaceId)
    {
        if (_windows.TryGetValue(surfaceId, out var existing))
        {
            existing.Activate();
            return existing;
        }

        var url    = $"{_baseUrl}?surface={Uri.EscapeDataString(surfaceId)}";
        var window = new RibosomeWindow(surfaceId, url);
        window.Closed += (_, _) => _windows.Remove(surfaceId);
        _windows[surfaceId] = window;
        window.Show();
        return window;
    }

    /// <summary>Gets the channel for an open surface, or null if not open.</summary>
    public ISurfaceChannel? GetChannel(string surfaceId)
        => _windows.TryGetValue(surfaceId, out var w) ? w.Channel : null;

    /// <summary>Closes a named surface window.</summary>
    public void CloseSurface(string surfaceId)
    {
        if (_windows.TryGetValue(surfaceId, out var w))
            w.Close();
    }

    public IReadOnlyList<string> OpenSurfaceIds => [.._windows.Keys];

    /// <summary>
    /// Opens the native WPF control-plane materializer and projects the same
    /// canonical Surface IR used by other surface implementations.
    /// </summary>
    public ControlPlaneSurfaceWindow OpenControlPlane(SurfaceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (_controlPlane is null)
        {
            _controlPlane = new ControlPlaneSurfaceWindow();
            _controlPlane.Closed += (_, _) => _controlPlane = null;
            _controlPlane.Show();
        }
        _controlPlane.Project(spec);
        _controlPlane.Activate();
        return _controlPlane;
    }

    public void CloseControlPlane() => _controlPlane?.Close();

    // ── Projection ────────────────────────────────────────────────────────────

    /// <summary>
    /// Register an ISurfaceProjector. Registration by TargetKind.
    /// Replaces any existing projector for the same TargetKind.
    /// </summary>
    public void RegisterProjector(ISurfaceProjector projector)
        => _projectors[projector.TargetKind] = projector;

    /// <summary>
    /// Project a SurfaceSpec to the named surface.
    /// Opens the surface window if not already open.
    /// Waits for the channel to be ready if the window was just opened.
    /// </summary>
    public async Task<ProjectionReceipt> ProjectAsync(
        SurfaceSpec spec,
        string      projectorKind = "web",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!_projectors.TryGetValue(projectorKind, out var projector))
            throw new InvalidOperationException(
                $"No projector registered for kind '{projectorKind}'. " +
                $"Available: {string.Join(", ", _projectors.Keys)}");

        var window = OpenSurface(spec.TargetSurfaceId);

        // If channel isn't ready yet (window just opened), wait for it.
        ISurfaceChannel? channel = window.Channel;
        if (channel is null)
        {
            var tcs = new TaskCompletionSource<ISurfaceChannel>();
            window.ChannelReady += (_, _) =>
            {
                if (window.Channel is not null)
                    tcs.TrySetResult(window.Channel);
            };
            using var reg = ct.Register(() =>
                tcs.TrySetException(new OperationCanceledException(ct)));
            channel = await tcs.Task.ConfigureAwait(false);
        }

        return await projector.ProjectAsync(spec, channel, ct).ConfigureAwait(false);
    }
}
