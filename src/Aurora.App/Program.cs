using Avalonia;
using Avalonia.Controls;

namespace Aurora.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var result = SettingsLoader.Load(AppContext.BaseDirectory);
        if (!result.IsValid)
        {
            // No tray yet and no console in a WinExe: a message box is the only place the user sees it.
            var message = $"Aurora cannot start. Fix appsettings.json:{Environment.NewLine}{string.Join(Environment.NewLine, result.Errors)}";
            NativeMethods.ShowError(message);
            return 1;
        }

        return AppBuilder.Configure(() => new AuroraApp(result.Settings!))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}
