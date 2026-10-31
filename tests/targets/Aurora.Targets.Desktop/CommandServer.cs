using System.IO.Pipes;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Aurora.Targets.Desktop;

// Reads one command per line from the pipe, runs it on the UI thread and answers once the change is applied.
internal static class CommandServer
{
    public const char LineSeparator = '|';
    private const string SecondTitle = "Aurora Target Desktop Second";

    // A server that dies unexpectedly would leave the window open with no pipe: close the target instead.
    public static void Start(string pipeName, TargetWindow main, IClassicDesktopStyleApplicationLifetime lifetime) =>
        Task.Run(async () =>
        {
            try
            {
                await Serve(pipeName, main, lifetime);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Command server failed: {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => lifetime.Shutdown(1));
            }
        });

    private static async Task Serve(string pipeName, TargetWindow main, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var exit = false;
        while (!exit)
        {
            await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync();
            exit = await ServeClient(pipe, main);
        }

        await Dispatcher.UIThread.InvokeAsync(() => lifetime.Shutdown());
    }

    // True when the client asked to exit; a client that drops just ends its turn.
    private static async Task<bool> ServeClient(NamedPipeServerStream pipe, TargetWindow main)
    {
        var utf8 = new UTF8Encoding(false);
        try
        {
            using var reader = new StreamReader(pipe, utf8, leaveOpen: true);
            await using var writer = new StreamWriter(pipe, utf8, leaveOpen: true) { AutoFlush = true };
            while (await reader.ReadLineAsync() is { } command)
            {
                await writer.WriteLineAsync(await RunOnUiThread(command, main));
                if (command == "exit")
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
            // The client went away: wait for the next one.
        }

        return false;
    }

    private static async Task<string> RunOnUiThread(string command, TargetWindow main)
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(() => Execute(command, main), DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            return $"error {ex.Message.ReplaceLineEndings(" ")}";
        }
    }

    private static string Execute(string command, TargetWindow main)
    {
        var space = command.IndexOf(' ', StringComparison.Ordinal);
        var name = space < 0 ? command : command[..space];
        var argument = space < 0 ? string.Empty : command[(space + 1)..];
        switch (name)
        {
            case "set":
                main.SetLines(Lines(argument));
                return "ok";
            case "second-window":
                new TargetWindow(SecondTitle, Lines(argument)).Show();
                return "ok";
            case "move" when TryPair(argument, out var x, out var y):
                main.Position = new PixelPoint(x, y);
                return "ok";
            case "resize" when TryPair(argument, out var w, out var h):
                main.Width = w / main.RenderScaling;
                main.Height = h / main.RenderScaling;
                return "ok";
            case "minimize":
                main.WindowState = WindowState.Minimized;
                return "ok";
            case "restore":
                main.WindowState = WindowState.Normal;
                return "ok";
            case "title":
                return $"ok {main.Title}";
            case "exit":
                return "ok";
            default:
                return $"error unknown command '{command}'";
        }
    }

    private static string[] Lines(string argument) => argument.Length == 0 ? [] : argument.Split(LineSeparator);

    private static bool TryPair(string argument, out int first, out int second)
    {
        var parts = argument.Split(' ');
        first = second = 0;
        return parts.Length == 2 && int.TryParse(parts[0], out first) && int.TryParse(parts[1], out second);
    }
}
