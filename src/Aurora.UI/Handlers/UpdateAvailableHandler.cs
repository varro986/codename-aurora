using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.UI.Handlers;

internal sealed class UpdateAvailableHandler : INotificationHandler<UpdateAvailable>
{
    private readonly TrayIconManager _tray;

    public UpdateAvailableHandler(TrayIconManager tray) => _tray = tray;

    public Task Handle(UpdateAvailable notification, CancellationToken cancellationToken)
    {
        _tray.ShowUpdateNotification(notification.Version);
        return Task.CompletedTask;
    }
}
