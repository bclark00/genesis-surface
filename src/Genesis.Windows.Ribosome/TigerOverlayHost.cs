using System.Windows;
using Genesis.Surface.Runtime;

namespace Genesis.Windows.Ribosome;

/// <summary>Owns Tiger overlay lifetime without introducing a polling loop.</summary>
public sealed class TigerOverlayHost
{
    private readonly TigerStateHub _hub;
    private readonly SurfaceRuntime _runtime;
    private TigerOverlayWindow? _window;

    public TigerOverlayHost(TigerStateHub hub, SurfaceRuntime? runtime = null)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _runtime = runtime ?? new SurfaceRuntime();
    }

    public TigerOverlayWindow Attach(string surfaceId = "tiger-ambient")
    {
        if (_window is { } existing)
        {
            existing.Activate();
            return existing;
        }
        _window = new TigerOverlayWindow(surfaceId, _hub, _runtime);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
        _window.Focus();
        return _window;
    }

    public void Detach() => _window?.Close();
}
