using System.Collections.Concurrent;
using Genesis.Surface.Abstractions;

namespace Genesis.Surface.Runtime;

public interface ISurfaceActionHandler
{
    string ActionId { get; }
    Task<SurfaceActionResult> HandleAsync(SurfaceActionRequest request, CancellationToken ct);
}

public sealed class DelegateSurfaceActionHandler(
    string actionId,
    Func<SurfaceActionRequest, CancellationToken, Task<SurfaceActionResult>> handler)
    : ISurfaceActionHandler
{
    public string ActionId { get; } = string.IsNullOrWhiteSpace(actionId)
        ? throw new ArgumentException("Action ID is required.", nameof(actionId))
        : actionId;

    public Task<SurfaceActionResult> HandleAsync(SurfaceActionRequest request, CancellationToken ct)
        => handler(request, ct);
}

/// <summary>Reusable registry and receipt boundary for actions emitted by surfaces.</summary>
public sealed class SurfaceActionDispatcher
{
    private readonly ConcurrentDictionary<string, ISurfaceActionHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<SurfaceActionReceipt>? ActionCompleted;

    public void Register(ISurfaceActionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[handler.ActionId] = handler;
    }

    public bool Unregister(string actionId) => _handlers.TryRemove(actionId, out _);

    public async Task<SurfaceActionReceipt> DispatchAsync(
        SurfaceActionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = request with
        {
            RequestedAt = request.RequestedAt ?? DateTimeOffset.UtcNow
        };

        SurfaceActionResult result;
        if (!_handlers.TryGetValue(normalized.ActionId, out var handler))
        {
            result = new(false, false, $"no_handler_registered:{normalized.ActionId}");
        }
        else
        {
            try
            {
                result = await handler.HandleAsync(normalized, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                result = new(false, false, "canceled");
            }
            catch (Exception ex)
            {
                result = new(false, false, ex.Message);
            }
        }

        var receipt = new SurfaceActionReceipt(
            Guid.NewGuid().ToString("n"), normalized, result.Accepted,
            result.Completed, result.Error, DateTimeOffset.UtcNow);
        ActionCompleted?.Invoke(this, receipt);
        return receipt;
    }
}
