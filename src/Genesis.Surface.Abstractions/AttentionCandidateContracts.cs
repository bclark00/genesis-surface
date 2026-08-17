namespace Genesis.Surface.Abstractions;

/// <summary>
/// Fixed-point relevance dimensions. Values are in [0, 10_000] so identity-
/// bearing encodings never depend on binary floating-point representation.
/// </summary>
public sealed record RelevanceVector(
    int ContextualRelevance,
    int Urgency,
    int Consequence,
    int Novelty,
    int EvidenceStrength,
    int Actionability,
    int UserAffinity,
    int InterruptionCost,
    int Redundancy,
    int Uncertainty)
{
    public static RelevanceVector Clamp(RelevanceVector value) => value with
    {
        ContextualRelevance = Clamp01(value.ContextualRelevance),
        Urgency = Clamp01(value.Urgency),
        Consequence = Clamp01(value.Consequence),
        Novelty = Clamp01(value.Novelty),
        EvidenceStrength = Clamp01(value.EvidenceStrength),
        Actionability = Clamp01(value.Actionability),
        UserAffinity = Clamp01(value.UserAffinity),
        InterruptionCost = Clamp01(value.InterruptionCost),
        Redundancy = Clamp01(value.Redundancy),
        Uncertainty = Clamp01(value.Uncertainty),
    };

    private static int Clamp01(int value) => Math.Clamp(value, 0, 10_000);
}

public enum AttentionResolution
{
    Interrupt,
    SurfaceInline,
    SurfacePersistent,
    IncludeInSummary,
    UpdateExisting,
    RetainOnly,
    RequireReview,
    SuppressRedundant,
    NoResult,
}

/// <summary>
/// Activation candidate with explicit evidence, policy, uncertainty, and
/// channel boundaries. Activation does not imply promotion.
/// </summary>
public sealed record AttentionCandidate(
    string CandidateId,
    string SubjectIdentity,
    string? EvidenceId,
    string? InterpretationId,
    string ActivationContextId,
    RelevanceVector Relevance,
    string GoverningPolicyId,
    IReadOnlyList<string> AllowedChannels,
    string UncertaintyClass,
    string? ExplanationRoot = null);

/// <summary>Governed decision separating promotion eligibility from presentation mode.</summary>
public sealed record PromotionRecord(
    string PromotionRecordId,
    string CandidateId,
    string AuthorityIdentity,
    AttentionResolution Resolution,
    string PolicyIdentity,
    IReadOnlyList<string> EvidenceReferences,
    DateTimeOffset RecordedAt,
    string? SurfaceItemId = null);

/// <summary>Stable identity for a source-admitted observation before interpretation.</summary>
public sealed record ProtocolObservation(
    string ObservationId,
    string SourceIdentity,
    string SourceClass,
    string? Direction,
    string NormalizedCoordinate,
    string PayloadIdentity,
    string ObservationPolicyIdentity,
    string CaptureReceipt);

/// <summary>Evidence supporting an interpretation without asserting causation implicitly.</summary>
public sealed record ObservationEvidence(
    string EvidenceId,
    IReadOnlyList<string> AdmittedObservationRoots,
    IReadOnlyList<string> DerivedObservationRoots,
    IReadOnlyList<string> AlignmentRoots,
    IReadOnlyList<string> MappingRevisionRoots,
    int SupportCount,
    IReadOnlyList<string> CounterevidenceRoots,
    string DerivationPolicyId);
