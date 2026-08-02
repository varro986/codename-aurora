using Aurora.Admin;
using Aurora.Core;
using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using Aurora.OCR;
using Aurora.Translation;
using Aurora.UI;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aurora.App;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // VelopackApp.Build().Run(args); — uncomment when Velopack is added (US-001 auto-update)

        var services = new ServiceCollection();
        ConfigureServices(services);
        using var provider = services.BuildServiceProvider();

        WireEvents(provider);

        var wpfApp = new App(provider);
        wpfApp.Run();

        provider.GetRequiredService<IContinuousModeController>().Stop();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b => b.AddConsole());

        // Core contracts
        services.AddSingleton<IAppSettings, AppSettings>();
        services.AddSingleton<ISettingsWriter, SettingsWriter>();
        services.AddSingleton<IContinuousModeController, ContinuousModeController>();

        // Module services
        services.AddSingleton<IOcrService, OcrService>();
        services.AddSingleton<ModelManager>();
        services.AddSingleton<IModelManager>(p => p.GetRequiredService<ModelManager>());
        services.AddSingleton<TranslationEngine>();
        services.AddSingleton<ITranslationEngine>(p => p.GetRequiredService<TranslationEngine>());
        services.AddSingleton<AdminService>();

        // UI services
        services.AddSingleton<HotkeyManager>();
        services.AddSingleton<TrayIconManager>();
        services.AddSingleton<OverlayService>();
        services.AddSingleton<HoverGlossaryService>();

        // App internal
        services.AddSingleton<ICaptureService, CaptureService>();
        services.AddSingleton<PipelineExecutor>();

        // MediatR — scan handlers from App and UI assemblies
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                typeof(Program).Assembly,
                typeof(App).Assembly);
        });
    }

    private static void WireEvents(IServiceProvider provider)
    {
        var publisher = provider.GetRequiredService<IPublisher>();

        // Wire ContinuousModeController.CaptureTick → PipelineExecutor
        var controller = provider.GetRequiredService<IContinuousModeController>();
        var executor = provider.GetRequiredService<PipelineExecutor>();
        controller.CaptureTick += (_, _) => _ = executor.ExecuteAsync();

        // Wire TranslationEngine.DictionaryHotReloaded → publish DictionaryReloaded via MediatR
        var engine = provider.GetRequiredService<TranslationEngine>();
        engine.DictionaryHotReloaded += (_, _) =>
            _ = publisher.Publish(new DictionaryReloaded());

        // Wire AdminService.UpdateFound → publish UpdateAvailable via MediatR
        var admin = provider.GetRequiredService<AdminService>();
        admin.UpdateFound += (_, version) =>
            _ = publisher.Publish(new UpdateAvailable(version));

        // Check for updates at startup (non-blocking)
        _ = admin.CheckForUpdatesAsync();
    }
}
