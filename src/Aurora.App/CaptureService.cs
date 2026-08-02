using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Aurora.Core.Interfaces;

namespace Aurora.App;

internal sealed class CaptureService : ICaptureService
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private static bool BelongsToCurrentProcess(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    public byte[] CaptureScreen()
    {
        var hwnd = GetForegroundWindow();
        if (BelongsToCurrentProcess(hwnd))
            return Array.Empty<byte>();

        if (!GetWindowRect(hwnd, out var rect))
            return Array.Empty<byte>();

        int width = Math.Max(1, rect.Right - rect.Left);
        int height = Math.Max(1, rect.Bottom - rect.Top);

        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
