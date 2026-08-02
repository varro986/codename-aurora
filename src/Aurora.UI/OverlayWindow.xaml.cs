using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Aurora.Core;
using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.UI;

public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private readonly IAppSettings _settings;
    private readonly IPublisher _publisher;
    private DispatcherTimer _dismissTimer = null!;
    private DispatcherTimer _dwellTimer = null!;
    private TranslationResult? _currentResult;

    public OverlayWindow(IPublisher publisher, IAppSettings settings)
    {
        _publisher = publisher;
        _settings = settings;
        InitializeComponent();
        ApplyColors();
        PositionBottomRight();

        _dismissTimer = new DispatcherTimer();
        _dismissTimer.Tick += (_, _) => { Hide(); _dismissTimer.Stop(); };

        _dwellTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(settings.HoverDwellThreshold)
        };
        _dwellTimer.Tick += OnDwellTick;
    }

    public void ShowTranslation(TranslationResult result)
    {
        // Called from OverlayService which already dispatches to UI thread.
        TranslationText.Text = result.Text;
        SourceLabel.Text = result.SourceLevel.ToString();
        _currentResult = result;
        Show();
        ScheduleDismiss();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

    private void ApplyColors()
    {
        var bg = ParseColor(_settings.OverlayBackgroundColor, Color.FromArgb(0xCC, 0, 0, 0));
        var fg = ParseColor(_settings.OverlayForegroundColor, Colors.White);
        Container.Background = new SolidColorBrush(bg);
        TranslationText.Foreground = new SolidColorBrush(fg);
        SourceLabel.Foreground = new SolidColorBrush(fg);
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private void PositionBottomRight()
    {
        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 20;
        Top = screen.Bottom - 120;
    }

    private void ScheduleDismiss()
    {
        _dismissTimer.Stop();
        _dismissTimer.Interval = _settings.OverlayDismissTimeout;
        _dismissTimer.Start();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _dwellTimer.Stop();
        _dwellTimer.Start();
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _dwellTimer.Stop();
    }

    private void OnDwellTick(object? sender, EventArgs e)
    {
        _dwellTimer.Stop();
        if (_currentResult is null) return;
        _ = _publisher.Publish(new WordDetailRequested(_currentResult.Text));
    }
}
