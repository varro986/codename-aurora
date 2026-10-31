using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;

namespace Aurora.Targets.Console;

// Sample target for the tests (IDEA-10): protocol in tests/targets/README.md.
internal static class Program
{
    private const string DefaultText = "Aurora target console|The quick brown fox|0123456789";
    private const char LineSeparator = '|';

    public static int Main(string[] args)
    {
        var pipeName = ArgumentValue(args, "--pipe");
        if (pipeName is null)
        {
            System.Console.Error.WriteLine("Missing --pipe <name>");
            return 2;
        }

        var parent = ArgumentValue(args, "--parent");
        if (parent is not null)
        {
            if (!int.TryParse(parent, CultureInfo.InvariantCulture, out var parentId))
            {
                System.Console.Error.WriteLine("--parent needs a process id");
                return 2;
            }

            ExitWhenEnds(parentId);
        }

        Show((ArgumentValue(args, "--text") ?? DefaultText).Split(LineSeparator));
        Serve(pipeName);
        return 0;
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void Show(string[] lines)
    {
        System.Console.BackgroundColor = ConsoleColor.White;
        System.Console.ForegroundColor = ConsoleColor.Black;
        System.Console.Clear();
        foreach (var line in lines)
        {
            System.Console.WriteLine(line);
        }
    }

    // The test that started this target may die without closing it: follow it.
    private static void ExitWhenEnds(int processId) =>
        new Thread(() =>
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

            Environment.Exit(0);
        })
        { IsBackground = true }.Start();

    private static void Serve(string pipeName)
    {
        var exit = false;
        while (!exit)
        {
            using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1);
            pipe.WaitForConnection();
            exit = ServeClient(pipe);
        }
    }

    // True when the client asked to exit; a client that drops just ends its turn.
    private static bool ServeClient(NamedPipeServerStream pipe)
    {
        var utf8 = new UTF8Encoding(false);
        try
        {
            using var reader = new StreamReader(pipe, utf8, leaveOpen: true);
            using var writer = new StreamWriter(pipe, utf8, leaveOpen: true) { AutoFlush = true };
            while (reader.ReadLine() is { } command)
            {
                writer.WriteLine(command == "exit" ? "ok" : Execute(command));
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

    private static string Execute(string command)
    {
        try
        {
            var space = command.IndexOf(' ', StringComparison.Ordinal);
            var name = space < 0 ? command : command[..space];
            var argument = space < 0 ? string.Empty : command[(space + 1)..];
            if (name == "set")
            {
                Show(argument.Length == 0 ? [] : argument.Split(LineSeparator));
                return "ok";
            }

            return $"error unknown command '{command}'";
        }
        catch (Exception ex)
        {
            return $"error {ex.Message.ReplaceLineEndings(" ")}";
        }
    }
}
