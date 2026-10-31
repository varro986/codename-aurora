using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Aurora.Core.Contracts;
using global::Windows.Graphics;
using global::Windows.Graphics.Capture;
using global::Windows.Graphics.DirectX;
using global::Windows.Graphics.DirectX.Direct3D11;
using global::Windows.Graphics.Imaging;
using global::Windows.Security.Authorization.AppCapabilityAccess;

namespace Aurora.Windows.Capture;

/// <summary>
/// A window or a monitor on Windows Graphics Capture. Started on the first read; keeps only the latest frame, which
/// is returned again on every call until a newer one arrives. While it waits for the first frame (a minimised window
/// has none) it checks every <paramref name="pollInterval"/> that the target still exists.
/// </summary>
internal sealed class WindowsFrameSource(
    Func<nint, GraphicsCaptureItem> createItem, Func<nint, bool> exists, nint handle, TimeSpan pollInterval) : IFrameSource
{
    private const int BufferCount = 3;
    private const int BytesPerPixel = 4;
    private const DirectXPixelFormat PixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _sync = new();
    private readonly TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Borderless access is a per-process permission: asked once, unless the ask failed.
    private static Task<bool>? s_borderlessAllowed;

    private IDirect3DDevice? _device;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;
    private SizeInt32 _poolSize;
    private Direct3D11CaptureFrame? _pending;
    private Frame? _latest;
    private bool _gone;
    private bool _disposed;

    public async Task<Frame?> NextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await StartAsync().ConfigureAwait(false))
            {
                return null;
            }

            while (true)
            {
                Direct3D11CaptureFrame? pending;
                lock (_sync)
                {
                    _gone |= !exists(handle);
                    if (_gone)
                    {
                        return null;
                    }

                    pending = _pending;
                    _pending = null;
                }

                if (pending is not null)
                {
                    _latest = await ReadAsync(pending).ConfigureAwait(false);
                }

                if (_latest is not null)
                {
                    return _latest;
                }

                try
                {
                    await _signal.Task.WaitAsync(pollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // No frame yet: look again whether the target is still there.
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _gone = true;
        }

        _signal.TrySetResult();
        await _gate.WaitAsync().ConfigureAwait(false);
        if (_pool is not null)
        {
            _pool.FrameArrived -= OnFrameArrived;
        }

        _session?.Dispose();
        _pool?.Dispose();
        _pending?.Dispose();
        _device?.Dispose();
        _gate.Release();
    }

    // False when the target is already gone and no capture item can be made for it.
    private async Task<bool> StartAsync()
    {
        if (_session is not null)
        {
            return true;
        }

        if (_gone)
        {
            return false;
        }

        GraphicsCaptureItem item;
        try
        {
            item = createItem(handle);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            lock (_sync)
            {
                _gone = true;
            }

            return false;
        }

        // Fields are set only when everything was created; a failure tears down what exists and leaves the source unstarted.
        IDirect3DDevice? device = null;
        Direct3D11CaptureFramePool? pool = null;
        GraphicsCaptureSession? session = null;
        try
        {
            device = CaptureItemFactory.CreateDevice();
            var size = item.Size;
            pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, PixelFormat, BufferCount, size);
            pool.FrameArrived += OnFrameArrived;
            session = pool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;
            if (await BorderlessAllowedAsync().ConfigureAwait(false))
            {
                session.IsBorderRequired = false;
            }

            session.StartCapture();
            (_device, _pool, _session, _poolSize) = (device, pool, session, size);
            return true;
        }
        catch
        {
            if (pool is not null)
            {
                pool.FrameArrived -= OnFrameArrived;
            }

            session?.Dispose();
            pool?.Dispose();
            device?.Dispose();
            throw;
        }
    }

    private static Task<bool> BorderlessAllowedAsync() =>
        s_borderlessAllowed is { IsFaulted: false } known ? known : s_borderlessAllowed = RequestBorderlessAsync();

    private static async Task<bool> RequestBorderlessAsync() =>
        await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless) == AppCapabilityAccessStatus.Allowed;

    private void OnFrameArrived(Direct3D11CaptureFramePool pool, object _)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            var frame = pool.TryGetNextFrame();
            if (frame is null)
            {
                return;
            }

            _pending?.Dispose();
            _pending = frame;
        }

        _signal.TrySetResult();
    }

    private async Task<Frame> ReadAsync(Direct3D11CaptureFrame captured)
    {
        using (captured)
        {
            var content = captured.ContentSize;
            if (content.Width != _poolSize.Width || content.Height != _poolSize.Height)
            {
                _poolSize = content;
                _pool!.Recreate(_device, PixelFormat, BufferCount, content);
            }

            using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(captured.Surface);
            var buffer = new global::Windows.Storage.Streams.Buffer((uint)(bitmap.PixelWidth * bitmap.PixelHeight * BytesPerPixel));
            bitmap.CopyToBuffer(buffer);
            return Crop(buffer.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight, content);
        }
    }

    // A frame can still be larger than the content right after a resize: keep only the content.
    private static Frame Crop(byte[] source, int sourceWidth, int sourceHeight, SizeInt32 content)
    {
        var width = Math.Min(content.Width, sourceWidth);
        var height = Math.Min(content.Height, sourceHeight);
        if (width == sourceWidth && height == sourceHeight)
        {
            return new Frame(width, height, width * BytesPerPixel, source);
        }

        var stride = width * BytesPerPixel;
        var cropped = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            Buffer.BlockCopy(source, row * sourceWidth * BytesPerPixel, cropped, row * stride, stride);
        }

        return new Frame(width, height, stride, cropped);
    }
}
