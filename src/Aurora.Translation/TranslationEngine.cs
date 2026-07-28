using Aurora.Core.Interfaces;

namespace Aurora.Translation;

public sealed class TranslationEngine : ITranslationEngine
{
    public Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
