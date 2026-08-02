using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.UI.Handlers;

internal sealed class WordDetailReadyHandler : INotificationHandler<WordDetailReady>
{
    private readonly HoverGlossaryService _glossary;

    public WordDetailReadyHandler(HoverGlossaryService glossary) => _glossary = glossary;

    public Task Handle(WordDetailReady notification, CancellationToken cancellationToken)
    {
        _glossary.ShowWordDetail(notification.Word, notification.Detail); // Detail is TranslationResult
        return Task.CompletedTask;
    }
}
