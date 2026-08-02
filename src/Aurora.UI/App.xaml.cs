using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.UI;

public partial class App : Application
{
    private readonly IServiceProvider _provider;
    private HotkeyManager? _hotkeys;
    private TrayIconManager? _tray;

    public App(IServiceProvider provider)
    {
        _provider = provider;
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _hotkeys = _provider.GetRequiredService<HotkeyManager>();
        _hotkeys.Register();

        _tray = _provider.GetRequiredService<TrayIconManager>();
        _tray.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
