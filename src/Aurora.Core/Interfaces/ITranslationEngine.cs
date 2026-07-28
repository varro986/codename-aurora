namespace Aurora.Core.Interfaces;

public interface ITranslationEngine
{
    Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default);
}
