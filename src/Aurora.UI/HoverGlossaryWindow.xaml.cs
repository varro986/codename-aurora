using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Aurora.Core;

namespace Aurora.UI;

public partial class HoverGlossaryWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private readonly DispatcherTimer _closeTimer;

    public HoverGlossaryWindow()
    {
        InitializeComponent();
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); Hide(); };
        MouseLeave += (_, _) => { _closeTimer.Stop(); _closeTimer.Start(); };
        MouseEnter += (_, _) => _closeTimer.Stop();
    }

    public void ShowWordDetail(string word, TranslationResult detail)
    {
        WordText.Text = word;
        DetailText.Text = $"{detail.Text}  [{detail.SourceLevel}]";

        var pos = System.Windows.Forms.Control.MousePosition;
        Left = pos.X + 12;
        Top = pos.Y + 12;
        Show();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

}
