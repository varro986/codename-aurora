using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Aurora.Windows.Tests.Support;

/// <summary>
/// A running sample target from tests/targets (protocol in tests/targets/README.md): started in its own window,
/// commanded over its named pipe, closed on dispose.
/// </summary>
public sealed partial class SampleTarget : IAsyncDisposable
{
    public const string Desktop = "Aurora.Targets.Desktop";
    public const string Console = "Aurora.Targets.Console";

    // A cold start of the Avalonia target can take more than 20 s on a busy machine.
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    private readonly NamedPipeClientStream _pipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private SampleTarget(Process process, NamedPipeClientStream pipe)
    {
        Process = process;
        _pipe = pipe;
        _reader = new StreamReader(pipe, leaveOpen: true);
        _writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
    }

    /// <summary>The target's own process, the one that owns its window (not conhost for the console target).</summary>
    public Process Process { get; }

    /// <summary>Starts a target and waits until its pipe answers; lines are joined with '|'.</summary>
    public static async Task<SampleTarget> StartAsync(string name, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var pipeName = $"aurora-test-{Guid.NewGuid():N}";
        // conhost gives the console target a window of its own: under Windows Terminal it would belong to the terminal.
        var start = name == Console
            ? new ProcessStartInfo("conhost.exe") { UseShellExecute = true, ArgumentList = { ExePath(name) } }
            : new ProcessStartInfo(ExePath(name)) { UseShellExecute = true };
        start.ArgumentList.Add("--parent");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--pipe");
        start.ArgumentList.Add(pipeName);
        start.ArgumentList.Add("--text");
        start.ArgumentList.Add(string.Join('|', lines));
        using var launched = Process.Start(start) ?? throw new InvalidOperationException($"{name} did not start.");

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(StartTimeout, cancellationToken).ConfigureAwait(false);
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var serverId))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            return new SampleTarget(Process.GetProcessById((int)serverId), pipe);
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            launched.Kill(entireProcessTree: true);
            throw;
        }
    }

    /// <summary>Sends one command and returns the answer line ("ok", "ok &lt;value&gt;" or "error &lt;reason&gt;").</summary>
    public async Task<string> SendAsync(string command, CancellationToken cancellationToken)
    {
        await _writer.WriteLineAsync(command.AsMemory(), cancellationToken).ConfigureAwait(false);
        return await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new IOException("The target closed the pipe.");
    }

    /// <summary>The main window of the target (the console window for the console target), once it exists.</summary>
    public async Task<nint> MainWindowAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < deadline)
        {
            Process.Refresh();
            if (Process.MainWindowHandle != 0)
            {
                return Process.MainWindowHandle;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The target shows no main window.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!Process.HasExited)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await SendAsync("exit", timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // Gone or hung: killed below.
        }

        if (!Process.WaitForExit(TimeSpan.FromSeconds(5)))
        {
            Process.Kill();
        }

        _reader.Dispose();
        await _writer.DisposeAsync().ConfigureAwait(false);
        await _pipe.DisposeAsync().ConfigureAwait(false);
        Process.Dispose();
    }

    // tests/<project>/bin/<configuration>/<tfm>/ -> tests/targets/<name>/bin/<configuration>/net10.0/<name>.exe
    private static string ExePath(string name)
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var tests = output.Parent.Parent!.Parent!.Parent!.FullName;
        var path = Path.Combine(tests, "targets", name, "bin", configuration, "net10.0", $"{name}.exe");
        return File.Exists(path) ? path : throw new FileNotFoundException("Build the solution first.", path);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeServerProcessId(nint pipe, out uint serverProcessId);
}
