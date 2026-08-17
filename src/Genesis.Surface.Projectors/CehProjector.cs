using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Genesis.Surface.Abstractions;

namespace Genesis.Surface.Projectors;

// ── CehProjector ──────────────────────────────────────────────────────────────
// RFC: SURFACE-CHANNEL-001 §CehProjector
//
// Projects a SurfaceSpec to the genesis-homebase CEH/WWAHost surface.
//
// Transport: genesis-seed surface queue via HTTP POST to seed endpoint.
// The homebase page (status.html + surface-channel-client.js) polls the
// seed for pending surface messages and applies them to the DOM.
//
// Channel direction:
//   Host → seed:    POST /mcp/{token} with tools/call surface_enqueue
//   seed → page:    page polls seed_state which now includes surface_queue
//   page → DOM:     surface-channel-client.js applies SurfaceMessage to DOM
//
// Fallback: if the seed is unreachable, the receipt records the failure.
// The spec is not buffered — caller retries if needed.
//
// Note: CehProjector implements ISurfaceChannel internally (seed-backed).
// The caller passes a CehSeedChannel wrapping the seed endpoint URL + token.

public sealed class CehProjector : ISurfaceProjector
{
    public string TargetKind => "ceh";

    // WebProjector handles HTML rendering — reuse it for the fragment content.
    private readonly WebProjector _webProjector = new();

    public async Task<ProjectionReceipt> ProjectAsync(
        SurfaceSpec     spec,
        ISurfaceChannel channel,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(channel);

        // Render blocks to HTML via WebProjector, then forward via the CEH channel.
        // The CehSeedChannel.SendAsync enqueues the SurfaceMessage on genesis-seed.
        return await _webProjector.ProjectAsync(spec, channel, ct).ConfigureAwait(false);
    }
}

// ── CehSeedChannel ────────────────────────────────────────────────────────────

/// <summary>
/// ISurfaceChannel implementation backed by genesis-seed's surface queue.
///
/// Sends SurfaceMessages to seed via seed_exec PowerShell:
///   Set-Content to a well-known temp path the homebase page polls.
///
/// This is a one-way channel (host → surface). MessageReceived is not
/// supported from the seed direction — use direct WebView2 SurfaceChannel
/// if bidirectional flow is needed.
/// </summary>
public sealed class CehSeedChannel : ISurfaceChannel
{
    private readonly string  _seedUrl;
    private readonly string  _seedToken;
    private readonly HttpClient _http;

    public string SurfaceId { get; }

    // MessageReceived is not supported from seed direction.
    // Inbound events from the homebase page require WebView2 (SurfaceChannel).
    public event EventHandler<SurfaceMessage>? MessageReceived
    {
        add    { /* no-op — one-way channel */ }
        remove { /* no-op */ }
    }

    public CehSeedChannel(
        string surfaceId,
        string seedUrl,
        string seedToken,
        HttpClient? http = null)
    {
        SurfaceId  = surfaceId;
        _seedUrl   = seedUrl.TrimEnd('/');
        _seedToken = seedToken;
        _http      = http ?? new HttpClient();
    }

    public async Task SendAsync(SurfaceMessage message, CancellationToken ct = default)
    {
        // Serialize SurfaceMessage → JSON → write to seed temp queue via seed_exec.
        // The homebase page picks up the latest queue file on each poll cycle.
        var json = JsonSerializer.Serialize(
            message, SurfaceJsonContext.Default.SurfaceMessage);

        // Escape for PowerShell double-quoted string
        var escaped = json.Replace("\"", "\\\"");
        var cmd =
            $"Set-Content -Path 'C:\\genesis\\surface-queue\\{SurfaceId}.json'" +
            $" -Value \"{escaped}\" -Encoding UTF8 -Force";

        var body = new
        {
            jsonrpc = "2.0",
            id      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            method  = "tools/call",
            @params = new
            {
                name      = "seed_exec",
                arguments = new { command = $"powershell.exe -NoProfile -Command \"{cmd}\"" }
            }
        };

        var url = $"{_seedUrl}/{_seedToken}";
        using var resp = await _http.PostAsJsonAsync(url, body, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }
}
