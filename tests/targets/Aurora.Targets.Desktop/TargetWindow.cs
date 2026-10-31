using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aurora.Targets.Desktop;

// Plain dark text on a light background in a large font, so OCR reads it reliably.
internal sealed class TargetWindow : Window
{
    public const string MainTitle = "Aurora Target Desktop";
    private const double TextSize = 48;
    private const int InitialX = 100;
    private const int InitialY = 100;
    private const double InitialWidth = 900;
    private const double InitialHeight = 400;

    public TargetWindow(string title, string[] lines)
    {
        Title = title;
        Width = InitialWidth;
        Height = InitialHeight;
        Position = new PixelPoint(InitialX, InitialY);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.White;
        SetLines(lines);
    }

    public void SetLines(string[] lines)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        foreach (var line in lines)
        {
            panel.Children.Add(new TextBlock { Text = line, FontSize = TextSize, Foreground = Brushes.Black });
        }

        Content = panel;
    }
}
