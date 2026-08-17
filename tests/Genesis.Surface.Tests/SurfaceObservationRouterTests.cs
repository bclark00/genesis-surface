using Genesis.Surface.Abstractions;
using Genesis.Specialists;
using Xunit;

namespace Genesis.Specialists.Tests;

public sealed class SurfaceObservationRouterTests
{
    private static SurfaceObservation Observation(string eventType, string title = "Workbench") =>
        new(
            "observation-1",
            "phantom.win32",
            eventType,
            "interactive",
            "test-app",
            42,
            title,
            true,
            false,
            new SurfaceBounds(0, 0, 100, 100),
            DateTimeOffset.UtcNow);

    [Fact]
    public void AuthorizationObservation_IsUrgentAndTargetsAmbientTiger()
    {
        var decision = new SurfaceObservationRouter().Evaluate(
            Observation("window.shown", "Windows Security authorization"));

        Assert.Equal("surface", decision.Disposition);
        Assert.Equal("urgent", decision.Urgency);
        Assert.Equal("tiger-ambient", decision.TargetSurface);
        Assert.Contains("visual", decision.Modalities);
        Assert.Contains("audit", decision.Modalities);
    }

    [Fact]
    public void ForegroundChange_IsRetainedWithoutInterrupting()
    {
        var decision = new SurfaceObservationRouter().Evaluate(
            Observation("foreground.changed"));

        Assert.Equal("observe", decision.Disposition);
        Assert.Equal("low", decision.Importance);
        Assert.Contains("audit", decision.Modalities);
        Assert.Null(decision.TargetSurface);
    }

    [Fact]
    public void OrdinaryWindowChange_IsSuppressed()
    {
        var decision = new SurfaceObservationRouter().Evaluate(
            Observation("window.location_changed"));

        Assert.Equal("suppress", decision.Disposition);
        Assert.Empty(decision.Modalities);
        Assert.Null(decision.TargetSurface);
    }
}
