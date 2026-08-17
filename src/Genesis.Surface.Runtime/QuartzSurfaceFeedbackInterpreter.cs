using Genesis.Memory.Quartz;
using Genesis.Surface.Abstractions;

namespace Genesis.Surface.Runtime;

/// <summary>
/// Bridges admitted surface observations into Quartz evidence.
/// InterpretAsync records the observation only; it does not alter Quartz
/// influence. ApplyGovernedEvidenceAsync is the explicit receipt-bearing lane
/// that may materialize influence.
/// </summary>
public sealed class QuartzSurfaceFeedbackInterpreter : ISurfaceFeedbackInterpreter, IDisposable
{
    private readonly QuartzGovernedFeedback _quartz;
    private readonly string _authorityIdentity;

    public QuartzSurfaceFeedbackInterpreter(
        QuartzGovernedFeedback quartz,
        string authorityIdentity = "surface.feedback.observation")
    {
        _quartz = quartz ?? throw new ArgumentNullException(nameof(quartz));
        _authorityIdentity = string.IsNullOrWhiteSpace(authorityIdentity)
            ? "surface.feedback.observation"
            : authorityIdentity;
    }

    public Task<SurfaceFeedbackInterpretation> InterpretAsync(
        SurfaceFeedbackObservation observation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(observation);

        var entry = _quartz.RecordFeedback(ToQuartzRecord(observation));
        return Task.FromResult(new SurfaceFeedbackInterpretation(
            observation.ObservationId,
            observation.ObservationId,
            "OBSERVED",
            _authorityIdentity,
            new[] { entry.EntryHash },
            observation.ObservedAt ?? DateTimeOffset.UtcNow,
            entry.EntryHash));
    }

    public Task<QuartzEvidenceEntry> ApplyGovernedEvidenceAsync(
        SurfaceFeedbackObservation observation,
        string decisionReceiptId,
        double signedInfluence,
        double confidence = 1.0,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(observation);

        var entry = _quartz.ApplyGovernedEvidence(
            ToQuartzRecord(observation), decisionReceiptId, signedInfluence, confidence);
        return Task.FromResult(entry);
    }

    private static QuartzFeedbackRecord ToQuartzRecord(SurfaceFeedbackObservation observation) =>
        new()
        {
            EvidenceId = observation.ObservationId,
            SnapshotId = observation.ActivationSnapshotId,
            ContextId = observation.CompositionId,
            FacetIdentity = observation.ObjectIdentity,
            Gesture = observation.Kind.ToString(),
            Payload = observation.Payload,
        };

    public void Dispose() => _quartz.Dispose();
}
