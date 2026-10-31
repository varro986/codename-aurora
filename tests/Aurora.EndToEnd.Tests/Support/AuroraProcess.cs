using System.Diagnostics;

namespace Aurora.EndToEnd.Tests.Support;

/// <summary>The built Aurora app, started with its own log file and a hotkey nobody else uses; killed on dispose.</summary>
internal sealed class AuroraProcess : IAsyncDisposable
{
    private const string AppTfm = "net10.0-windows10.0.22621.0";
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    private readonly string _folder;
    private readonly string _logFile;

    private AuroraProcess(Process process, string folder, string logFile) =>
        (Process, _folder, _logFile) = (process, folder, logFile);

    public Process Process { get; }

    public static async Task<AuroraProcess> StartAsync(CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"aurora-e2e-{Guid.NewGuid():N}");
        var logFile = Path.Combine(folder, "aurora.log");
        var start = new ProcessStartInfo(Path.Combine(OutputFolder(), "Aurora.App.exe")) { UseShellExecute = false };
        start.Environment["Aurora__LogFile"] = logFile;
        start.Environment["Aurora__Hotkey"] = "Ctrl+Alt+Shift+F20";
        var process = Process.Start(start) ?? throw new InvalidOperationException("Aurora did not start.");
        var app = new AuroraProcess(process, folder, logFile);
        try
        {
            await app.WaitForLogAsync("Ready:", cancellationToken);
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    /// <summary>The folder of the app build, found like the targets: same configuration as the running tests.</summary>
    public static string OutputFolder()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var repository = output.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var path = Path.Combine(repository, "src", "Aurora.App", "bin", configuration, AppTfm);
        return File.Exists(Path.Combine(path, "Aurora.App.exe")) ? path : throw new FileNotFoundException("Build the solution first.", path);
    }

    public string Log()
    {
        if (!File.Exists(_logFile))
        {
            return string.Empty;
        }

        // The app appends while we read.
        using var stream = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public Task WaitForLogAsync(string text, CancellationToken cancellationToken) =>
        Poll.UntilAsync(
            () => Log().Contains(text, StringComparison.Ordinal),
            $"Aurora never logged '{text}'. Log:{Environment.NewLine}{Log()}",
            cancellationToken,
            StartTimeout);

    public IReadOnlyList<nint> VisibleWindows() => Desktop.VisibleWindowsOf(Process.Id);

    /// <summary>The overlay: a visible window of the app with the click-through style.</summary>
    public nint? Overlay() =>
        VisibleWindows().Where(w => Desktop.HasExtendedStyles(w, Desktop.WsExTransparent)).Select(w => (nint?)w).FirstOrDefault();

    public async ValueTask DisposeAsync()
    {
        if (!Process.HasExited)
        {
            Process.Kill(entireProcessTree: true);
            await Process.WaitForExitAsync();
        }

        Process.Dispose();
        File.Delete(_logFile);
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder);
        }
    }
}
