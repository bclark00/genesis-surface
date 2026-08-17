using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Genesis.Surface.Abstractions;

/// <summary>Context and budget supplied when a surface asks Quartz for attention candidates.</summary>
public sealed record SurfaceActivationQuery(
    string QueryId,
    string SessionId,
    string SurfacePurpose,
    string? TaskIdentity = null,
    int AttentionBudget = 100,
    TimeSpan? UrgencyHorizon = null,
    double MinimumConfidence = 0,
    IReadOnlyDictionary<string, int>? RegionCapacities = null,
    IReadOnlyList<string>? DiversityPolicy = null);

/// <summary>One ranked candidate returned by a stable Quartz activation snapshot.</summary>
public sealed record SurfaceActivationCandidate(
    string ObjectIdentity,
    double Activation,
    double Importance,
    double Urgency,
    double Confidence,
    double Recency,
    IReadOnlyList<string>? EvidenceReferences = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? CandidateId = null);

/// <summary>Receipt-bound, immutable selection input for attention composition.</summary>
public sealed record SurfaceActivationSnapshot(
    string SnapshotId,
    string QueryId,
    string QuartzReceiptId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SurfaceActivationCandidate> Candidates,
    string? QuartzEvidenceIdentity = null);

/// <summary>Provider boundary: Quartz returns a stable snapshot, never a live UI cursor.</summary>
public interface IActivationSurfaceProvider
{
    Task<SurfaceActivationSnapshot> QueryAsync(
        SurfaceActivationQuery query,
        CancellationToken cancellationToken = default);
}

public enum SurfaceAttentionRegion { Now, Next, Watch, Context, Review, Pinned }

public sealed record SurfaceAttentionAssignment(
    string ObjectIdentity,
    SurfaceAttentionRegion Region,
    int Ordinal,
    double Salience,
    string? Group = null);

/// <summary>Semantic attention decision made before presentation planning.</summary>
public sealed record SurfaceComposition(
    string CompositionId,
    string ActivationSnapshotId,
    string TargetSurfaceId,
    DateTimeOffset ComposedAt,
    IReadOnlyList<SurfaceAttentionAssignment> Assignments,
    string? AttentionPolicy = null);

public enum SurfaceFeedbackKind
{
    Open, Dismiss, Pin, Defer, Expand, Acknowledge, Correct,
    MarkIrrelevant, RequestMore, RequestLess, Reorder, Connect
}

/// <summary>
/// A fact about an interaction with a specifically exposed surface state.
/// This is not itself an interpretation or Quartz weight update.
/// </summary>
public sealed record SurfaceFeedbackObservation(
    string ObservationId,
    string ActivationSnapshotId,
    string CompositionId,
    string SurfaceSpecId,
    string SurfaceMaterializationId,
    string ObjectIdentity,
    SurfaceAttentionRegion Region,
    int Ordinal,
    SurfaceFeedbackKind Kind,
    TimeSpan? ExposureDuration = null,
    string? Treatment = null,
    string? Payload = null,
    long LocalSequence = 0,
    DateTimeOffset? ObservedAt = null);

public sealed record SurfaceFeedbackInterpretation(
    string InterpretationId,
    string ObservationId,
    string Disposition,
    string AuthorityIdentity,
    IReadOnlyList<string> EvidenceReferences,
    DateTimeOffset InterpretedAt,
    string? QuartzEvidenceEventId = null);

/// <summary>
/// Governance boundary: raw observations must be admitted and interpreted
/// before they can become Quartz evidence or influence future ranking.
/// </summary>
public interface ISurfaceFeedbackInterpreter
{
    Task<SurfaceFeedbackInterpretation> InterpretAsync(
        SurfaceFeedbackObservation observation,
        CancellationToken cancellationToken = default);
}

/// <summary>Deterministic identity binding for SurfaceSpec content and target.</summary>
public static class SurfaceSpecIdentity
{
    public static string ComputeSpecId(
        string targetSurfaceId,
        string emitBasin,
        string altitude,
        IReadOnlyList<BlockBase> blocks,
        string? title = null)
    {
        var canonical = new StringBuilder();
        Add(canonical, targetSurfaceId);
        Add(canonical, emitBasin);
        Add(canonical, altitude);
        Add(canonical, title);
        foreach (var block in blocks) AppendBlock(canonical, block);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    public static SurfaceSpec Create(
        string targetSurfaceId,
        string emitBasin,
        string altitude,
        IReadOnlyList<BlockBase> blocks,
        string? title = null,
        DateTimeOffset? generatedAt = null)
        => new(ComputeSpecId(targetSurfaceId, emitBasin, altitude, blocks, title),
            targetSurfaceId, emitBasin, altitude, blocks, title, generatedAt);

    private static void AppendBlock(StringBuilder b, BlockBase block)
    {
        Add(b, block.GetType().Name); Add(b, block.BlockId); Add(b, block.EmitPrimitive); Add(b, block.CssClass);
        switch (block)
        {
            case TextBlock x: Add(b, x.Label); Add(b, x.Value); break;
            case MetricBlock x: Add(b, x.Label); Add(b, x.Value.ToString("R")); Add(b, x.Unit); Add(b, x.Trend); break;
            case StatusBlock x: Add(b, x.Label); Add(b, x.State); Add(b, x.Detail); break;
            case ContainerBlock x:
                Add(b, x.Title); Add(b, x.Layout);
                foreach (var child in x.Children) AppendBlock(b, child);
                break;
            case LogBlock x:
                Add(b, x.Label);
                foreach (var entry in x.Entries) { Add(b, entry.Ts); Add(b, entry.Text); Add(b, entry.Level); }
                break;
            case IntentBlock x: Add(b, x.IntentId); Add(b, x.Title); Add(b, x.Status); Add(b, x.Source); break;
            case PlanBlock x: Add(b, x.Operation); Add(b, x.Target); Add(b, x.Disposition); Add(b, x.Explanation); Add(b, x.RequiresAuthorization); break;
            case AuthorizationBlock x: Add(b, x.Operation); Add(b, x.Target); Add(b, x.State); break;
            case ExecutionStageBlock x: Add(b, x.Stage); Add(b, x.State); Add(b, x.Detail); break;
            case EvidenceTrailBlock x:
                foreach (var entry in x.Entries) { Add(b, entry.Ts); Add(b, entry.Text); Add(b, entry.Level); }
                break;
        }
    }

    private static void Add(StringBuilder b, object? value)
    {
        var text = value?.ToString() ?? "<null>";
        b.Append(text.Length).Append(':').Append(text).Append('|');
    }
}

[JsonSerializable(typeof(SurfaceActivationQuery))]
[JsonSerializable(typeof(SurfaceActivationCandidate))]
[JsonSerializable(typeof(SurfaceActivationSnapshot))]
[JsonSerializable(typeof(SurfaceComposition))]
[JsonSerializable(typeof(SurfaceAttentionAssignment))]
[JsonSerializable(typeof(SurfaceFeedbackObservation))]
[JsonSerializable(typeof(SurfaceFeedbackInterpretation))]
[JsonSerializable(typeof(RelevanceVector))]
[JsonSerializable(typeof(AttentionCandidate))]
[JsonSerializable(typeof(PromotionRecord))]
[JsonSerializable(typeof(ProtocolObservation))]
[JsonSerializable(typeof(ObservationEvidence))]
public partial class AdaptiveSurfaceJsonContext : JsonSerializerContext { }
