using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.UI.Handlers;

internal sealed class DictionaryReloadedHandler : INotificationHandler<DictionaryReloaded>
{
    private readonly TrayIconManager _tray;

    public DictionaryReloadedHandler(TrayIconManager tray) => _tray = tray;

    public Task Handle(DictionaryReloaded notification, CancellationToken cancellationToken)
    {
        _tray.ShowDictionaryReloadedNotification();
        return Task.CompletedTask;
    }
}
