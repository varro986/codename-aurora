using Aurora.OCR;
using Xunit;

namespace Aurora.Tests.Unit.OCR;

public sealed class VisualFingerprintProviderTests
{
    [Fact]
    public void Compute_SameBytes_ReturnsSameHash()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };

        var h1 = VisualFingerprintProvider.Compute(data);
        var h2 = VisualFingerprintProvider.Compute(data);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Compute_DifferentBytes_ReturnsDifferentHash()
    {
        var a = new byte[256];
        var b = new byte[256];
        for (var i = 0; i < 256; i++) { a[i] = (byte)i; b[i] = (byte)(255 - i); }

        Assert.NotEqual(VisualFingerprintProvider.Compute(a), VisualFingerprintProvider.Compute(b));
    }

    [Fact]
    public void Compute_SingleByte_DoesNotThrow()
    {
        var data = new byte[] { 42 };

        var result = VisualFingerprintProvider.Compute(data);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void Compute_LargeBuffer_ReturnsSameHashOnRepeatCall()
    {
        var data = new byte[1000];
        new Random(42).NextBytes(data);

        Assert.Equal(VisualFingerprintProvider.Compute(data), VisualFingerprintProvider.Compute(data));
    }

    [Fact]
    public void Compute_ReturnsHexString()
    {
        var data = new byte[512];
        new Random(1).NextBytes(data);

        var result = VisualFingerprintProvider.Compute(data);

        Assert.Matches("^[0-9A-Fa-f]+$", result);
    }

    [Fact]
    public void Compute_EmptyArray_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, VisualFingerprintProvider.Compute(Array.Empty<byte>()));
    }
}
