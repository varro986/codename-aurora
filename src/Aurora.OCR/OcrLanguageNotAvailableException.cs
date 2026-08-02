namespace Aurora.OCR;

public sealed class OcrLanguageNotAvailableException : Exception
{
    public string Language { get; }

    public OcrLanguageNotAvailableException(string language)
        : base($"WinRT OCR language '{language}' is not available on this system.")
    {
        Language = language;
    }
}
