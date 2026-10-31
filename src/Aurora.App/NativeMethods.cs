using System.Runtime.InteropServices;

namespace Aurora.App;

internal static partial class NativeMethods
{
    private const uint MB_ICONERROR = 0x10;

    public static void ShowError(string message) => MessageBoxW(0, message, "Aurora", MB_ICONERROR);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint owner, string text, string caption, uint type);
}
