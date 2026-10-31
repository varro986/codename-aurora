using Aurora.Core.Contracts;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aurora.Overlay;

/// <summary>
/// A dimmed layer over every screen: a left click picks a point (on release, so the release never reaches the
/// application below), Esc cancels. Esc needs a layer window with the keyboard focus: the window under the pointer
/// takes it when the pointer enters, so until the pointer is over a screen the key may still go to the previous window.
/// </summary>
public sealed class PickLayer
{
    private TaskCompletionSource<PickResult?>? _pending;
    private List<Window> _windows = [];
    private bool _pressed;

    /// <summary>
    /// The picked point, or null when cancelled; the layer is already closed when this returns. A call while a pick is
    /// active returns that pick's task.
    /// </summary>
    public Task<PickResult?> PickAsync() => Dispatcher.UIThread.InvokeAsync(StartOrJoin);

    private Task<PickResult?> StartOrJoin()
    {
        if (_pending is not null)
        {
            return _pending.Task;
        }

        _pending = new TaskCompletionSource<PickResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = _pending.Task;
        _pressed = false;
        var first = CreateWindow();
        foreach (var screen in first.Screens.All)
        {
            var window = _windows.Count == 0 ? first : CreateWindow();
            Place(window, screen);
            _windows.Add(window);
        }

        if (_windows.Count == 0)
        {
            Finish(null);
            return task;
        }

        foreach (var window in _windows)
        {
            window.Show();
        }

        _windows[0].Activate();
        return task;
    }

    private Window CreateWindow()
    {
        var window = new Window
        {
            WindowDecorations = WindowDecorations.None,
            Topmost = true,
            ShowInTaskbar = false,
            CanResize = false,
            Background = OverlayStyle.PickDim,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Cursor = new Cursor(StandardCursorType.Cross),
        };
        window.PointerEntered += (_, _) => window.Activate();
        window.PointerPressed += OnPointerPressed;
        window.PointerReleased += OnPointerReleased;
        window.KeyDown += OnKeyDown;
        window.Closed += (_, _) => Finish(null);
        return window;
    }

    private static void Place(Window window, Avalonia.Platform.Screen screen)
    {
        window.Position = screen.Bounds.Position;
        window.Width = screen.Bounds.Width / screen.Scaling;
        window.Height = screen.Bounds.Height / screen.Scaling;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var window = (Window)sender!;
        _pressed = e.GetCurrentPoint(window).Properties.IsLeftButtonPressed;
        e.Handled = true;
    }

    // Only a release that follows a press on the layer counts: a button already down when the layer opened is ignored.
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        var window = (Window)sender!;
        var point = Avalonia.VisualExtensions.PointToScreen(window, e.GetPosition(window));
        Finish(new PickResult(new PixelPoint(point.X, point.Y), ToCore(e.KeyModifiers)));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Finish(null);
        }
    }

    // Windows are closed before the result is set, so the caller never finds the layer under the point.
    private void Finish(PickResult? result)
    {
        var completion = _pending;
        if (completion is null)
        {
            return;
        }

        _pending = null;
        var windows = _windows;
        _windows = [];
        foreach (var window in windows)
        {
            window.Close();
        }

        completion.SetResult(result);
    }

    private static Aurora.Core.Contracts.KeyModifiers ToCore(Avalonia.Input.KeyModifiers modifiers)
    {
        var result = Aurora.Core.Contracts.KeyModifiers.None;
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
        {
            result |= Aurora.Core.Contracts.KeyModifiers.Control;
        }

        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
        {
            result |= Aurora.Core.Contracts.KeyModifiers.Shift;
        }

        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt))
        {
            result |= Aurora.Core.Contracts.KeyModifiers.Alt;
        }

        return result;
    }
}
