using Aurora.OCR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aurora.Tests.Unit.OCR;

// These tests exercise only the null/empty-guard in RecognizeAsync — the code path that
// returns before any WinRT call, so no OCR language pack is needed.
public sealed class OcrServiceNullGuardTests
{
    private readonly OcrService _sut = new(NullLogger<OcrService>.Instance);

    [Fact]
    public async Task RecognizeAsync_NullBytes_ReturnsEmpty()
    {
        var result = await _sut.RecognizeAsync(null!, "it");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RecognizeAsync_EmptyBytes_ReturnsEmpty()
    {
        var result = await _sut.RecognizeAsync(Array.Empty<byte>(), "it");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RecognizeAsync_CancelledToken_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // 1-byte payload passes the null guard, then hits ct.ThrowIfCancellationRequested()
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.RecognizeAsync(new byte[] { 0xFF }, "it", cts.Token));
    }
}
