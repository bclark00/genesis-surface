using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Genesis.Surface.Abstractions;
using SurfaceContainerBlock = Genesis.Surface.Abstractions.ContainerBlock;
using SurfaceLogBlock = Genesis.Surface.Abstractions.LogBlock;
using SurfaceMetricBlock = Genesis.Surface.Abstractions.MetricBlock;
using SurfaceStatusBlock = Genesis.Surface.Abstractions.StatusBlock;
using SurfaceTextBlock = Genesis.Surface.Abstractions.TextBlock;
using SurfaceIntentBlock = Genesis.Surface.Abstractions.IntentBlock;
using SurfacePlanBlock = Genesis.Surface.Abstractions.PlanBlock;
using SurfaceAuthorizationBlock = Genesis.Surface.Abstractions.AuthorizationBlock;
using SurfaceExecutionStageBlock = Genesis.Surface.Abstractions.ExecutionStageBlock;
using SurfaceEvidenceTrailBlock = Genesis.Surface.Abstractions.EvidenceTrailBlock;

namespace Genesis.Windows.Ribosome;

/// <summary>
/// Projects canonical Surface Block IR into native WPF controls.
/// This is the Windows/XAML expression of the portable surface grammar.
/// </summary>
public sealed class WpfSurfaceProjector
{
    private readonly Panel _root;

    public WpfSurfaceProjector(Panel root) => _root = root;

    public ProjectionReceipt Project(SurfaceSpec spec)
    {
        try
        {
            _root.Children.Clear();
            foreach (var block in spec.Blocks)
                _root.Children.Add(ProjectBlock(block));
            return new ProjectionReceipt(Guid.NewGuid().ToString("n"), spec.SpecId,
                "wpf", spec.TargetSurfaceId, DateTimeOffset.UtcNow, true, spec.Blocks.Count);
        }
        catch (Exception ex)
        {
            return new ProjectionReceipt(Guid.NewGuid().ToString("n"), spec.SpecId,
                "wpf", spec.TargetSurfaceId, DateTimeOffset.UtcNow, false,
                spec.Blocks.Count, ex.Message);
        }
    }

    private static FrameworkElement ProjectBlock(BlockBase block) => block switch
    {
        SurfaceTextBlock text => Row(text.Label, text.Value, Brushes.LightGray),
        SurfaceMetricBlock metric => Row(metric.Label,
            metric.Value.ToString("G6", CultureInfo.InvariantCulture) +
            (string.IsNullOrWhiteSpace(metric.Unit) ? "" : $" {metric.Unit}"),
            Brushes.DeepSkyBlue),
        SurfaceStatusBlock status => Row(status.Label, status.Detail is null
            ? status.State : $"{status.State} — {status.Detail}", StatusBrush(status.State)),
        SurfaceContainerBlock container => Container(container),
        SurfaceLogBlock log => Log(log),
        SurfaceIntentBlock intent => Card("INTENT", $"{intent.Title}\n{intent.IntentId}", intent.Status, Brushes.MediumPurple),
        SurfacePlanBlock plan => Card("PLAN", $"{plan.Operation} → {plan.Target ?? "all"}", plan.Explanation, Brushes.DeepSkyBlue),
        SurfaceAuthorizationBlock authorization => Card("AUTHORIZATION", authorization.Operation, $"{authorization.Target} · {authorization.State}", StatusBrush(authorization.State)),
        SurfaceExecutionStageBlock stage => Row(stage.Stage, stage.Detail is null ? stage.State : $"{stage.State} — {stage.Detail}", StatusBrush(stage.State)),
        SurfaceEvidenceTrailBlock evidence => Log(new SurfaceLogBlock(evidence.BlockId, "EVIDENCE / RECEIPTS", evidence.Entries)),
        _ => new System.Windows.Controls.TextBlock { Text = block.BlockId, Foreground = Brushes.Gray }
    };

    private static FrameworkElement Card(string label, string value, string detail, Brush accent)
    {
        var panel = new Border { BorderBrush = accent, BorderThickness = new Thickness(1, 1, 1, 1), Padding = new Thickness(10), Margin = new Thickness(0, 4, 0, 4) };
        var stack = new StackPanel();
        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = label, Foreground = Brushes.Gray, FontSize = 10 });
        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = value, Foreground = accent, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        stack.Children.Add(new System.Windows.Controls.TextBlock { Text = detail, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        panel.Child = stack;
        return panel;
    }

    private static FrameworkElement Row(string label, string value, Brush valueBrush)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new System.Windows.Controls.TextBlock { Text = label, Foreground = Brushes.Gray });
        var result = new System.Windows.Controls.TextBlock { Text = value, Foreground = valueBrush, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(result, 1);
        grid.Children.Add(result);
        return grid;
    }

    private static FrameworkElement Container(SurfaceContainerBlock block)
    {
        var panel = new StackPanel { Orientation = block.Layout == "row" ? Orientation.Horizontal : Orientation.Vertical };
        if (!string.IsNullOrWhiteSpace(block.Title))
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = block.Title, Foreground = Brushes.DarkOrange, FontWeight = FontWeights.Bold });
        foreach (var child in block.Children) panel.Children.Add(ProjectBlock(child));
        return panel;
    }

    private static FrameworkElement Log(SurfaceLogBlock block)
    {
        var panel = new StackPanel();
        if (!string.IsNullOrWhiteSpace(block.Label))
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = block.Label, Foreground = Brushes.DarkOrange });
        foreach (var entry in block.Entries)
            panel.Children.Add(Row(entry.Ts, entry.Text, StatusBrush(entry.Level)));
        return panel;
    }

    private static Brush StatusBrush(string state) => state.ToLowerInvariant() switch
    {
        "healthy" or "info" => Brushes.LimeGreen,
        "degraded" or "warn" => Brushes.Orange,
        "down" or "error" => Brushes.IndianRed,
        _ => Brushes.LightGray
    };
}
