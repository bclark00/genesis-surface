namespace Genesis.Surface.Abstractions;

public sealed record SurfaceActionRequest(
    string ActionId,
    string SurfaceId,
    string? IntentId = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    DateTimeOffset? RequestedAt = null);

public sealed record SurfaceActionResult(
    bool Accepted,
    bool Completed,
    string? Error = null,
    IReadOnlyDictionary<string, string>? Evidence = null);

public sealed record SurfaceActionReceipt(
    string ReceiptId,
    SurfaceActionRequest Request,
    bool Accepted,
    bool Completed,
    string? Error,
    DateTimeOffset RecordedAt);
