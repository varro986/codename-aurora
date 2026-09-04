using System;
using System.Windows;

namespace CodenameAurora.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new App();
        app.Run();
    }
}
