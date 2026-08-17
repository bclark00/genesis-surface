using System.Text.Json.Serialization;

namespace Genesis.Surface.Abstractions;

// ── SURFACE-CHANNEL-001 ───────────────────────────────────────────────────────
// Block IR + projector contracts for live surface expression.
//
// Naming distinction from Genesis.Ribosome:
//   Genesis.Ribosome     = genome → C# phenotype artifacts (static, file-based)
//   Genesis.Surface.*    = Block IR → live runtime surfaces (dynamic, DOM/WPF)
//
// Pipeline:
//   Claude generates SurfaceSpec (Block tree, EMIT-annotated)
//     ↓
//   ISurfaceProjector.ProjectAsync(spec, channel)
//     ├── WebProjector  → HTML fragments → ISurfaceChannel.SendAsync()
//     │                → PostWebMessageAsJson → WebView2 DOM patch
//     ├── CehProjector  → seed_exec surface queue → homebase polling client
//     └── WpfProjector  → XAML fragment → WPF visual tree mutation (future)
//     ↓
//   Surface appears on GMKtec screen in real time

// ── Block base ────────────────────────────────────────────────────────────────

/// <summary>
/// Root type for all Block IR nodes.
/// BlockId must be stable across projections — it is the merge key for op=patch.
/// EmitPrimitive marks the block's EMIT character (E/M/I/T).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "blockType")]
[JsonDerivedType(typeof(TextBlock),      "text")]
[JsonDerivedType(typeof(MetricBlock),    "metric")]
[JsonDerivedType(typeof(StatusBlock),    "status")]
[JsonDerivedType(typeof(ContainerBlock), "container")]
[JsonDerivedType(typeof(LogBlock),       "log")]
public abstract record BlockBase(
    string BlockId,
    string EmitPrimitive,
    string? CssClass = null);

// ── Concrete block types ──────────────────────────────────────────────────────

/// <summary>
/// Labeled scalar text value.
/// E-dominant (E): observation — something exists or is named.
/// Maps to: node-role + node-meta rows in status.html.
/// </summary>
public sealed record TextBlock(
    string  BlockId,
    string  Label,
    string  Value,
    string  EmitPrimitive = "E",
    string? CssClass = null) : BlockBase(BlockId, EmitPrimitive, CssClass);

/// <summary>
/// Numeric value with label, unit, optional trend direction.
/// M-dominant (M): measure — quantity, comparison, magnitude.
/// Maps to: uptime counters, corpus-node counts, port assignments.
/// </summary>
public sealed record MetricBlock(
    string  BlockId,
    string  Label,
    double  Value,
    string  Unit          = "",
    string? Trend         = null,   // "up" | "down" | "flat" | null
    string  EmitPrimitive = "M",
    string? CssClass      = null) : BlockBase(BlockId, EmitPrimitive, CssClass);

/// <summary>
/// Health / lifecycle state indicator.
/// T-dominant (T): transition — state, lifecycle, governance.
/// Maps to: node-card healthy/degraded/down states in status.html.
/// </summary>
public sealed record StatusBlock(
    string  BlockId,
    string  Label,
    string  State,          // "healthy" | "degraded" | "down" | "unknown"
    string? Detail        = null,
    string  EmitPrimitive = "T",
    string? CssClass      = null) : BlockBase(BlockId, EmitPrimitive, CssClass);

/// <summary>
/// Layout wrapper composing child blocks.
/// I-dominant (I): integration — input, composition, assembly.
/// Maps to: hb-panel containers in status.html.
/// </summary>
public sealed record ContainerBlock(
    string                   BlockId,
    string?                  Title,
    IReadOnlyList<BlockBase> Children,
    string                   Layout        = "column", // "column" | "row" | "grid"
    string                   EmitPrimitive = "I",
    string?                  CssClass      = null) : BlockBase(BlockId, EmitPrimitive, CssClass);

/// <summary>
/// Ordered event / commit / log feed.
/// E-dominant (E): observed events over time.
/// Maps to: p-commits panel in status.html.
/// </summary>
public sealed record LogBlock(
    string                   BlockId,
    string?                  Label,
    IReadOnlyList<LogEntry>  Entries,
    string                   EmitPrimitive = "E",
    string?                  CssClass      = null) : BlockBase(BlockId, EmitPrimitive, CssClass);

/// <summary>Single entry in a LogBlock.</summary>
public sealed record LogEntry(
    string Ts,
    string Text,
    string Level = "info");  // "info" | "warn" | "error"

// ── SurfaceSpec ───────────────────────────────────────────────────────────────

