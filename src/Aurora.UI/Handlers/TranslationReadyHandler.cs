using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.UI.Handlers;

internal sealed class TranslationReadyHandler : INotificationHandler<TranslationReady>
{
    private readonly OverlayService _overlay;

    public TranslationReadyHandler(OverlayService overlay) => _overlay = overlay;

    public Task Handle(TranslationReady notification, CancellationToken cancellationToken)
    {
        _overlay.ShowTranslation(notification.Result);
        return Task.CompletedTask;
    }
}
