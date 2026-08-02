using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.App.Handlers;

internal sealed class ShutdownRequestedHandler : INotificationHandler<ShutdownRequested>
{
    private readonly IContinuousModeController _controller;

    public ShutdownRequestedHandler(IContinuousModeController controller)
    {
        _controller = controller;
    }

    public Task Handle(ShutdownRequested notification, CancellationToken cancellationToken)
    {
        _controller.Stop();
        System.Windows.Application.Current.Dispatcher.InvokeAsync(
            () => System.Windows.Application.Current.Shutdown());
        return Task.CompletedTask;
    }
}
