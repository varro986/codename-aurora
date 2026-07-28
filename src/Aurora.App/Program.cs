using Aurora.UI;

namespace Aurora.App;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // VelopackApp.Build().Run(args) must be the very first call — ADR-005.
        // Uncomment once Velopack is added: VelopackApp.Build().Run(args);

        // DI registration lives here — ADR-006, ADR-007.
        // Concrete registrations are driven by user stories.

        var app = new App();
        app.Run();
    }
}
