namespace Aurora.Core.Interfaces;

public interface IOcrService
{
    Task<string> RecognizeTextAsync(byte[] imageData, CancellationToken cancellationToken = default);
}
