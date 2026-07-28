using System.Windows;

namespace Aurora.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // ShutdownMode is set to OnExplicitShutdown in App.xaml.
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // ADR-002: unregister all global hotkeys here before base.OnExit.
        base.OnExit(e);
    }
}
