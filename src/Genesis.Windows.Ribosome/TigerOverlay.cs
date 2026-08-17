using System.Windows;
using System.Windows.Input;
using Genesis.Surface.Abstractions;
using Genesis.Surface.Runtime;

namespace Genesis.Windows.Ribosome;

public sealed record TigerStateRevision(
    long Revision,
    IReadOnlyDictionary<string, string> Text,
    IReadOnlyDictionary<string, double> Metrics,
    IReadOnlyDictionary<string, string> Status,
    TigerPosture? Posture = null,
    string? ProposedIntent = null,
    double IntentConfidence = 0,
    PendingApprovalTask? PendingApproval = null);

/// <summary>Event-driven state source for ambient Tiger projections.</summary>
public sealed class TigerStateHub
{
    private long _revision;
    private TigerStateRevision _current = new(0,
        new Dictionary<string, string>(),
        new Dictionary<string, double>(),
        new Dictionary<string, string>());

    public TigerStateRevision Current => _current;
    public event EventHandler<TigerStateRevision>? Changed;

    public TigerStateRevision Publish(
        IReadOnlyDictionary<string, string>? text = null,
        IReadOnlyDictionary<string, double>? metrics = null,
        IReadOnlyDictionary<string, string>? status = null,
        TigerPosture? posture = null,
        string? proposedIntent = null,
        double? intentConfidence = null,
        PendingApprovalTask? pendingApproval = null)
    {
        var next = new TigerStateRevision(
            Interlocked.Increment(ref _revision),
            text ?? _current.Text,
            metrics ?? _current.Metrics,
            status ?? _current.Status,
            posture ?? _current.Posture,
            proposedIntent ?? _current.ProposedIntent,
            intentConfidence ?? _current.IntentConfidence,
            pendingApproval ?? _current.PendingApproval);
        Interlocked.Exchange(ref _current, next);
        Changed?.Invoke(this, next);
        return next;
    }
}

/// <summary>
/// Native Tiger projection. SurfaceSpec/Block IR is projected directly into
/// WPF FrameworkElements; no browser, DOM, HTML, or V8 bridge is involved.
/// </summary>
public partial class TigerOverlayWindow : Window
{
    private readonly TigerStateHub _hub;
    private readonly SurfaceRuntime _runtime;
    private readonly WpfSurfaceProjector _projector;
    private WpfSurfaceChannel? _channel;
    private string? _sessionId;

    public string SurfaceId { get; }
    public event EventHandler<string>? IntentRequested;
    public event EventHandler<PendingApprovalTask>? ApprovalRequested;
    public event EventHandler<SurfaceRuntimeReceipt>? ProjectionReceipt;

    public TigerOverlayWindow(string surfaceId, TigerStateHub hub, SurfaceRuntime? runtime = null)
    {
        SurfaceId = surfaceId;
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _runtime = runtime ?? new SurfaceRuntime();
        InitializeComponent();
        _projector = new WpfSurfaceProjector(SurfacePanel);
        Loaded += OnLoaded;
        Activated += OnActivated;
        Closed += OnClosed;
        _hub.Changed += OnStateChanged;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _channel = new WpfSurfaceChannel(SurfaceId);
        _sessionId = _runtime.Open(
            new SurfaceOpenRequest(SurfaceId, "tiger-host", "ambient", false), _channel).SessionId;
        ApplyState(_hub.Current);
    }

    private void OnStateChanged(object? sender, TigerStateRevision state)
    {
        try { Dispatcher.Invoke(() => ApplyState(state)); }
        catch (InvalidOperationException) { }
    }

    private void ApplyState(TigerStateRevision state)
    {
        if (_sessionId is null || _channel is null) return;

        var spec = BuildSpec(state);
        _projector.Project(spec);
        _channel.SetProjectedSpec(spec);

        var blocks = spec.Blocks
            .Select(block => new RenderedBlock(block.BlockId, "application/xaml+xml", block.GetType().Name))
            .ToArray();
        var receipt = _runtime.ApplyAsync(
            _sessionId,
            new SurfaceMutation($"tiger-state-{state.Revision}", "replace", blocks))
            .GetAwaiter().GetResult();
        ProjectionReceipt?.Invoke(this, receipt);
    }

    private SurfaceSpec BuildSpec(TigerStateRevision state)
    {
        var blocks = new List<BlockBase>();
        blocks.AddRange(state.Text.Select(item =>
            new TextBlock($"text:{item.Key}", item.Key, item.Value)));
        blocks.AddRange(state.Metrics.Select(item =>
            new MetricBlock($"metric:{item.Key}", item.Key, item.Value)));
        blocks.AddRange(state.Status.Select(item =>
            new StatusBlock($"status:{item.Key}", item.Key, item.Value)));
        if (state.Posture is { } posture)
        {
            blocks.Add(new TextBlock("posture:basin", "basin", posture.Basin));
            blocks.Add(new TextBlock("posture:altitude", "altitude", posture.Altitude));
            blocks.Add(new MetricBlock("posture:clauses", "clauses", posture.Clauses));
            if (!string.IsNullOrWhiteSpace(posture.FrontierIntent))
                blocks.Add(new StatusBlock("posture:intent", "frontier intent", posture.FrontierIntent));
        }
        if (!string.IsNullOrWhiteSpace(state.ProposedIntent))
        {
            blocks.Add(new StatusBlock("intent:proposed", "proposed intent", state.ProposedIntent!,
                $"confidence {state.IntentConfidence:P0}"));
            DiagnoseButton.Content = $"confirm {state.ProposedIntent}";
        }
        if (state.PendingApproval is { } approval)
        {
            blocks.Add(new AuthorizationBlock(
                "approval:pending",
                approval.Title,
                approval.ProjectName,
                approval.Status));
            blocks.Add(new TextBlock(
                "approval:summary",
                "pending approval",
                approval.Summary));
            DiagnoseButton.Content = approval.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)
                ? "review / approve"
                : approval.Status;
        }

        return new SurfaceSpec(
            $"tiger-{state.Revision}", SurfaceId, "E_BASIN", "ground", blocks,
            "Genesis / Tiger", DateTimeOffset.UtcNow);
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private void OnSurfacePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsActive)
        {
            Activate();
            Focus();
        }
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        // Reassert the ambient surface's z-order after another window was
        // activated. This does not steal focus until the user clicks Tiger.
        Topmost = true;
    }

    private void OnDiagnoseClick(object sender, RoutedEventArgs e)
    {
        if (_hub.Current.PendingApproval is { } approval &&
            approval.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
        {
            ApprovalRequested?.Invoke(this, approval);
            return;
        }
        IntentRequested?.Invoke(this, _hub.Current.ProposedIntent ?? "diagnose");
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _hub.Changed -= OnStateChanged;
        if (_sessionId is not null) _runtime.Close(_sessionId);
        _channel?.Dispose();
    }
}