/// <summary>
/// A named, EMIT-annotated tree of Blocks.
/// The canonical IR passed to ISurfaceProjector.
///
/// SpecId is content-addressed: same blocks + targetSurfaceId = same spec id.
/// This ensures projections are idempotent for identical inputs.
/// </summary>
public sealed record SurfaceSpec(
    string                   SpecId,
    string                   TargetSurfaceId,
    string                   EmitBasin,         // "E_BASIN" | "M_BASIN" | "I_BASIN" | "T_BASIN"
    string                   Altitude,          // "ground" | "1000ft" | "10000ft" | "50000ft"
    IReadOnlyList<BlockBase> Blocks,
    string?                  Title         = null,
    DateTimeOffset?          GeneratedAt   = null);

// ── Channel abstractions ──────────────────────────────────────────────────────

/// <summary>
/// Bidirectional channel between host process and surface runtime.
///
/// Web impl  (SurfaceChannel.cs):   PostWebMessageAsJson / WebMessageReceived
/// CEH impl  (CehSeedChannel.cs):   genesis-seed surface queue (SSE / polling)
/// Mock impl (MockSurfaceChannel):  in-memory, for tests
/// </summary>
public interface ISurfaceChannel
{
    string SurfaceId { get; }

    /// <summary>Send a SurfaceMessage to the surface runtime.</summary>
    Task SendAsync(SurfaceMessage message, CancellationToken ct = default);

    /// <summary>Fired when the surface runtime sends a message back to the host.</summary>
    event EventHandler<SurfaceMessage>? MessageReceived;
}

/// <summary>
/// Wire format for all channel messages.
///
/// Op semantics:
///   replace  Replace entire target innerHTML with all block Content joined.
///   patch    Merge blocks by BlockId: update existing elements, append new ones.
///   clear    Remove all children from target element.
///
/// Must be kept in sync with surface-channel-client.js (JS counterpart).
/// </summary>
public sealed record SurfaceMessage(
    string                        Op,           // "patch" | "replace" | "clear"
    string                        TargetId,     // DOM element id to target
    IReadOnlyList<RenderedBlock>? Blocks  = null,
    string?                       Error   = null,
    string?                       IntentId = null,
    long                          Revision = 0);

/// <summary>A block rendered to its target format by a projector.</summary>
public sealed record RenderedBlock(
    string BlockId,
    string ContentType,   // "text/html" | "application/xaml+xml"
    string Content);

/// <summary>
/// Committed intent IR for a live surface mutation.
/// SurfaceSpec is the declarative block tree; SurfaceIntent is the governed
/// operation that moves a spec or delta across the channel.
/// </summary>
public sealed record SurfaceIntent(
    string                        IntentId,
    string                        TargetSurfaceId,
    long                          Revision,
    string                        Operation,    // "patch" | "replace" | "clear"
    IReadOnlyList<RenderedBlock>? Blocks = null);

// ── Projector interface ───────────────────────────────────────────────────────

/// <summary>
/// Projects a SurfaceSpec to a live runtime surface via ISurfaceChannel.
///
/// Registered implementations:
///   WebProjector  — Block tree → HTML fragments → WebView2 DOM (PostWebMessageAsJson)
///   CehProjector  — Block tree → seed surface queue → homebase page picks up on poll
///   WpfProjector  — Block tree → XAML fragments → WPF visual tree (future)
/// </summary>
public interface ISurfaceProjector
{
    string TargetKind { get; }  // "web" | "ceh" | "wpf"

    Task<ProjectionReceipt> ProjectAsync(
        SurfaceSpec    spec,
        ISurfaceChannel channel,
        CancellationToken ct = default);
}

// ── Receipt ───────────────────────────────────────────────────────────────────

/// <summary>
/// Audit receipt for a surface projection.
/// Append-only — no modification after creation.
/// </summary>
public sealed record ProjectionReceipt(
    string         ReceiptId,
    string         SpecId,
    string         ProjectorKind,
    string         SurfaceId,
    DateTimeOffset ProjectedAt,
    bool           Success,
    int            BlockCount,
    string?        Error = null);

// ── JSON serialization context ────────────────────────────────────────────────

[JsonSerializable(typeof(SurfaceSpec))]
[JsonSerializable(typeof(SurfaceIntent))]
[JsonSerializable(typeof(SurfaceMessage))]
[JsonSerializable(typeof(RenderedBlock))]
[JsonSerializable(typeof(ProjectionReceipt))]
[JsonSerializable(typeof(BlockBase))]
[JsonSerializable(typeof(TextBlock))]
[JsonSerializable(typeof(MetricBlock))]
[JsonSerializable(typeof(StatusBlock))]
[JsonSerializable(typeof(ContainerBlock))]
[JsonSerializable(typeof(LogBlock))]
[JsonSerializable(typeof(LogEntry))]
public partial class SurfaceJsonContext : JsonSerializerContext { }
