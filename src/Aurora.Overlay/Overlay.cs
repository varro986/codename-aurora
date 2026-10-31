using Aurora.Core.Contracts;
using Avalonia.Threading;

namespace Aurora.Overlay;

/// <summary>The translation drawn over the target; every call is marshalled to the UI thread.</summary>
public sealed class Overlay(IOverlayWindowStyler styler) : IOverlay
{
    private OverlayWindow? _window;

    public void Show(PixelRect targetBounds, IReadOnlyList<OcrLine> lines)
    {
        var snapshot = lines.ToArray();
        Dispatcher.UIThread.Post(() => ShowOnUiThread(targetBounds, snapshot));
    }

    public void Hide() => Dispatcher.UIThread.Post(() => _window?.Hide());

    private void ShowOnUiThread(PixelRect targetBounds, IReadOnlyList<OcrLine> lines)
    {
        _window ??= new OverlayWindow(styler);
        _window.Present(targetBounds, lines);
        _window.Show();
    }
}
