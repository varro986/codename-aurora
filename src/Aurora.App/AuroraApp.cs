using Aurora.Core.Settings;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Aurora.App;

/// <summary>The Avalonia application: no window, only the tray shell.</summary>
internal sealed class AuroraApp(AuroraSettings settings) : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        var lifetime = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var log = new FileLog(settings.LogFile, settings.LogMaxBytes);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Error("Unhandled error", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => log.Error("Unobserved task error", e.Exception);
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            log.Error("Error on the UI thread", e.Exception);
            e.Handled = true;
        };

        log.Info("Aurora started.");
        // The tray icon registered on the application keeps the shell alive; Quit disposes it.
        _ = new TrayShell(settings, log, lifetime);
        base.OnFrameworkInitializationCompleted();
    }
}
