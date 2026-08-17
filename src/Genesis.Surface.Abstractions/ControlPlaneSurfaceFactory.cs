namespace Genesis.Surface.Abstractions;

/// <summary>Builds a renderer-neutral control-plane surface; it never executes domain work.</summary>
public static class ControlPlaneSurfaceFactory
{
    public static SurfaceSpec Create(
        string targetSurfaceId, string intentId, string intentTitle, string intentStatus,
        string operation, string? target, string planDisposition, string explanation,
        bool requiresAuthorization, string authorizationState,
        IReadOnlyList<(string Stage, string State, string? Detail)> stages,
        IReadOnlyList<LogEntry> evidence, string? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSurfaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var blocks = new List<BlockBase>
        {
            new IntentBlock("control.intent", intentId, intentTitle, intentStatus, source),
            new PlanBlock("control.plan", operation, target, planDisposition, explanation, requiresAuthorization),
            new AuthorizationBlock("control.authorization", operation, target ?? "all", authorizationState),
            new ContainerBlock("control.execution", "Execution",
                stages.Select((x, i) => (BlockBase)new ExecutionStageBlock($"control.stage.{i}", x.Stage, x.State, x.Detail)).ToArray()),
            new EvidenceTrailBlock("control.evidence", evidence)
        };
        var seed = string.Join('|', intentId, operation, target, planDisposition, authorizationState,
            string.Join(';', stages.Select(x => $"{x.Stage}:{x.State}")), evidence.Count);
        var specId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(seed)))[..16].ToLowerInvariant();
        return new SurfaceSpec(specId, targetSurfaceId, "I_BASIN", "1000ft", blocks,
            "Genesis Control Plane", DateTimeOffset.UtcNow);
    }
}
