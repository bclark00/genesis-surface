using System.Collections.Concurrent;
using Genesis.Surface.Abstractions;

namespace Genesis.Surface.Runtime;

public sealed record SurfaceOpenRequest(
    string SurfaceId,
    string Actor,
    string Kind = "generic",
    bool Remote = false);

public sealed record SurfaceMutation(
    string IntentId,
    string Operation,
    IReadOnlyList<RenderedBlock>? Blocks = null);

public sealed record SurfaceAttachRequest(
    string ProjectionId,
    string TargetKind = "generic",
    bool Remote = false,
    bool ReplayLast = true);

public sealed record SurfaceProjection(
    string ProjectionId,
    string SessionId,
    string SurfaceId,
    string TargetKind,
    bool Remote,
    DateTimeOffset AttachedAt,
    long LastRevision);

public sealed record SurfaceSession(
    string SessionId,
    string SurfaceId,
    string Actor,
    string Kind,
    bool Remote,
    DateTimeOffset OpenedAt,
    long Revision,
    bool Closed = false);

public sealed record SurfaceRuntimeReceipt(
    string ReceiptId,
    string SessionId,
    string IntentId,
    string Operation,
    long Revision,
    bool Success,
    string? Error = null);

/// <summary>
/// Transport-neutral lifecycle and intent dispatcher for live surfaces.
///
/// HomeBase, a WebView2 window, a CEH queue, or a remote relay all register
/// as ISurfaceChannel implementations. The runtime owns session identity and
/// monotonic revisions; the channel only transports the resulting message.
/// </summary>
public sealed class SurfaceRuntime
{
    private sealed class Entry
    {
        public required SurfaceSession Session;
        public Dictionary<string, ProjectionEntry> Projections { get; } = new();
        public SurfaceMessage? LastMessage;
        public SemaphoreSlim Gate { get; } = new(1, 1);
    }

    private sealed class ProjectionEntry
    {
        public required SurfaceProjection Projection;
        public required ISurfaceChannel Channel;
    }

    private readonly ConcurrentDictionary<string, Entry> _sessions = new();

    public SurfaceSession Open(SurfaceOpenRequest request, ISurfaceChannel channel)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(channel);
        if (string.IsNullOrWhiteSpace(request.SurfaceId))
            throw new ArgumentException("SurfaceId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Actor))
            throw new ArgumentException("Actor is required.", nameof(request));
        if (!string.Equals(request.SurfaceId, channel.SurfaceId, StringComparison.Ordinal))
            throw new InvalidOperationException("Channel surface does not match the requested surface.");

        var session = new SurfaceSession(
            SessionId: Guid.NewGuid().ToString("n"),
            SurfaceId: request.SurfaceId,
            Actor: request.Actor,
            Kind: request.Kind,
            Remote: request.Remote,
            OpenedAt: DateTimeOffset.UtcNow,
            Revision: 0);

        var entry = new Entry { Session = session };
        entry.Projections[session.SessionId] = new ProjectionEntry
        {
            Projection = new SurfaceProjection(
                session.SessionId, session.SessionId, session.SurfaceId,
                request.Kind, request.Remote, DateTimeOffset.UtcNow, 0),
            Channel = channel
        };

        if (!_sessions.TryAdd(session.SessionId, entry))
            throw new InvalidOperationException("Could not register surface session.");
        return session;
    }

