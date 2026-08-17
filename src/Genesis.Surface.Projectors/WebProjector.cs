using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Genesis.Surface.Abstractions;

namespace Genesis.Surface.Projectors;

// ── WebProjector ──────────────────────────────────────────────────────────────
// RFC: SURFACE-CHANNEL-001 §WebProjector
//
// Projects a SurfaceSpec to HTML fragments delivered via ISurfaceChannel.
// The surface runtime (surface-channel-client.js) receives the SurfaceMessage
// and applies the patch to the live DOM via window.chrome.webview — no navigation.
//
// Op selection:
//   op=replace  Replace entire surface area (first projection, or spec change)
//   op=patch    Merge by BlockId (subsequent updates — only changed blocks sent)
//
// Current implementation always emits op=replace for simplicity.
// Incremental patch support (diff previous state) is a future optimization.

public sealed class WebProjector : ISurfaceProjector
{
    public string TargetKind => "web";

    public async Task<ProjectionReceipt> ProjectAsync(
        SurfaceSpec spec,
        ISurfaceChannel channel,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(channel);

        var receiptId = NewId();
        var projectedAt = DateTimeOffset.UtcNow;

        try
        {
            var rendered = spec.Blocks
                .Select(b => new RenderedBlock(b.BlockId, "text/html", RenderBlock(b)))
                .ToList();

            var message = new SurfaceMessage(
                Op: "replace",
                TargetId: spec.TargetSurfaceId,
                Blocks: rendered);

            await channel.SendAsync(message, ct).ConfigureAwait(false);

            return new ProjectionReceipt(
                ReceiptId:     receiptId,
                SpecId:        spec.SpecId,
                ProjectorKind: TargetKind,
                SurfaceId:     channel.SurfaceId,
                ProjectedAt:   projectedAt,
                Success:       true,
                BlockCount:    spec.Blocks.Count);
        }
        catch (Exception ex)
        {
            return new ProjectionReceipt(
                ReceiptId:     receiptId,
                SpecId:        spec.SpecId,
                ProjectorKind: TargetKind,
                SurfaceId:     channel.SurfaceId,
                ProjectedAt:   projectedAt,
                Success:       false,
                BlockCount:    spec.Blocks.Count,
                Error:         ex.Message);
        }
    }

    // ── Block renderers ───────────────────────────────────────────────────────

    private static string RenderBlock(BlockBase block) => block switch
    {
        TextBlock      b => RenderText(b),
        MetricBlock    b => RenderMetric(b),
        StatusBlock    b => RenderStatus(b),
        ContainerBlock b => RenderContainer(b),
        LogBlock       b => RenderLog(b),
        _                =>
            $"<div class=\"g-block g-unknown\" id=\"{Esc(block.BlockId)}\">" +
            $"<span class=\"g-label\">[unknown block type]</span></div>"
    };

    private static string RenderText(TextBlock b) =>
        $"<div class=\"g-block g-text {Esc(b.CssClass)}\" id=\"{Esc(b.BlockId)}\">" +
        $"<span class=\"g-label\">{Esc(b.Label)}</span>" +
        $"<span class=\"g-value\">{Esc(b.Value)}</span>" +
        "</div>";

    private static string RenderMetric(MetricBlock b)
    {
        var trend = b.Trend switch
        {
            "up"   => "<span class=\"g-trend-up\">&#8593;</span>",
            "down" => "<span class=\"g-trend-down\">&#8595;</span>",
            "flat" => "<span class=\"g-trend-flat\">&#8594;</span>",
            _      => ""
        };
        return
            $"<div class=\"g-block g-metric {Esc(b.CssClass)}\" id=\"{Esc(b.BlockId)}\">" +
            $"<span class=\"g-label\">{Esc(b.Label)}</span>" +
            $"<span class=\"g-value\">{b.Value:G6}" +
            $"<span class=\"g-unit\">{Esc(b.Unit)}</span>{trend}</span>" +
            "</div>";
    }

    private static string RenderStatus(StatusBlock b)
    {
        var stateCss = b.State switch
        {
            "healthy"  => "g-healthy",
            "degraded" => "g-degraded",
            "down"     => "g-down",
            _          => "g-unknown"
        };
        var detail = b.Detail is not null
            ? $"<span class=\"g-detail\">{Esc(b.Detail)}</span>"
            : "";
        return
            $"<div class=\"g-block g-status {stateCss} {Esc(b.CssClass)}\" id=\"{Esc(b.BlockId)}\">" +
            $"<span class=\"g-label\">{Esc(b.Label)}</span>" +
            $"<span class=\"g-state\">{Esc(b.State)}</span>" +
            $"{detail}</div>";
    }

    private static string RenderContainer(ContainerBlock b)
    {
        var sb = new StringBuilder();
        sb.Append(
            $"<div class=\"g-block g-container g-layout-{Esc(b.Layout)} {Esc(b.CssClass)}\"" +
            $" id=\"{Esc(b.BlockId)}\">");
        if (b.Title is not null)
            sb.Append($"<div class=\"g-container-title\">{Esc(b.Title)}</div>");
        foreach (var child in b.Children)
            sb.Append(RenderBlock(child));
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string RenderLog(LogBlock b)
    {
        var sb = new StringBuilder();
        sb.Append(
            $"<div class=\"g-block g-log {Esc(b.CssClass)}\" id=\"{Esc(b.BlockId)}\">");
        if (b.Label is not null)
            sb.Append($"<div class=\"g-log-label\">{Esc(b.Label)}</div>");
        sb.Append("<div class=\"g-log-entries\">");
        foreach (var e in b.Entries)
        {
            var lvlCss = e.Level switch
            {
                "warn"  => "g-warn",
                "error" => "g-error",
                _       => "g-info"
            };
            sb.Append(
                $"<div class=\"g-log-entry {lvlCss}\">" +
                $"<span class=\"g-ts\">{Esc(e.Ts)}</span>" +
                $"<span class=\"g-msg\">{Esc(e.Text)}</span></div>");
        }
        sb.Append("</div></div>");
        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Esc(string? s)
    {
        if (s is null) return "";
        return s
            .Replace("&",  "&amp;")
            .Replace("<",  "&lt;")
            .Replace(">",  "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'",  "&#39;");
    }

    private static string NewId()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
}
