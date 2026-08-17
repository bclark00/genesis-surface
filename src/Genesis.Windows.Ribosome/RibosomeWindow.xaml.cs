using System.Windows;
using Genesis.Surface.Abstractions;
using Microsoft.Web.WebView2.Wpf;

namespace Genesis.Windows.Ribosome;

// ── RibosomeWindow ────────────────────────────────────────────────────────────
// RFC: SURFACE-CHANNEL-001 §RibosomeWindow
//
// A WPF Window that hosts a WebView2 surface and exposes a SurfaceChannel
// for real-time DOM manipulation from the host process.
//
// Architecture:
//   WPF shell (RibosomeWindow) owns the chrome (title, resize, DPI, z-order)
//   WebView2 owns the render surface (Chromium)
//   SurfaceChannel is the bridge (PostWebMessageAsJson / WebMessageReceived)
//
// Content changes flow via SurfaceChannel.SendAsync() without navigation.
// The initial URL loads surface-channel-client.js which wires up the WebView2
// message listener and applies DOM patches on arrival.
//
// Managed by RibosomeHost (multi-window surface registry).

public partial class RibosomeWindow : Window
{
    private SurfaceChannel? _channel;

    /// <summary>The named surface id — matches TargetSurfaceId in SurfaceSpec.</summary>
    public string SurfaceId { get; }

    /// <summary>
    /// The bidirectional DOM channel.
    /// Available after the window is Loaded and CoreWebView2 is initialized.
    /// Null until Loaded fires.
    /// </summary>
    public ISurfaceChannel? Channel => _channel;

    /// <summary>Fires once the SurfaceChannel is ready for use.</summary>
    public event EventHandler? ChannelReady;

    public RibosomeWindow(string surfaceId, string initialUrl)
    {
        SurfaceId = surfaceId;
        InitializeComponent();
        Title = $"Genesis \u2014 {surfaceId}";
        Loaded += async (_, _) => await InitializeAsync(initialUrl).ConfigureAwait(false);
    }

    private async Task InitializeAsync(string initialUrl)
    {
        await WebView.EnsureCoreWebView2Async().ConfigureAwait(false);
        _channel = new SurfaceChannel(SurfaceId, WebView);
        WebView.CoreWebView2.Navigate(initialUrl);
        ChannelReady?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClosed(EventArgs e)
    {
        _channel?.Dispose();
        base.OnClosed(e);
    }
}
