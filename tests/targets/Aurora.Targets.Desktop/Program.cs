using Avalonia;

namespace Aurora.Targets.Desktop;

// Sample target for the tests (IDEA-10): protocol in tests/targets/README.md.
internal static class Program
{
    public static int Main(string[] args) =>
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
