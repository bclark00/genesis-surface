using System.Text.Json;
using Genesis.Surface.Abstractions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Genesis.Windows.Ribosome;

// ── SurfaceChannel ────────────────────────────────────────────────────────────
// RFC: SURFACE-CHANNEL-001 §SurfaceChannel (WebView2 implementation)
//
// The direct DOM manipulation mechanism.
//
// Host → DOM:  CoreWebView2.PostWebMessageAsJson(json)
//              Received in JS as: window.chrome.webview.addEventListener('message', fn)
//
// DOM → Host:  window.chrome.webview.postMessage(data) (from JS)
//              Received as:       CoreWebView2.WebMessageReceived += handler
//
// Wire format: SurfaceMessage (Genesis.Surface.Abstractions)
//   { Op, TargetId, Blocks[{BlockId, ContentType, Content}], Error }
//
// Client side: genesis-homebase/webapps/genesis/surface-channel-client.js
// That script receives the message and applies op=replace/patch/clear to the DOM.
//
// Threading: Send must be called on the WPF dispatcher thread.
// For cross-thread sends, use Application.Current.Dispatcher.InvokeAsync.

public sealed class SurfaceChannel : ISurfaceChannel, IDisposable
{
    private readonly WebView2 _webView;
    private bool              _disposed;

    public string SurfaceId { get; }

    /// <summary>
    /// Fired when the surface runtime posts a message back to the host.
    /// Use window.surfaceChannel.send() from JS to trigger this event.
    /// </summary>
    public event EventHandler<SurfaceMessage>? MessageReceived;

    public SurfaceChannel(string surfaceId, WebView2 webView)
    {
        SurfaceId = surfaceId;
        _webView  = webView;
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>
    /// Sends a SurfaceMessage to the live DOM.
    /// The JS surface-channel-client.js handler applies op=replace|patch|clear.
    /// </summary>
    public Task SendAsync(SurfaceMessage message, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(
            message, SurfaceJsonContext.Default.SurfaceMessage);

        _webView.CoreWebView2.PostWebMessageAsJson(json);
        return Task.CompletedTask;
    }

    private void OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_disposed) return;
        try
        {
            var msg = JsonSerializer.Deserialize(
                e.WebMessageAsJson,
                SurfaceJsonContext.Default.SurfaceMessage);
            if (msg is not null)
                MessageReceived?.Invoke(this, msg);
        }
        catch
        {
            // Malformed message from DOM — swallow, never crash the host.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        _disposed = true;
    }
}
