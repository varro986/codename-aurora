using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.App.Handlers;

internal sealed class WordDetailRequestedHandler : INotificationHandler<WordDetailRequested>
{
    private readonly ITranslationEngine _translation;
    private readonly IPublisher _publisher;

    public WordDetailRequestedHandler(ITranslationEngine translation, IPublisher publisher)
    {
        _translation = translation;
        _publisher = publisher;
    }

    public async Task Handle(WordDetailRequested notification, CancellationToken cancellationToken)
    {
        var result = await _translation.TranslateAsync(notification.Word, cancellationToken);
        await _publisher.Publish(new WordDetailReady(notification.Word, result), cancellationToken);
    }
}
