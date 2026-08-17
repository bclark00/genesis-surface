namespace Genesis.Surface.Abstractions;

public sealed record SurfaceBounds(int Left, int Top, int Right, int Bottom);

/// <summary>Normalized environmental fact emitted by an OS observer such as Phantom.</summary>
public sealed record SurfaceObservation(
    string ObservationId,
    string Source,
    string EventType,
    string SessionId,
    string ApplicationIdentity,
    long WindowHandle,
    string? Title,
    bool Visible,
    bool Focused,
    SurfaceBounds? Bounds,
    DateTimeOffset ObservedAt,
    IReadOnlyDictionary<string, string>? Attributes = null);

/// <summary>Policy result. A decision is not a domain admission or execution receipt.</summary>
public sealed record SurfaceDecision(
    string DecisionId,
    string ObservationId,
    string Disposition,
    string Importance,
    string Urgency,
    IReadOnlyList<string> Modalities,
    string? TargetSurface,
    string Reason,
    DateTimeOffset DecidedAt);

/// <summary>Evidence that a chosen presentation modality accepted or rejected delivery.</summary>
public sealed record SurfaceDeliveryReceipt(
    string ReceiptId,
    string DecisionId,
    string Modality,
    string? SurfaceId,
    bool Accepted,
    bool Delivered,
    string? Error,
    DateTimeOffset RecordedAt);

/// <summary>
/// Renderer-neutral evidence about whether a surface projection is merely
/// prepared, actually displayed, or acknowledged by its host.
/// </summary>
public sealed record SurfacePresenceObservation(
    string ObservationId,
    string SurfaceId,
    string? SpecId,
    long? Revision,
    string Presence,
    bool Visible,
    bool? Occluded,
    SurfaceBounds? Bounds,
    string EvidenceKind,
    DateTimeOffset ObservedAt,
    IReadOnlyDictionary<string, string>? Attributes = null);

/// <summary>Optional channel capability for reporting display presence.</summary>
public interface ISurfacePresenceSource
{
    event EventHandler<SurfacePresenceObservation>? PresenceChanged;
}
