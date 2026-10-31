using Aurora.Core.Contracts;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Aurora.Overlay;

/// <summary>Transparent topmost window that paints each translated line over its original; positions in screen pixels.</summary>
internal sealed class OverlayWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly IOverlayWindowStyler _styler;
    private PixelRect _targetBounds;
    private IReadOnlyList<OcrLine> _lines = [];
    private bool _styled;

    public OverlayWindow(IOverlayWindowStyler styler)
    {
        _styler = styler;
        WindowDecorations = WindowDecorations.None;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        Focusable = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Content = _canvas;

        // Invisible until the native styles are applied, so the window never shows up clickable or unpainted.
        Opacity = 0;
        Opened += (_, _) => ApplyStyle();

        // A move to a monitor with another scaling can make Avalonia resize the window: pin it to the target again.
        ScalingChanged += (_, _) => PlaceOnTarget();
    }

    public void Present(PixelRect targetBounds, IReadOnlyList<OcrLine> lines)
    {
        _targetBounds = targetBounds;
        _lines = lines;
        PlaceOnTarget();
    }

    private void ApplyStyle()
    {
        if (_styled || TryGetPlatformHandle() is not { } handle)
        {
            return;
        }

        _styler.Apply(handle.Handle);
        _styled = true;
        Opacity = 1;
    }

    private void PlaceOnTarget()
    {
        var targetBounds = _targetBounds;
        Position = new Avalonia.PixelPoint(targetBounds.X, targetBounds.Y);

        // The screen's own scaling is what the window gets once it sits there, so the size is right from the first frame.
        var scaling = Screens.ScreenFromBounds(ToAvalonia(targetBounds))?.Scaling ?? RenderScaling;
        Width = targetBounds.Width / scaling;
        Height = targetBounds.Height / scaling;

        _canvas.Children.Clear();
        foreach (var line in _lines)
        {
            _canvas.Children.Add(CreateLine(line, targetBounds, scaling));
        }
    }

    private static Border CreateLine(OcrLine line, PixelRect targetBounds, double scaling)
    {
        var width = line.Bounds.Width / scaling;
        var height = line.Bounds.Height / scaling;

        var text = new TextBlock
        {
            Text = line.Text,
            FontSize = Math.Max(1, height * OverlayStyle.FontHeightRatio),
            Foreground = OverlayStyle.LineForeground,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Shrinks text that is wider than the original line; never enlarges it.
        var fitted = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = text,
        };

        var box = new Border
        {
            Width = width,
            Height = height,
            Background = OverlayStyle.LineBackground,
            ClipToBounds = true,
            Child = fitted,
        };
        Canvas.SetLeft(box, (line.Bounds.X - targetBounds.X) / scaling);
        Canvas.SetTop(box, (line.Bounds.Y - targetBounds.Y) / scaling);
        return box;
    }

    private static Avalonia.PixelRect ToAvalonia(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
