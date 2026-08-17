using Genesis.Surface.Abstractions;

namespace Genesis.Surface.Runtime;

/// <summary>
/// Routes renderer-originated action messages through the shared dispatcher
/// and returns an action receipt over the same surface channel.
/// </summary>
public sealed class SurfaceActionRouter : IDisposable
{
    private readonly ISurfaceChannel _channel;
    private readonly SurfaceActionDispatcher _dispatcher;
    private int _disposed;

    public SurfaceActionRouter(ISurfaceChannel channel, SurfaceActionDispatcher dispatcher)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _channel.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(object? sender, SurfaceMessage message)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            !string.Equals(message.Op, "action.request", StringComparison.OrdinalIgnoreCase) ||
            message.ActionRequest is null)
            return;

        _ = DispatchAsync(message.ActionRequest);
    }

    private async Task DispatchAsync(SurfaceActionRequest request)
    {
        SurfaceActionReceipt receipt;
        try
        {
            receipt = await _dispatcher.DispatchAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            receipt = new SurfaceActionReceipt(
                Guid.NewGuid().ToString("n"), request, false, false,
                ex.Message, DateTimeOffset.UtcNow);
        }

        if (Volatile.Read(ref _disposed) == 0)
            await _channel.SendAsync(new SurfaceMessage(
                "action.receipt", request.SurfaceId,
                ActionReceipt: receipt)).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _channel.MessageReceived -= OnMessageReceived;
    }
}
