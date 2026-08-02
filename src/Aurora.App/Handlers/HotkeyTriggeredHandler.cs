using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.App.Handlers;

internal sealed class HotkeyTriggeredHandler : INotificationHandler<HotkeyTriggered>
{
    private readonly PipelineExecutor _executor;

    public HotkeyTriggeredHandler(PipelineExecutor executor)
    {
        _executor = executor;
    }

    public async Task Handle(HotkeyTriggered notification, CancellationToken cancellationToken)
    {
        await _executor.ExecuteAsync(cancellationToken);
    }
}
