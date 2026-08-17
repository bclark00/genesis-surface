using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Genesis.Surface.Abstractions;

namespace Genesis.Windows.Ribosome;

/// <summary>
/// Native WPF realization of a password request expressed through the Surface
/// layer. Explanatory content is projected from SurfaceSpec; the secret is
/// collected only by PasswordBox and is never rendered into the surface tree.
/// </summary>
public static class WpfSurfacePasswordPrompt
{
    public static string? Show(SurfaceSpec spec, string passwordLabel = "Vault passphrase", Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var window = new Window
        {
            Title = spec.Title ?? passwordLabel,
            Width = 560,
            MinWidth = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Owner = owner,
        };

        var root = new StackPanel { Margin = new Thickness(24) };
        new WpfSurfaceProjector(root).Project(spec);
        root.Children.Add(new System.Windows.Controls.TextBlock { Text = passwordLabel, Margin = new Thickness(0, 18, 0, 6), FontWeight = FontWeights.SemiBold });

        var password = new PasswordBox { MinWidth = 400, Padding = new Thickness(8), FontFamily = new FontFamily("Consolas") };
        root.Children.Add(password);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = new Button { Content = "Cancel", Width = 96, Margin = new Thickness(0, 0, 10, 0) };
        var submit = new Button { Content = "Unlock", Width = 96, IsDefault = true };
        cancel.Click += (_, _) => window.DialogResult = false;
        submit.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(password.Password)) window.DialogResult = true; };
        buttons.Children.Add(cancel);
        buttons.Children.Add(submit);
        root.Children.Add(buttons);
        window.Content = root;
        window.Loaded += (_, _) => password.Focus();
        return window.ShowDialog() == true ? password.Password : null;
    }
}
