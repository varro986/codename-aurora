using Aurora.Core.Interfaces;

namespace Aurora.OCR;

public sealed class OcrService : IOcrService
{
    public Task<string> RecognizeTextAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
