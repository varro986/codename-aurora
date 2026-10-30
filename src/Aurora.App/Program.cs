namespace Aurora.App;

internal static class Program
{
    // Tray, overlay and wiring arrive with T8; until then the app validates its configuration and exits.
    [STAThread]
    public static int Main()
    {
        var result = SettingsLoader.Load(AppContext.BaseDirectory);
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine(error);
        }

        return result.IsValid ? 0 : 1;
    }
}
