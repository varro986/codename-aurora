namespace Aurora.Core.Interfaces;

public interface IOcrService
{
    Task<string> RecognizeAsync(byte[] imageBytes, string language, CancellationToken ct = default);
}
