using System.Runtime.InteropServices;

namespace Aurora.Windows.Tests.Capture;

/// <summary>A small topmost window, never activated, filled with one colour by its class brush; runs on its own thread.</summary>
internal sealed class ColorWindow : IDisposable
{
    public const byte Red = 255;
    public const byte Green = 0;
    public const byte Blue = 255;

    private const string ClassName = "AuroraCaptureTestColor";

    private static readonly Lazy<bool> Registered = new(Register);

    private readonly ManualResetEventSlim _stop = new();
    private readonly Thread _thread;

    private ColorWindow()
    {
        var ready = new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() => Run(ready)) { IsBackground = true };
        _thread.Start();
        Handle = ready.Task.GetAwaiter().GetResult();
    }

    public nint Handle { get; }

    public static ColorWindow Show() => new();

    public void Dispose()
    {
        _stop.Set();
        _thread.Join();
        _stop.Dispose();
    }

    private static bool Register()
    {
        var defaultProc = NativeLibrary.GetExport(NativeLibrary.Load("user32.dll"), "DefWindowProcW");
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProc = defaultProc,
            Background = NativeMethods.CreateSolidBrush((uint)(Red | (Green << 8) | (Blue << 16))),
            ClassName = Marshal.StringToHGlobalUni(ClassName),
        };
        return NativeMethods.RegisterClass(in windowClass) != 0;
    }

    private void Run(TaskCompletionSource<nint> ready)
    {
        try
        {
            Assert.True(Registered.Value);
            var window = NativeMethods.CreateWindow(
                NativeMethods.WsExTopmost | NativeMethods.WsExNoActivate, ClassName, "aurora-capture-test",
                NativeMethods.WsPopup, 300, 300, 300, 200, 0, 0, 0, 0);
            Assert.NotEqual(0, window);
            NativeMethods.ShowWindow(window, NativeMethods.SwShowNoActivate);
            ready.SetResult(window);

            while (!_stop.IsSet)
            {
                while (NativeMethods.PeekMessage(out var message, 0, 0, 0, NativeMethods.PmRemove))
                {
                    NativeMethods.DispatchMessage(in message);
                }

                Thread.Sleep(10);
            }

            NativeMethods.DestroyWindow(window);
        }
        catch (Exception ex)
        {
            ready.TrySetException(ex);
        }
    }
}
