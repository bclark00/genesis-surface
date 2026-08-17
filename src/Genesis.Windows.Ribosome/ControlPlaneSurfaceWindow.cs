using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Genesis.Surface.Abstractions;

namespace Genesis.Windows.Ribosome;

/// <summary>
/// Native WPF materializer for the canonical control-plane Surface IR.
/// WPF owns the desktop window and geometry; SurfaceSpec owns semantic blocks.
/// </summary>
public sealed class ControlPlaneSurfaceWindow : Window
{
    private readonly StackPanel _blocks = new();
    private readonly WpfSurfaceProjector _projector;

    public string SurfaceId => "genesis-desktop.control-plane";
    public string? CurrentSpecId { get; private set; }

    public ControlPlaneSurfaceWindow()
    {
        Title = "Genesis · Agent Control Plane";
        Width = 760;
        Height = 680;
        MinWidth = 480;
        MinHeight = 360;
        Background = new SolidColorBrush(Color.FromRgb(10, 14, 18));
        Foreground = Brushes.LightGray;

        var root = new DockPanel { Margin = new Thickness(18) };
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        heading.Children.Add(new System.Windows.Controls.TextBlock { Text = "SURFACE IR · CONTROL PLANE", Foreground = Brushes.DarkOrange, FontSize = 11 });
        heading.Children.Add(new System.Windows.Controls.TextBlock { Text = "Genesis Agent Control Plane", Foreground = Brushes.White, FontSize = 22, Margin = new Thickness(0, 4, 0, 0) });
        DockPanel.SetDock(heading, Dock.Top);
        root.Children.Add(heading);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _blocks };
        root.Children.Add(scroll);
        Content = root;
        _projector = new WpfSurfaceProjector(_blocks);
    }

    /// <summary>Projects a new IR snapshot while preserving stable block identity semantics.</summary>
    public ProjectionReceipt Project(SurfaceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        CurrentSpecId = spec.SpecId;
        return _projector.Project(spec);
    }
}
