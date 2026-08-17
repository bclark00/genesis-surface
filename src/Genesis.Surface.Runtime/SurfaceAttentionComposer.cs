using System.Security.Cryptography;
using System.Text;
using Genesis.Surface.Abstractions;

namespace Genesis.Surface.Runtime;

/// <summary>
/// Converts a receipt-bound Quartz activation snapshot into semantic attention
/// regions before any renderer-specific Block IR is emitted.
/// </summary>
public sealed class SurfaceAttentionComposer
{
    public SurfaceComposition Compose(
        SurfaceActivationSnapshot snapshot,
        SurfaceActivationQuery query,
        IReadOnlyList<PromotionRecord> promotions,
        string targetSurfaceId,
        string attentionPolicy = "default")
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(promotions);

        var eligible = promotions
            .Where(p => p.Resolution is not (AttentionResolution.RetainOnly
                or AttentionResolution.SuppressRedundant
                or AttentionResolution.NoResult
                or AttentionResolution.RequireReview))
            .ToDictionary(p => p.CandidateId, StringComparer.Ordinal);

        var capacities = query.RegionCapacities ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [SurfaceAttentionRegion.Now.ToString()] = 3,
            [SurfaceAttentionRegion.Next.ToString()] = 5,
            [SurfaceAttentionRegion.Watch.ToString()] = 8,
        };
        var used = new Dictionary<SurfaceAttentionRegion, int>();
        var assignments = new List<SurfaceAttentionAssignment>();

        foreach (var candidate in snapshot.Candidates
                     .Where(c => c.CandidateId is not null && eligible.ContainsKey(c.CandidateId))
                     .Where(c => c.Confidence >= query.MinimumConfidence)
                     .OrderByDescending(c => c.Activation)
                     .ThenByDescending(c => c.Urgency)
                     .ThenByDescending(c => c.Importance)
                     .Take(Math.Max(0, query.AttentionBudget)))
        {
            var region = ChooseRegion(candidate);
            var capacity = Capacity(capacities, region);
            var ordinal = used.GetValueOrDefault(region);
            if (ordinal >= capacity) continue;

            assignments.Add(new SurfaceAttentionAssignment(
                candidate.ObjectIdentity, region, ordinal,
                Salience(candidate), candidate.Metadata?.GetValueOrDefault("group")));
            used[region] = ordinal + 1;
        }

        return new SurfaceComposition(
            StableId("composition", snapshot.SnapshotId, targetSurfaceId, attentionPolicy),
            snapshot.SnapshotId,
            targetSurfaceId,
            DateTimeOffset.UtcNow,
            assignments,
            attentionPolicy);
    }

    /// <summary>Builds renderer-neutral blocks from a semantic composition.</summary>
    public SurfaceSpec ComposeSpec(
        SurfaceActivationSnapshot snapshot,
        SurfaceActivationQuery query,
        IReadOnlyList<PromotionRecord> promotions,
        string targetSurfaceId,
        string emitBasin = "E_BASIN",
        string altitude = "ground",
        string? title = "Attention")
    {
        var composition = Compose(snapshot, query, promotions, targetSurfaceId);
        var blocks = composition.Assignments
            .GroupBy(a => a.Region)
            .OrderBy(g => g.Key)
            .Select(group => (BlockBase)new ContainerBlock(
                StableId("region", composition.CompositionId, group.Key.ToString()),
                group.Key.ToString(),
                group.OrderBy(a => a.Ordinal).Select(a => (BlockBase)new TextBlock(
                    StableId("candidate", composition.CompositionId, a.ObjectIdentity),
                    a.ObjectIdentity,
                    $"{a.Salience:0.000}" )).ToArray(),
                Layout: "column"))
            .ToArray();

        return SurfaceSpecIdentity.Create(targetSurfaceId, emitBasin, altitude, blocks, title);
    }

    private static SurfaceAttentionRegion ChooseRegion(SurfaceActivationCandidate c)
        => c.Urgency >= 0.8 || c.Activation >= 0.9
            ? SurfaceAttentionRegion.Now
            : c.Urgency >= 0.45 || c.Importance >= 0.7
                ? SurfaceAttentionRegion.Next
                : SurfaceAttentionRegion.Watch;

    private static double Salience(SurfaceActivationCandidate c)
        => Math.Clamp((c.Activation + c.Importance + c.Urgency + c.Confidence) / 4.0, 0, 1);

    private static int Capacity(IReadOnlyDictionary<string, int> capacities, SurfaceAttentionRegion region)
        => capacities.TryGetValue(region.ToString(), out var value) ? Math.Max(0, value) : int.MaxValue;

    private static string StableId(string kind, params string[] parts)
        => $"{kind}-{Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join("\u001f", parts)))).ToLowerInvariant()[..16]}";
}
