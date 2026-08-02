using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using Aurora.UI;
using MediatR;

namespace Aurora.App.Handlers;

internal sealed class RulloToggleRequestedHandler : INotificationHandler<RulloToggleRequested>
{
    private readonly IContinuousModeController _controller;
    private readonly TrayIconManager _tray;

    public RulloToggleRequestedHandler(IContinuousModeController controller, TrayIconManager tray)
    {
        _controller = controller;
        _tray = tray;
    }

    public Task Handle(RulloToggleRequested notification, CancellationToken cancellationToken)
    {
        _controller.Toggle();
        _tray.SetContinuousActive(_controller.IsActive);
        return Task.CompletedTask;
    }
}
