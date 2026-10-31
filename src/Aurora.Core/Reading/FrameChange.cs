using Aurora.Core.Contracts;

namespace Aurora.Core.Reading;

/// <summary>Remembers the last accepted frame and tells whether the next one differs from it.</summary>
public sealed class FrameChange
{
    private byte[]? _bytes;
    private int _width;
    private int _height;
    private int _stride;

    /// <summary>True, and remembers the frame, when it differs from the last accepted one; false otherwise.</summary>
    public bool Accept(Frame frame)
    {
        var span = frame.Bgra.Span;
        if (_bytes is not null
            && frame.Width == _width && frame.Height == _height && frame.Stride == _stride
            && span.SequenceEqual(_bytes))
        {
            return false;
        }

        if (_bytes?.Length != span.Length)
        {
            _bytes = new byte[span.Length];
        }

        span.CopyTo(_bytes);
        (_width, _height, _stride) = (frame.Width, frame.Height, frame.Stride);
        return true;
    }

    public void Reset() => _bytes = null;
}