    public async Task<SurfaceProjection?> AttachAsync(
        string sessionId,
        SurfaceAttachRequest request,
        ISurfaceChannel channel,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(channel);
        if (string.IsNullOrWhiteSpace(request.ProjectionId)) return null;
        if (!_sessions.TryGetValue(sessionId, out var entry)) return null;
        if (!string.Equals(channel.SurfaceId, entry.Session.SurfaceId, StringComparison.Ordinal))
            return null;

        await entry.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (entry.Session.Closed || entry.Projections.ContainsKey(request.ProjectionId)) return null;
            var projection = new SurfaceProjection(
                request.ProjectionId, sessionId, entry.Session.SurfaceId,
                request.TargetKind, request.Remote, DateTimeOffset.UtcNow,
                entry.Session.Revision);
            var attached = new ProjectionEntry { Projection = projection, Channel = channel };
            entry.Projections.Add(request.ProjectionId, attached);

            if (request.ReplayLast && entry.LastMessage is not null)
            {
                try
                {
                    await channel.SendAsync(entry.LastMessage, ct).ConfigureAwait(false);
                    attached.Projection = projection with { LastRevision = entry.Session.Revision };
                }
                catch
                {
                    entry.Projections.Remove(request.ProjectionId);
                    return null;
                }
            }
            return attached.Projection;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public async Task<bool> DetachAsync(string sessionId, string projectionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry)) return false;
        await entry.Gate.WaitAsync().ConfigureAwait(false);
        try { return entry.Projections.Remove(projectionId); }
        finally { entry.Gate.Release(); }
    }

    public async Task<SurfaceRuntimeReceipt> ApplyAsync(
        string sessionId,
        SurfaceMutation mutation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (!_sessions.TryGetValue(sessionId, out var entry))
            return Reject(sessionId, mutation, 0, "surface_session_not_found");
        await entry.Gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (entry.Session.Closed)
                return Reject(sessionId, mutation, entry.Session.Revision, "surface_session_closed");
            if (string.IsNullOrWhiteSpace(mutation.IntentId))
                return Reject(sessionId, mutation, entry.Session.Revision, "intent_id_required");
            if (mutation.Operation is not ("patch" or "replace" or "clear"))
                return Reject(sessionId, mutation, entry.Session.Revision, "unsupported_surface_operation");

            var revision = entry.Session.Revision + 1;
            var intent = new SurfaceIntent(
                mutation.IntentId,
                entry.Session.SurfaceId,
                revision,
                mutation.Operation,
                mutation.Blocks);

            var message = new SurfaceMessage(
                intent.Operation,
                intent.TargetSurfaceId,
                intent.Blocks,
                IntentId: intent.IntentId,
                Revision: intent.Revision);

            var delivered = 0;
            var failures = new List<string>();
            foreach (var projection in entry.Projections.Values.ToArray())
            {
                try
                {
                    await projection.Channel.SendAsync(message, ct).ConfigureAwait(false);
                    projection.Projection = projection.Projection with { LastRevision = revision };
                    delivered++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{projection.Projection.ProjectionId}: {ex.Message}");
                }
            }
            if (delivered == 0)
                return Reject(sessionId, mutation, entry.Session.Revision, "no_projection_delivered");

            entry.LastMessage = message;
            entry.Session = entry.Session with { Revision = revision };
            return new SurfaceRuntimeReceipt(
                Guid.NewGuid().ToString("n"), sessionId, mutation.IntentId,
                mutation.Operation, revision, true,
                failures.Count == 0 ? null : string.Join("; ", failures));
        }
        catch (Exception ex)
        {
            return Reject(sessionId, mutation, entry.Session.Revision, ex.Message);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public bool Close(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry)) return false;
        entry.Gate.Wait();
        try { entry.Session = entry.Session with { Closed = true }; }
        finally { entry.Gate.Release(); }
        return true;
    }

    public bool TryGet(string sessionId, out SurfaceSession? session)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            session = entry.Session;
            return true;
        }
        session = null;
        return false;
    }

    public IReadOnlyList<SurfaceSession> Snapshot()
        => _sessions.Values.Select(x => x.Session).ToArray();

    private static SurfaceRuntimeReceipt Reject(
        string sessionId, SurfaceMutation mutation, long revision, string error)
        => new(Guid.NewGuid().ToString("n"), sessionId, mutation.IntentId,
            mutation.Operation, revision, false, error);
}
