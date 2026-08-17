using Genesis.Surface.Abstractions;
using Genesis.Surface.Runtime;
using Xunit;

namespace Genesis.Specialists.Tests;

public sealed class SurfaceActionDispatcherTests
{
    [Fact]
    public async Task RegisteredActionProducesCompletedReceipt()
    {
        var dispatcher = new SurfaceActionDispatcher();
        dispatcher.Register(new DelegateSurfaceActionHandler(
            "test.confirm",
            (_, _) => Task.FromResult(new SurfaceActionResult(true, true))));

        var receipt = await dispatcher.DispatchAsync(
            new SurfaceActionRequest("test.confirm", "tiger-test"));

        Assert.True(receipt.Accepted);
        Assert.True(receipt.Completed);
        Assert.Null(receipt.Error);
    }

    [Fact]
    public async Task UnknownActionProducesExplicitRejectionReceipt()
    {
        var receipt = await new SurfaceActionDispatcher().DispatchAsync(
            new SurfaceActionRequest("missing.action", "tiger-test"));

        Assert.False(receipt.Accepted);
        Assert.False(receipt.Completed);
        Assert.Equal("no_handler_registered:missing.action", receipt.Error);
    }
}
