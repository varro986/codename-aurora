using Aurora.Core;

namespace Aurora.Core.Interfaces;

public interface ITranslationEngine
{
    Task<TranslationResult> TranslateAsync(string text, CancellationToken ct = default);
}
