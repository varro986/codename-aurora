namespace Aurora.Core.Contracts;

/// <summary>One captured image, BGRA 8 bits per channel, in physical pixels.</summary>
public sealed record Frame(int Width, int Height, int Stride, ReadOnlyMemory<byte> Bgra);

public interface IFrameSource : IAsyncDisposable
{
    /// <summary>
    /// The latest frame at once, even if unchanged since the last call; it waits only for the very first frame.
    /// Null when the source is gone (window closed, monitor removed).
    /// </summary>
    Task<Frame?> NextAsync(CancellationToken cancellationToken);
}

public interface IFrameSourceFactory
{
    IFrameSource Create(WindowTarget window);

    IFrameSource Create(MonitorTarget monitor);
}
