using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Aurora.Targets.Desktop;

internal sealed class App : Application
{
    private const string DefaultText = "Aurora target desktop|The quick brown fox|0123456789";

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];
            var pipeName = ArgumentValue(args, "--pipe");
            if (pipeName is null)
            {
                Console.Error.WriteLine("Missing --pipe <name>");
                desktop.Shutdown(2);
                return;
            }

            var parent = ArgumentValue(args, "--parent");
            if (parent is not null)
            {
                if (!int.TryParse(parent, CultureInfo.InvariantCulture, out var parentId))
                {
                    Console.Error.WriteLine("--parent needs a process id");
                    desktop.Shutdown(2);
                    return;
                }

                ShutdownWhenEnds(parentId, desktop);
            }

            var text = ArgumentValue(args, "--text") ?? DefaultText;
            var window = new TargetWindow(TargetWindow.MainTitle, text.Split(CommandServer.LineSeparator));
            desktop.MainWindow = window;
            CommandServer.Start(pipeName, window, desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // The test that started this target may die without closing it: follow it.
    private static void ShutdownWhenEnds(int processId, IClassicDesktopStyleApplicationLifetime desktop) =>
        Task.Run(() =>
        {
            try
            {
                using var parent = Process.GetProcessById(processId);
                parent.WaitForExit();
            }
            catch (ArgumentException)
            {
                // Already gone.
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() => desktop.Shutdown());
        });

    private static string? ArgumentValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
