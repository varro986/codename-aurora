using System.Runtime.InteropServices;
using global::Windows.Graphics.Capture;
using global::Windows.Graphics.DirectX.Direct3D11;

namespace Aurora.Windows.Capture;

/// <summary>The COM interop that turns handles into capture items and builds the Direct3D device.</summary>
internal static unsafe class CaptureItemFactory
{
    private const string ItemClass = "Windows.Graphics.Capture.GraphicsCaptureItem";

    public static GraphicsCaptureItem ForWindow(nint handle) => Create(NativeMethods.CreateForWindowSlot, handle);

    public static GraphicsCaptureItem ForMonitor(nint handle) => Create(NativeMethods.CreateForMonitorSlot, handle);

    public static IDirect3DDevice CreateDevice()
    {
        Marshal.ThrowExceptionForHR(NativeMethods.D3D11CreateDevice(
            0, NativeMethods.DriverTypeHardware, 0, NativeMethods.CreateDeviceBgraSupport, 0, 0,
            NativeMethods.SdkVersion, out var d3dDevice, 0, out var context));
        Marshal.Release(context);
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, in NativeMethods.DxgiDeviceId, out var dxgiDevice));
            try
            {
                Marshal.ThrowExceptionForHR(NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var abi));
                try
                {
                    return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(abi);
                }
                finally
                {
                    Marshal.Release(abi);
                }
            }
            finally
            {
                Marshal.Release(dxgiDevice);
            }
        }
        finally
        {
            Marshal.Release(d3dDevice);
        }
    }

    private static GraphicsCaptureItem Create(int slot, nint handle)
    {
        Marshal.ThrowExceptionForHR(NativeMethods.WindowsCreateString(ItemClass, ItemClass.Length, out var className));
        try
        {
            Marshal.ThrowExceptionForHR(NativeMethods.RoGetActivationFactory(
                className, in NativeMethods.GraphicsCaptureItemInteropId, out var factory));
            try
            {
                var itemId = NativeMethods.GraphicsCaptureItemId;
                nint abi;
                var create = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)(*(void***)factory)[slot];
                Marshal.ThrowExceptionForHR(create(factory, handle, &itemId, &abi));
                try
                {
                    return GraphicsCaptureItem.FromAbi(abi);
                }
                finally
                {
                    Marshal.Release(abi);
                }
            }
            finally
            {
                Marshal.Release(factory);
            }
        }
        finally
        {
            _ = NativeMethods.WindowsDeleteString(className);
        }
    }
}
