using System.Runtime.InteropServices;

namespace Aurora.Windows.Capture;

internal static partial class NativeMethods
{
    public const int DriverTypeHardware = 1;
    public const uint CreateDeviceBgraSupport = 0x20;
    public const uint SdkVersion = 7;

    // Vtable slots of IGraphicsCaptureItemInterop, an IUnknown interface: its two creators follow the three IUnknown methods.
    public const int CreateForWindowSlot = 3;
    public const int CreateForMonitorSlot = 4;

    public static readonly Guid GraphicsCaptureItemInteropId = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    public static readonly Guid GraphicsCaptureItemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    public static readonly Guid DxgiDeviceId = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

    [LibraryImport("combase.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int WindowsCreateString(string source, int length, out nint hstring);

    [LibraryImport("combase.dll")]
    public static partial int WindowsDeleteString(nint hstring);

    [LibraryImport("combase.dll")]
    public static partial int RoGetActivationFactory(nint classId, in Guid iid, out nint factory);

    [LibraryImport("d3d11.dll")]
    public static partial int D3D11CreateDevice(
        nint adapter, int driverType, nint software, uint flags, nint featureLevels, uint featureLevelCount,
        uint sdkVersion, out nint device, nint featureLevel, out nint immediateContext);

    [LibraryImport("d3d11.dll")]
    public static partial int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}
