using Genesis.Surface.Abstractions;
using Genesis.Surface.Runtime;
using Xunit;

namespace Genesis.Specialists.Tests;

public sealed class SurfacePresenceTests
{
    [Fact]
    public void RuntimeRetainsLatestRendererPresenceWithoutPromotingProjectionToDisplay()
    {
        var channel = new TestPresenceChannel("tiger-ambient");
        var runtime = new SurfaceRuntime();
        runtime.Open(new SurfaceOpenRequest("tiger-ambient", "test", "ambient"), channel);

        channel.Emit(new SurfacePresenceObservation(
            "presence-1", "tiger-ambient", "spec-1", 1, "projected",
            true, null, null, "test.projector", DateTimeOffset.UtcNow));

        Assert.True(runtime.TryGetPresence("tiger-ambient", out var projected));
        Assert.Equal("projected", projected!.Presence);

        channel.Emit(projected with
        {
            ObservationId = "presence-2",
            Presence = "displayed",
            EvidenceKind = "test.render-pass"
        });

        var snapshot = Assert.Single(runtime.PresenceSnapshot());
        Assert.Equal("displayed", snapshot.Presence);
        Assert.Equal("test.render-pass", snapshot.EvidenceKind);
    }

    private sealed class TestPresenceChannel(string surfaceId) : ISurfaceChannel, ISurfacePresenceSource
    {
        public string SurfaceId { get; } = surfaceId;
        public event EventHandler<SurfaceMessage>? MessageReceived;
        public event EventHandler<SurfacePresenceObservation>? PresenceChanged;

        public Task SendAsync(SurfaceMessage message, CancellationToken ct = default)
            => Task.CompletedTask;

        public void Emit(SurfacePresenceObservation observation)
            => PresenceChanged?.Invoke(this, observation);
    }
}
