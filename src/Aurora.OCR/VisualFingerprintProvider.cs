namespace Aurora.OCR;

internal static class VisualFingerprintProvider
{
    private const int Samples = 256;

    public static string Compute(byte[] imageBytes)
    {
        if (imageBytes.Length == 0) return string.Empty;

        Span<byte> sampled = stackalloc byte[Samples];
        var step = Math.Max(1, imageBytes.Length / Samples);
        for (var i = 0; i < Samples; i++)
            sampled[i] = imageBytes[Math.Min(i * step, imageBytes.Length - 1)];

        return Convert.ToHexString(sampled);
    }
}
